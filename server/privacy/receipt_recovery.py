"""One-intent receipt reads. No PlayFab calls, deletion permissions, or completion observer."""
import hashlib
import math
import secrets
from urllib.parse import urlsplit
from .deletion import Rejected


def bound_hash(*values):
    raw = b''.join(str(len(v.encode('utf-8'))).encode('ascii') + b':' + v.encode('utf-8') for v in values)
    return hashlib.sha256(raw).hexdigest()


class ReceiptRecovery:
    def __init__(self, service, origin, ttl_seconds=30 * 86400):
        uri = urlsplit(origin)
        if (uri.scheme != 'https' or not uri.hostname or uri.username or uri.password or
                uri.path != '/' or uri.query or uri.fragment or not origin.isascii()):
            raise ValueError('Explicit HTTPS origin required')
        if type(ttl_seconds) not in (int, float) or not math.isfinite(ttl_seconds) or not 1 <= ttl_seconds <= 30 * 86400:
            raise ValueError('Finite receipt lifetime required')
        self.service, self.origin, self.ttl = service, origin, ttl_seconds
        with service.core.connection() as db:
            db.execute('CREATE TABLE IF NOT EXISTS deletion_receipt_access ('
                'request_id TEXT PRIMARY KEY, account TEXT NOT NULL, entity_id TEXT NOT NULL, '
                'client_key TEXT NOT NULL, owner_hash TEXT NOT NULL, binding TEXT NOT NULL, '
                'verifier TEXT NOT NULL, created REAL NOT NULL, expires REAL NOT NULL, '
                'acknowledged INTEGER NOT NULL DEFAULT 0, window_start REAL NOT NULL DEFAULT 0, '
                'read_count INTEGER NOT NULL DEFAULT 0)')

    @staticmethod
    def hex(value):
        if not isinstance(value, str) or len(value) != 64 or any(c not in '0123456789abcdef' for c in value):
            raise Rejected('invalid_request')
        return value

    def register(self, proof, request_id, verifier):
        self.service.core.ready()
        self.hex(verifier)
        principal = self.service.principal(proof)
        with self.service.core.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            request = self.service.core.owned(db, request_id, principal.account, principal)
            old = db.execute('SELECT * FROM deletion_receipt_access WHERE request_id=?', (request_id,)).fetchone()
            owner = bound_hash(self.origin, self.service.provider.title_id, principal.account)
            binding = bound_hash(self.origin, self.service.provider.title_id, principal.account, principal.title_entity_id)
            if old:
                if (old['account'] != principal.account or old['entity_id'] != principal.title_entity_id or
                        old['client_key'] != request['client_key'] or old['owner_hash'] != owner or old['binding'] != binding or
                        not secrets.compare_digest(old['verifier'], verifier)):
                    raise Rejected('operation_conflict')
                self.live(old)
                return self.view(db, old, request)
            if request['state'] != 'awaiting_confirmation':
                raise Rejected('already_submitted')
            now = self.service.core.clock()
            db.execute('INSERT INTO deletion_receipt_access '
                '(request_id,account,entity_id,client_key,owner_hash,binding,verifier,created,expires) VALUES (?,?,?,?,?,?,?,?,?)',
                (request_id, principal.account, principal.title_entity_id, request['client_key'], owner, binding, verifier, now, now + self.ttl))
            row = db.execute('SELECT * FROM deletion_receipt_access WHERE request_id=?', (request_id,)).fetchone()
            return self.view(db, row, request)

    def live(self, row):
        if row is None or row['acknowledged'] or not row['created'] <= self.service.core.clock() < row['expires']:
            raise Rejected('receipt_unavailable')

    def require_registered(self, request_id, principal):
        with self.service.core.connection() as db:
            row = db.execute('SELECT * FROM deletion_receipt_access WHERE request_id=?', (request_id,)).fetchone()
            self.live(row)
            if row['account'] != principal.account or row['entity_id'] != principal.title_entity_id or (
                    principal.intent_key and principal.intent_key != row['client_key']):
                raise Rejected('operation_conflict')

    def view(self, db, row, request):
        if request['account'] != row['account'] or request['client_key'] != row['client_key'] or request['scope'] != 'title':
            raise Rejected('operation_conflict')
        operation = db.execute('SELECT * FROM title_deletion_operations WHERE id=?', (row['request_id'],)).fetchone()
        if operation and (operation['account'] != row['account'] or operation['entity_id'] != row['entity_id']):
            raise Rejected('operation_conflict')
        state = request['state']
        phase = operation['phase'] if operation else 'not_submitted'
        if state == 'accepted' and phase == 'accepted':
            state = 'processing'
        if state == 'cancelled' and phase == 'ready':
            phase = 'not_submitted' # Bound but never submitted; terminal cancellation remains readable.
        if state not in ('awaiting_confirmation', 'queued', 'processing', 'cancelled'):
            raise Rejected('receipt_unavailable')
        return dict(requestId=row['request_id'], clientKey=row['client_key'], ownerHash=row['owner_hash'],
            binding=row['binding'], expiresAt=row['expires'], policyRevision=request['revision'], scope='title',
            state=state, submissionState=phase)

    def read(self, request_id, capability, acknowledge=False):
        # Authentication deliberately does not require policy readiness, a fresh ticket, or a live PlayFab account.
        self.hex(capability)
        verifier = hashlib.sha256(capability.encode('ascii')).hexdigest()
        with self.service.core.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM deletion_receipt_access WHERE request_id=?', (request_id,)).fetchone()
            if row is None or not secrets.compare_digest(row['verifier'], verifier):
                raise Rejected('receipt_unavailable')
            now = self.service.core.clock()
            if not row['created'] <= now < row['expires']:
                raise Rejected('receipt_unavailable')
            if row['acknowledged']:
                if acknowledge:
                    return {'acknowledged': True} # Lost ack response may be retried with the same capability.
                raise Rejected('receipt_unavailable')
            request = db.execute('SELECT * FROM deletion_requests WHERE id=?', (request_id,)).fetchone()
            snapshot = self.view(db, row, request)
            if acknowledge:
                if snapshot['submissionState'] != 'accepted' and snapshot['state'] != 'cancelled':
                    raise Rejected('receipt_unavailable')
                db.execute('UPDATE deletion_receipt_access SET acknowledged=1 WHERE request_id=?', (request_id,))
                return {'acknowledged': True}
            if now - row['window_start'] < 60 and row['read_count'] >= 6:
                raise Rejected('receipt_rate_limited')
            if now - row['window_start'] >= 60:
                db.execute('UPDATE deletion_receipt_access SET window_start=?,read_count=1 WHERE request_id=?', (now, request_id))
            else:
                db.execute('UPDATE deletion_receipt_access SET read_count=read_count+1 WHERE request_id=?', (request_id,))
            return snapshot
