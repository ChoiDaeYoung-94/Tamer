"""Durable confirmation of an authenticated anonymous session, not fresh provider auth.

SQLite is for a host with a durable local database. This is not an Azure Table
adapter and must not use an ephemeral Functions filesystem. No listener/defaults.
"""
from contextlib import contextmanager
import hashlib
import json
import secrets
import sqlite3
import time

from .deletion import DeletionService, Policy, Principal, Rejected
from .intake import IntakeService
from .playfab_session import PlayFabSession, VerifiedSession
from .title_deletion_provider import ServerDeletePlayerSubmission


def digest(value):
    return hashlib.sha256(value.encode()).hexdigest()


def unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


class SessionConfirmation:
    PURPOSE = 'delete_title_account'
    NONCE_LIFETIME = 120
    PROOF_LIFETIME = 300

    def __init__(self, database, upstream, policy=None, clock=time.time):
        self.database, self.upstream = str(database), upstream
        self.policy, self.clock = policy or Policy(), clock
        with self.connection() as db:
            db.execute('CREATE TABLE IF NOT EXISTS deletion_session_nonces ('
                'nonce_hash TEXT PRIMARY KEY, title_id TEXT NOT NULL, account TEXT NOT NULL, entity_id TEXT NOT NULL, '
                'session_hash TEXT NOT NULL, account_type TEXT NOT NULL, intent_key TEXT NOT NULL, '
                'purpose TEXT NOT NULL, policy_hash TEXT NOT NULL, created REAL NOT NULL, expires REAL NOT NULL, '
                'consumed INTEGER NOT NULL DEFAULT 0)')
            db.execute('CREATE TABLE IF NOT EXISTS deletion_session_proofs ('
                'proof_hash TEXT PRIMARY KEY, nonce_hash TEXT UNIQUE NOT NULL, title_id TEXT NOT NULL, '
                'account TEXT NOT NULL, entity_id TEXT NOT NULL, session_hash TEXT NOT NULL, '
                'intent_key TEXT NOT NULL, policy_hash TEXT NOT NULL, confirmed REAL NOT NULL, expires REAL NOT NULL)')

    @contextmanager
    def connection(self):
        db = sqlite3.connect(self.database, timeout=5)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    def ready(self):
        if not self.policy.ready or self.policy.scope != 'title' or not self.policy.session_confirmation_enabled:
            raise Rejected('policy_unavailable')

    def policy_hash(self):
        return digest(json.dumps([self.policy.revision, self.policy.scope, self.policy.retention_plan,
                                  self.policy.session_confirmation_enabled, self.PURPOSE], sort_keys=True))

    @staticmethod
    def ticket(value):
        if not isinstance(value, str) or not 1 <= len(value) <= 4096:
            raise Rejected('session_confirmation_required')
        return value

    @staticmethod
    def token(value):
        if not isinstance(value, str) or len(value) != 43 or not value.replace('-', '').replace('_', '').isalnum() or not value.isascii():
            raise Rejected('session_confirmation_required')
        return value

    def verified(self, ticket):
        principal = self.upstream.authenticate(self.ticket(ticket))
        if (not isinstance(principal, VerifiedSession) or principal.title_id != self.upstream.title_id
                or principal.account_type not in ('android_device', 'custom_id')):
            raise Rejected('account_type_unsupported')
        return principal

    def begin(self, session_ticket, client_key):
        self.ready()
        DeletionService.key(client_key)
        verified = self.verified(session_ticket)
        now = self.clock()
        nonce = secrets.token_urlsafe(32)
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            # Limit outstanding intents for one session; do not grow a nonce backlog on retries.
            db.execute('DELETE FROM deletion_session_nonces WHERE expires < ?', (now,))
            db.execute('DELETE FROM deletion_session_proofs WHERE expires < ?', (now,))
            db.execute('UPDATE deletion_session_nonces SET consumed=1 WHERE session_hash=? AND consumed=0',
                       (digest(session_ticket),))
            db.execute('INSERT INTO deletion_session_nonces VALUES (?,?,?,?,?,?,?,?,?,?,?,0)',
                (digest(nonce), verified.title_id, verified.account, verified.entity_id, digest(session_ticket),
                 verified.account_type, client_key, self.PURPOSE, self.policy_hash(), now, now + self.NONCE_LIFETIME))
        return {'nonce': nonce, 'expiresAt': now + self.NONCE_LIFETIME,
                'evidenceKind': 'session_confirmation', 'purpose': self.PURPOSE}

    def confirm(self, session_ticket, nonce, confirmed):
        self.ready()
        self.ticket(session_ticket)
        self.token(nonce)
        if confirmed is not True:
            raise Rejected('invalid_confirmation')
        # Cheap local validation before any provider lookup; no request body carries account/type/time.
        with self.connection() as db:
            row = db.execute('SELECT * FROM deletion_session_nonces WHERE nonce_hash=?', (digest(nonce),)).fetchone()
        self.validate_nonce(row, session_ticket)
        verified = self.verified(session_ticket)
        if (verified.account, verified.entity_id, verified.account_type) != (row['account'], row['entity_id'], row['account_type']):
            raise Rejected('operation_conflict')
        grant = secrets.token_urlsafe(32)
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM deletion_session_nonces WHERE nonce_hash=?', (digest(nonce),)).fetchone()
            self.validate_nonce(row, session_ticket)  # Repeat after I/O and while holding the write lock.
            now = self.clock()
            db.execute('UPDATE deletion_session_nonces SET consumed=1 WHERE nonce_hash=?', (digest(nonce),))
            db.execute('INSERT INTO deletion_session_proofs VALUES (?,?,?,?,?,?,?,?,?,?)',
                (digest(grant), digest(nonce), row['title_id'], row['account'], row['entity_id'], row['session_hash'],
                 row['intent_key'], row['policy_hash'], now, now + self.PROOF_LIFETIME))
        # The ticket is already owned by this caller; it is never stored in the database.
        proof = json.dumps({'grant': grant, 'sessionTicket': session_ticket}, separators=(',', ':'))
        return {'accountId': verified.account, 'proof': proof, 'evidenceKind': 'session_confirmation',
                'expiresAt': now + self.PROOF_LIFETIME}

    def validate_nonce(self, row, ticket):
        now = self.clock()
        if (row is None or row['consumed'] != 0 or not row['created'] <= now < row['expires']
                or row['title_id'] != self.upstream.title_id or row['purpose'] != self.PURPOSE
                or row['policy_hash'] != self.policy_hash()
                or not secrets.compare_digest(row['session_hash'], digest(ticket))):
            raise Rejected('session_confirmation_required')

    def authenticate(self, proof):
        self.ready()
        try:
            if not isinstance(proof, str) or not 1 <= len(proof) <= 8192:
                raise ValueError()
            body = json.loads(proof, object_pairs_hook=unique)
            if not isinstance(body, dict) or set(body) != {'grant', 'sessionTicket'}:
                raise ValueError()
            grant, ticket = self.token(body['grant']), self.ticket(body['sessionTicket'])
        except (ValueError, TypeError, KeyError):
            raise Rejected('session_confirmation_required') from None
        with self.connection() as db:
            row = db.execute('SELECT * FROM deletion_session_proofs WHERE proof_hash=?', (digest(grant),)).fetchone()
        now = self.clock()
        if (row is None or row['title_id'] != self.upstream.title_id or row['policy_hash'] != self.policy_hash()
                or not row['confirmed'] <= now < row['expires']
                or not secrets.compare_digest(row['session_hash'], digest(ticket))):
            raise Rejected('session_confirmation_required')
        # Bounded capability for this one intent. No claim that a provider reauthenticated now.
        # It remains usable for accepted/unknown status after PlayFab invalidates the ticket.
        return Principal(row['account'], None, row['entity_id'], 'session_confirmation', row['confirmed'], row['intent_key'])


def compose(database, title_id, secret_key, policy=None, clock=time.time, request=None, recovery_origin=None, recovery_ttl_seconds=30 * 86400):
    """Explicit composition only. The caller supplies private credentials and enabled policy."""
    upstream = PlayFabSession(title_id, secret_key, **({} if request is None else {'request': request}))
    effective_policy = policy if policy is not None and policy.session_confirmation_enabled else Policy()
    confirmation = SessionConfirmation(database, upstream, effective_policy, clock)
    service = IntakeService(database, title_id, confirmation.authenticate, upstream.resolve_target,
        ServerDeletePlayerSubmission(title_id, upstream.invoke), upstream.verify_target, confirmation.policy, clock)
    if recovery_origin is not None:
        from .receipt_recovery import ReceiptRecovery
        service.receipt_recovery = ReceiptRecovery(service, recovery_origin, recovery_ttl_seconds)
    return service, confirmation
