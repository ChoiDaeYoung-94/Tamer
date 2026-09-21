"""WSGI factory with no listener or production adapters. JSON responses never log proofs."""
import json
from pathlib import Path
from .deletion import Rejected, SyntheticProvider

PUBLIC_CODES = frozenset(('reauthentication_required', 'invalid_request', 'policy_unavailable', 'policy_changed',
                          'request_already_exists', 'request_not_found', 'confirmation_expired', 'invalid_confirmation',
                          'already_submitted', 'provider_unconfirmed', 'unavailable', 'synthetic_account_required', 'operation_conflict',
                          'session_confirmation_required', 'account_type_unsupported', 'receipt_unavailable', 'receipt_rate_limited'))


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def create_app(service, synthetic_demo=False, session_confirmation=None):
    if synthetic_demo and not isinstance(service.provider, SyntheticProvider):
        raise ValueError('Synthetic provider required')
    if session_confirmation is not None and (synthetic_demo or service.core.authenticate != session_confirmation.authenticate
            or service.policy != session_confirmation.policy):
        raise ValueError('Matching session confirmation composition required')

    def app(environ, start_response):
        status, payload, content_type = '503 Service Unavailable', {'code': 'unavailable'}, 'application/json'
        try:
            method, path = environ.get('REQUEST_METHOD'), environ.get('PATH_INFO')
            if method == 'GET' and path == '/privacy/deletion':
                content_type = 'text/html; charset=utf-8'
                payload = (Path(__file__).with_name('demo.html').read_text(encoding='utf-8') if synthetic_demo else
                           '<!doctype html><html lang="ko"><meta charset="utf-8"><title>계정 삭제</title>'
                           '<h1>계정 및 데이터 삭제</h1><p>삭제 접수 서비스는 아직 제공되지 않습니다. 계정은 변경되지 않았습니다.</p></html>')
                status = '200 OK'
            elif method == 'GET' and path == '/v1/deletion/config':
                payload, status = {'available': service.policy.ready, 'synthetic': synthetic_demo,
                    'receiptRecovery': getattr(service, 'receipt_recovery', None) is not None,
                    'evidenceKind': 'session_confirmation' if session_confirmation else 'provider_reauthentication'}, '200 OK'
            elif method == 'POST' and path in ('/v1/deletion/request', '/v1/deletion/confirm', '/v1/deletion/status', '/v1/deletion/cancel', '/demo/advance',
                                              '/v1/deletion/session-challenge', '/v1/deletion/session-confirm',
                                              '/v1/deletion/receipt-register', '/v1/deletion/receipt-status', '/v1/deletion/receipt-ack'):
                length = int(environ.get('CONTENT_LENGTH') or 0)
                if not 0 < length <= 16384 or environ.get('CONTENT_TYPE', '').split(';')[0] != 'application/json':
                    raise ValueError()
                raw = environ['wsgi.input'].read(length)
                if len(raw) != length:
                    raise ValueError()
                body = json.loads(raw, object_pairs_hook=unique)
                required = {'proof', 'clientKey'} if path.endswith('/request') else {'proof', 'requestId'}
                if path.endswith('/confirm'):
                    required |= {'challenge', 'policyRevision'}
                if path == '/demo/advance':
                    required |= {'complete'}
                if path.endswith('/session-challenge'):
                    required = {'sessionTicket', 'clientKey'}
                if path.endswith('/session-confirm'):
                    required = {'sessionTicket', 'nonce', 'confirmed'}
                if path.endswith('/receipt-register'):
                    required = {'proof', 'requestId', 'verifier'}
                if path.endswith('/receipt-status') or path.endswith('/receipt-ack'):
                    required = {'requestId', 'capability'}
                if not isinstance(body, dict) or set(body) != required:
                    raise ValueError()
                if '/receipt-' in path:
                    recovery = getattr(service, 'receipt_recovery', None)
                    if recovery is None:
                        raise Rejected('receipt_unavailable')
                    payload = (recovery.register(body['proof'], body['requestId'], body['verifier']) if path.endswith('/receipt-register')
                        else recovery.read(body['requestId'], body['capability'], acknowledge=path.endswith('/receipt-ack')))
                elif path.endswith('/session-challenge') or path.endswith('/session-confirm'):
                    if session_confirmation is None:
                        raise Rejected('unavailable')
                    payload = (session_confirmation.begin(body['sessionTicket'], body['clientKey'])
                        if path.endswith('/session-challenge') else
                        session_confirmation.confirm(body['sessionTicket'], body['nonce'], body['confirmed']))
                elif path.endswith('/request'):
                    payload = service.request(body['proof'], body['clientKey'])
                elif path.endswith('/confirm'):
                    payload = service.confirm(body['proof'], body['requestId'], body['challenge'], body['policyRevision'])
                elif path.endswith('/status'):
                    payload = service.status(body['proof'], body['requestId'])
                elif path.endswith('/cancel'):
                    payload = service.cancel(body['proof'], body['requestId'])
                elif synthetic_demo:
                    service.status(body['proof'], body['requestId'])  # Verify ownership before touching the fake worker.
                    if type(body['complete']) is not bool:
                        raise ValueError()
                    if body['complete']:
                        service.provider.completed.add(body['requestId'])
                    payload = service.advance(body['requestId'])
                else:
                    raise Rejected('unavailable')
                status = '200 OK'
            else:
                status, payload = '404 Not Found', {'code': 'not_found'}
        except Rejected as error:
            status, payload = '409 Conflict', {'code': str(error) if str(error) in PUBLIC_CODES else 'unavailable'}
        except (ValueError, TypeError, KeyError, UnicodeError):
            status, payload = '400 Bad Request', {'code': 'invalid_request'}
        except Exception:
            # Upstream failures leave persisted processing state; never leak raw exceptions.
            pass
        encoded = (payload if isinstance(payload, str) else json.dumps(payload)).encode('utf-8')
        headers = [('Content-Type', content_type), ('Cache-Control', 'no-store'), ('X-Content-Type-Options', 'nosniff'),
                   ('Content-Security-Policy', "default-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; frame-ancestors 'none'"),
                   ('Content-Length', str(len(encoded)))]
        start_response(status, headers)
        return [encoded]
    return app
