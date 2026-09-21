"""Deletion orchestration only. No PlayFab adapter, credentials or network access.

The injected provider must reconcile by durable request ID idempotently. A provider
receipt is not completion. Default policy disables all requests and execution.
"""
from dataclasses import dataclass
from contextlib import contextmanager
import hashlib
import math
import secrets
import sqlite3
import time
import uuid


class Rejected(Exception):
    pass


@dataclass(frozen=True)
class Policy:
    revision: str = ''
    scope: str = ''
    retention_approved: bool = False
    rejoin_approved: bool = False
    enabled: bool = False
    retention_plan: tuple = ()
    session_confirmation_enabled: bool = False

    @property
    def ready(self):
        return (self.enabled and bool(self.revision) and self.scope in ('title', 'master')
                and self.retention_approved and self.rejoin_approved and bool(self.retention_plan))


@dataclass(frozen=True)
class Principal:
    account: str
    reauthenticated_at: float | None
    title_entity_id: str = ''
    evidence_kind: str = 'provider_reauthentication'
    confirmed_at: float | None = None
    intent_key: str = ''


@dataclass(frozen=True)
class ProviderResult:
    state: str
    completion_evidence: str = ''


class DeletionService:
    def __init__(self, database, authenticate, provider, policy=None, clock=time.time):
        self.database = str(database)
        self.authenticate = authenticate
        self.provider = provider
        self.policy = policy or Policy()
        self.clock = clock
        with self.connection() as db:
            db.execute('CREATE TABLE IF NOT EXISTS deletion_requests ('
                       'id TEXT PRIMARY KEY, account TEXT NOT NULL, client_key TEXT NOT NULL, '
                       'revision TEXT NOT NULL, scope TEXT NOT NULL, state TEXT NOT NULL, '
                       'challenge_hash TEXT NOT NULL, expires REAL NOT NULL, completion_evidence TEXT NOT NULL, '
                       'UNIQUE(account, client_key))')

    @contextmanager
    def connection(self):
        db = sqlite3.connect(self.database, timeout=5)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    def principal(self, proof):
        if not isinstance(proof, str) or not 1 <= len(proof) <= 8192:
            raise Rejected('reauthentication_required')
        principal = self.authenticate(proof)
        if not isinstance(principal, Principal) or not principal.account:
            raise Rejected('reauthentication_required')
        if principal.evidence_kind == 'provider_reauthentication' and principal.confirmed_at is None:
            observed_at = principal.reauthenticated_at
        elif (principal.evidence_kind == 'session_confirmation' and self.policy.session_confirmation_enabled
                and principal.reauthenticated_at is None and principal.intent_key):
            self.key(principal.intent_key)
            observed_at = principal.confirmed_at
        else:
            raise Rejected('reauthentication_required')
        if type(observed_at) not in (int, float) or not math.isfinite(observed_at):
            raise Rejected('reauthentication_required')
        age = self.clock() - observed_at
        if not 0 <= age <= 300:
            raise Rejected('reauthentication_required')
        return principal

    @staticmethod
    def key(value):
        if not isinstance(value, str) or not 16 <= len(value) <= 128 or not value.isascii() or not value.replace('-', '').isalnum():
            raise Rejected('invalid_request')
        return value

    def ready(self):
        if not self.policy.ready:
            raise Rejected('policy_unavailable')

    @staticmethod
    def view(row):
        return {'requestId': row['id'], 'state': row['state'],
                'policyRevision': row['revision'], 'scope': row['scope'],
                'completionEvidence': row['completion_evidence']}

    def request(self, proof, client_key):
        self.ready()
        principal = self.principal(proof)
        account = principal.account
        self.key(client_key)
        if principal.intent_key and principal.intent_key != client_key:
            raise Rejected('operation_conflict')
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            old = db.execute('SELECT * FROM deletion_requests WHERE account=? AND client_key=?',
                             (account, client_key)).fetchone()
            if old is None:
                # A reopened app/web flow has no persisted client key. Freshly
                # authenticated owners resume their one outstanding request.
                old = db.execute("SELECT * FROM deletion_requests WHERE account=? AND state NOT IN ('cancelled','completed','accepted')", (account,)).fetchone()
                if old:
                    if principal.intent_key and principal.intent_key != old['client_key']:
                        raise Rejected('operation_conflict')
                    client_key = old['client_key']
            if old:
                # A lost initial response may ask for a new challenge for the same request.
                if old['state'] != 'awaiting_confirmation':
                    return self.view(old)
                if old['revision'] != self.policy.revision or old['scope'] != self.policy.scope:
                    raise Rejected('policy_changed')
                request_id = old['id']
            else:
                request_id = uuid.uuid4().hex
            challenge = secrets.token_urlsafe(32)
            digest = hashlib.sha256(challenge.encode()).hexdigest()
            db.execute('INSERT INTO deletion_requests VALUES (?,?,?,?,?,?,?,?,?) '
                       'ON CONFLICT(account,client_key) DO UPDATE SET challenge_hash=excluded.challenge_hash, expires=excluded.expires',
                       (request_id, account, client_key, self.policy.revision, self.policy.scope,
                        'awaiting_confirmation', digest, self.clock() + 300, ''))
            row = db.execute('SELECT * FROM deletion_requests WHERE id=?', (request_id,)).fetchone()
            return dict(self.view(row), challenge=challenge)

    def owned(self, db, request_id, account, principal=None):
        self.key(request_id)
        row = db.execute('SELECT * FROM deletion_requests WHERE id=? AND account=?', (request_id, account)).fetchone()
        if row is None:
            raise Rejected('request_not_found')
        if principal is not None and principal.intent_key and principal.intent_key != row['client_key']:
            raise Rejected('operation_conflict')
        return row

    def confirm(self, proof, request_id, challenge, revision):
        self.ready()
        principal = self.principal(proof)
        account = principal.account
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = self.owned(db, request_id, account, principal)
            if row['revision'] != revision or revision != self.policy.revision or row['scope'] != self.policy.scope:
                raise Rejected('policy_changed')
            if row['state'] in ('queued', 'processing', 'completed', 'accepted'):
                return self.view(row)
            if row['state'] != 'awaiting_confirmation' or row['expires'] < self.clock():
                raise Rejected('confirmation_expired')
            if not isinstance(challenge, str) or len(challenge) > 128 or not secrets.compare_digest(
                    hashlib.sha256(challenge.encode()).hexdigest(), row['challenge_hash']):
                raise Rejected('invalid_confirmation')
            db.execute("UPDATE deletion_requests SET state='queued', challenge_hash='' WHERE id=?", (request_id,))
            return dict(self.view(row), state='queued')

    def status(self, proof, request_id):
        principal = self.principal(proof)
        account = principal.account
        with self.connection() as db:
            return self.view(self.owned(db, request_id, account, principal))

    def cancel(self, proof, request_id):
        principal = self.principal(proof)
        account = principal.account
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = self.owned(db, request_id, account, principal)
            if row['state'] not in ('awaiting_confirmation', 'queued', 'cancelled'):
                raise Rejected('already_submitted')
            db.execute("UPDATE deletion_requests SET state='cancelled', challenge_hash='' WHERE id=?", (request_id,))
            return dict(self.view(row), state='cancelled')

    def advance(self, request_id):
        """Worker-only, never exposed by HTTP. Retry reconciles the same operation."""
        self.ready()
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM deletion_requests WHERE id=?', (self.key(request_id),)).fetchone()
            if row is None:
                raise Rejected('request_not_found')
            if row['state'] not in ('queued', 'processing'):
                return self.view(row)
            if row['revision'] != self.policy.revision or row['scope'] != self.policy.scope:
                raise Rejected('policy_changed')
            db.execute("UPDATE deletion_requests SET state='processing' WHERE id=?", (request_id,))
        # Persist processing before external work. Response loss remains processing,
        # not success or permission to submit a different operation.
        outcome = self.provider.reconcile(request_id, row['account'], self.policy)
        if not isinstance(outcome, ProviderResult) or outcome.state not in ('pending', 'completed') or (
                outcome.state == 'completed' and not outcome.completion_evidence):
            raise Rejected('provider_unconfirmed')
        with self.connection() as db:
            if outcome.state == 'completed':
                db.execute("UPDATE deletion_requests SET state='completed', completion_evidence=? WHERE id=? AND state='processing'",
                           (outcome.completion_evidence, request_id))
            return self.view(db.execute('SELECT * FROM deletion_requests WHERE id=?', (request_id,)).fetchone())


class SyntheticProvider:
    """In-memory, explicitly synthetic deletion; never touches player files/accounts."""
    def __init__(self):
        self.operations = {}
        self.completed = set()

    def reconcile(self, request_id, account, policy):
        if not account.startswith('synthetic-'):
            raise Rejected('synthetic_account_required')
        operation = (account, policy.revision, policy.scope)
        if self.operations.setdefault(request_id, operation) != operation:
            raise Rejected('operation_conflict')
        return ProviderResult('completed', 'synthetic-completion-' + request_id) if request_id in self.completed else ProviderResult('pending')
