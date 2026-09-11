"""WSGI entry point factory; no listener, credentials or deployment on import.

Mount behind TLS and authenticated/rate-limited ingress. Disable request-body,
Authorization and upstream URL logging at every layer before deployment.
"""
import json
from .verification import Rejected


def create_app(verifier):
    def app(environ, start_response):
        status, result = '503 Service Unavailable', {'verified': False, 'code': 'unavailable'}
        try:
            if environ.get('REQUEST_METHOD') != 'POST' or environ.get('PATH_INFO') != '/v1/no-ads/verify':
                status, result = '404 Not Found', {'verified': False, 'code': 'not_found'}
            else:
                length = int(environ.get('CONTENT_LENGTH') or 0)
                if not 0 < length <= 65536 or environ.get('CONTENT_TYPE', '').split(';')[0] != 'application/json':
                    raise Rejected('invalid_request')
                body = json.loads(environ['wsgi.input'].read(length))
                result = verifier.verify(body)
                status = '200 OK'
        except Rejected as error:
            status, result = '403 Forbidden', {'verified': False, 'code': str(error)}
        except (ValueError, TypeError, KeyError):
            status, result = '400 Bad Request', {'verified': False, 'code': 'invalid_request'}
        except Exception:
            # No upstream error bodies, URLs containing tokens or tracebacks.
            pass
        encoded = json.dumps(result).encode()
        start_response(status, [('Content-Type', 'application/json'),
                               ('Cache-Control', 'no-store'), ('Content-Length', str(len(encoded)))])
        return [encoded]
    return app
