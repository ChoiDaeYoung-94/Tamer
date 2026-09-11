"""Explicit C# -> real loopback WSGI contract check. Requires local .NET 9 SDK; no packages."""
import json
import subprocess
import tempfile
import threading
import time
from pathlib import Path
from wsgiref.simple_server import make_server
from .local_demo import demo_service, QuietHandler
from .http_app import create_app


def main():
    root = Path(__file__).resolve().parents[2]
    project = root / 'tools/revival/deletion_http_contract/DeletionHttpContract.csproj'
    subprocess.run(['dotnet', 'build', str(project), '--nologo', '-v', 'quiet'], cwd=root, check=True)
    assembly = project.parent / 'bin/Debug/net9.0/DeletionHttpContract.dll'
    modes = ('success', 'cancel', 'retry', 'identity', 'config', 'redirect', 'unknown', 'evidence',
             'timeout', 'malformed', 'oversize', 'error', 'cancel-token')
    for mode in modes:
        with tempfile.TemporaryDirectory(prefix='tamer-deletion-contract-') as folder:
            service = demo_service(Path(folder) / 'requests.sqlite')
            real_app = create_app(service, synthetic_demo=True)
            calls, failed_once = [], []

            def app(env, start):
                path = env['PATH_INFO']
                calls.append(path)
                if mode == 'retry' and path.endswith('/confirm') and not failed_once:
                    failed_once.append(True)
                    real_app(env, lambda *_: None)  # Commit, then simulate response loss.
                    start('503 Service Unavailable', [('Content-Type', 'application/json')])
                    return [b'{"code":"unavailable"}']
                if path.endswith('/config') and mode == 'config':
                    start('200 OK', [('Content-Type', 'application/json')])
                    return [b'{"available":true,"synthetic":false}']
                if path.endswith('/request'):
                    if mode == 'timeout':
                        time.sleep(0.8)
                    elif mode == 'redirect':
                        start('307 Temporary Redirect', [('Location', '/must-not-follow'), ('Content-Type', 'application/json')])
                        return [b'{}']
                    elif mode in ('unknown', 'evidence', 'malformed', 'oversize', 'error'):
                        start('409 Conflict' if mode == 'error' else '200 OK', [('Content-Type', 'application/json')])
                        if mode == 'malformed':
                            return [b'not json']
                        if mode == 'oversize':
                            return [b' ' * 17000]
                        return [json.dumps(dict(requestId='fake', policyRevision='fake', scope='title',
                                                state='completed' if mode == 'evidence' else 'unrecognized')).encode()]
                return real_app(env, start)

            with make_server('127.0.0.1', 0, app, handler_class=QuietHandler) as http:
                thread = threading.Thread(target=http.serve_forever, daemon=True)
                thread.start()
                try:
                    subprocess.run(['dotnet', str(assembly), f'http://127.0.0.1:{http.server_port}/', mode],
                                   cwd=root, check=True, timeout=20)
                finally:
                    http.shutdown()
                    thread.join(timeout=3)
            assert '/must-not-follow' not in calls
            if mode in ('identity', 'cancel-token'):
                assert not calls, calls
            if mode == 'config':
                assert calls == ['/v1/deletion/config'], calls
            if mode in ('success', 'cancel', 'retry'):
                with service.connection() as db:
                    rows = db.execute('SELECT state FROM deletion_requests').fetchall()
                    assert len(rows) == 1 and rows[0]['state'] == ('cancelled' if mode == 'cancel' else 'completed')
    print(f'C# -> Python loopback contract: {len(modes)}/{len(modes)} passed')


if __name__ == '__main__':
    main()
