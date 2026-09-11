"""Standalone staging composition. No credentials or sockets on import."""
import logging
import threading
from urllib.parse import urlsplit

from . import database
from .http_app import create_app
from .upstream import Upstream
from .verification import Ledger, Verifier


class SafeLogFilter(logging.Filter):
    def filter(self, record):
        record.msg, record.args = 'receipt_service_event', ()
        record.exc_info = record.exc_text = record.stack_info = None
        return True


def safe_logging():
    handler = logging.StreamHandler()
    handler.addFilter(SafeLogFilter())
    logging.basicConfig(handlers=[handler], level=logging.WARNING, force=True)


class GoogleToken:
    def __init__(self, credential_file):
        from google.oauth2 import service_account
        from google.auth.transport.requests import Request
        self.credentials = service_account.Credentials.from_service_account_file(str(credential_file),
            scopes=['https://www.googleapis.com/auth/androidpublisher'])
        self.transport = Request()
        self.lock = threading.Lock()

    def __call__(self):
        with self.lock:
            if not self.credentials.valid:
                def bounded_request(*args, **kwargs):
                    kwargs['timeout'] = 10
                    return self.transport(*args, **kwargs)
                self.credentials.refresh(bounded_request)
            return self.credentials.token


def application(settings, verifier, readiness):
    core = create_app(verifier)
    expected_host = urlsplit(settings.origin).netloc.lower()
    def app(environ, start_response):
        path = environ.get('PATH_INFO')
        if path in ('/health/live', '/health/ready') and environ.get('REQUEST_METHOD') == 'GET':
            healthy = path == '/health/live' or readiness()
            body = b'{"ready":true}' if healthy else b'{"ready":false}'
            start_response('200 OK' if healthy else '503 Service Unavailable',
                [('Content-Type', 'application/json'), ('Cache-Control', 'no-store'), ('Content-Length', str(len(body)))])
            return [body]
        if environ.get('wsgi.url_scheme') != 'https' or environ.get('HTTP_HOST', '').lower() != expected_host:
            start_response('403 Forbidden', [('Content-Type', 'application/json'), ('Content-Length', '18')])
            return [b'{"verified":false}']
        if not readiness():
            start_response('503 Service Unavailable', [('Content-Type', 'application/json'), ('Content-Length', '18')])
            return [b'{"verified":false}']
        return core(environ, start_response)
    return app


def build(settings):
    if not database.ready(settings.database): raise ValueError('database_migration_required')
    secret = settings.secret_file.read_text(encoding='utf-8').strip()
    if not 1 <= len(secret) <= 4096: raise ValueError('invalid_server_secret')
    upstream = Upstream(settings.title, secret, GoogleToken(settings.google_file))
    verifier = Verifier(upstream.authenticate, upstream.get_purchase, Ledger(settings.database),
                        settings.package, settings.accounts)
    # Readiness establishes local configuration/storage only, never claims valid
    # provider permissions or performs a synthetic call against a real account.
    return application(settings, verifier, lambda: database.ready(settings.database))


def make_server(app, port):
    from waitress.server import create_server
    return create_server(app, host='127.0.0.1', port=port, threads=2,
        trusted_proxy='127.0.0.1', trusted_proxy_headers={'x-forwarded-proto'},
        clear_untrusted_proxy_headers=True, max_request_body_size=65536,
        max_request_header_size=8192, channel_timeout=20, connection_limit=32,
        expose_tracebacks=False)
