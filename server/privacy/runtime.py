"""Single-host WSGI composition; no environment reads or network on import."""
from contextlib import closing, contextmanager
import logging
import os
import sqlite3
from urllib.parse import urlsplit

from .http_app import create_app
from .session_confirmation import compose


TABLE_COLUMNS = {
    'deletion_requests': 'id account client_key revision scope state challenge_hash expires completion_evidence',
    'title_deletion_operations': 'id title_id account entity_id policy_hash components phase evidence',
    'deletion_session_nonces': 'nonce_hash title_id account entity_id session_hash account_type intent_key purpose policy_hash created expires consumed',
    'deletion_session_proofs': 'proof_hash nonce_hash title_id account entity_id session_hash intent_key policy_hash confirmed expires',
    'deletion_receipt_access': 'request_id account entity_id client_key owner_hash binding verifier created expires acknowledged window_start read_count',
}


class SafeLogFilter(logging.Filter):
    def filter(self, record):
        record.msg, record.args = 'deletion_service_event', ()
        record.exc_info = record.exc_text = record.stack_info = None
        return True


def safe_logging():
    handler = logging.StreamHandler()
    handler.addFilter(SafeLogFilter())
    logging.basicConfig(handlers=[handler], level=logging.WARNING, force=True)


def check_database(database):
    # mode=rw cannot silently recreate a missing database. No migration/repair here.
    with closing(sqlite3.connect(database.as_uri() + '?mode=rw', uri=True, timeout=5)) as db:
        if db.execute('PRAGMA quick_check').fetchall() != [('ok',)]:
            raise ValueError('database_check_failed')
        for table, columns in TABLE_COLUMNS.items():
            actual = [row[1] for row in db.execute('PRAGMA table_info(' + table + ')')]
            if actual != columns.split():
                raise ValueError('database_schema_mismatch')


def components(settings, request=None):
    return compose(settings.database, settings.title, settings.secret, settings.policy,
                   request=request, recovery_origin=settings.origin,
                   recovery_ttl_seconds=settings.receipt_ttl)


def initialize(settings, request=None):
    # Explicit command only; never truncate/replace an existing database, even empty.
    with settings.database.open('xb'):
        pass
    components(settings, request)
    check_database(settings.database)


def build(settings, request=None):
    check_database(settings.database)
    service, confirmation = components(settings, request)
    core = create_app(service, session_confirmation=confirmation)
    host = urlsplit(settings.origin).netloc.lower()

    def reply(start_response, status, body):
        start_response(status, [('Content-Type', 'application/json'), ('Cache-Control', 'no-store'),
                                ('Content-Length', str(len(body)))])
        return [body]

    def application(environ, start_response):
        if environ.get('REQUEST_METHOD') == 'GET' and environ.get('PATH_INFO') == '/health/live':
            return reply(start_response, '200 OK', b'{"live":true}')
        if environ.get('wsgi.url_scheme') != 'https' or environ.get('HTTP_HOST', '').lower() != host:
            return reply(start_response, '403 Forbidden', b'{"code":"unavailable"}')
        if not settings.policy.enabled and environ.get('REQUEST_METHOD') != 'GET':
            return reply(start_response, '503 Service Unavailable', b'{"code":"policy_unavailable"}')
        return core(environ, start_response)
    return application


@contextmanager
def instance_lock(database):
    path = database.with_suffix('.lock')
    if path.is_symlink():
        raise ValueError('invalid_lock_path')
    # Keep the inode/file after release: unlinking would allow split lock ownership.
    with path.open('a+b') as stream:
        stream.seek(0, os.SEEK_END)
        if stream.tell() == 0:
            stream.write(b'0')
            stream.flush()
        stream.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == 'nt':
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream.fileno(), fcntl.LOCK_UN)


def make_server(app, port):
    from waitress.server import create_server
    # Exactly one process/thread behind a same-host TLS reverse proxy. Never trust *.
    return create_server(app, host='127.0.0.1', port=port, threads=1,
        trusted_proxy='127.0.0.1', trusted_proxy_headers={'x-forwarded-proto'},
        clear_untrusted_proxy_headers=True, max_request_body_size=16384,
        max_request_header_size=8192, channel_timeout=20, connection_limit=32,
        expose_tracebacks=False)
