"""Explicit localhost-only synthetic demo. No production identity or provider."""
import tempfile
import time
from pathlib import Path
from wsgiref.simple_server import make_server, WSGIRequestHandler
from .deletion import DeletionService, Policy, Principal, Rejected, SyntheticProvider
from .http_app import create_app


def demo_service(database):
    def authenticate(proof):
        if proof != 'synthetic-demo-proof':
            raise Rejected('reauthentication_required')
        return Principal('synthetic-demo-account', time.time())
    return DeletionService(database, authenticate, SyntheticProvider(),
                           Policy('synthetic-policy-v1', 'title', True, True, True,
                                  ('synthetic account data: simulated removal only',)))


class QuietHandler(WSGIRequestHandler):
    def log_message(self, *_):
        pass


if __name__ == '__main__':
    with tempfile.TemporaryDirectory(prefix='tamer-deletion-demo-') as folder:
        service = demo_service(Path(folder) / 'requests.sqlite')
        with make_server('127.0.0.1', 8766, create_app(service, synthetic_demo=True), handler_class=QuietHandler) as http:
            print('Synthetic deletion demo: http://127.0.0.1:8766/privacy/deletion')
            http.serve_forever()
