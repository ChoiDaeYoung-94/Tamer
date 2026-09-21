"""Durable title-only provider boundary; no credentials, network or default composition.

Callbacks belong to a trusted server adapter. They must never be populated from
client JSON. A queued PlayFab response is not proof of deletion completion.
"""
from contextlib import contextmanager
from dataclasses import dataclass
import hashlib
import json
import sqlite3

from .deletion import DeletionService, ProviderResult, Rejected


@dataclass(frozen=True)
class DeletionTarget:
    title_id: str
    account: str
    title_entity_id: str


@dataclass(frozen=True)
class CompletionProof:
    request_id: str
    target: DeletionTarget
    component: str
    evidence: str


class SubmissionNotAccepted(Exception):
    """Only a definitive pre-acceptance rejection; never use for a timeout."""


class ServerDeletePlayerSubmission:
    """Injected server transport only; never reads a key or accepts a client route."""
    def __init__(self, title_id, invoke):
        if not isinstance(title_id, str) or not title_id.strip() or not callable(invoke):
            raise ValueError('A pinned title and trusted transport are required')
        self.title_id, self.invoke = title_id, invoke

    def __call__(self, target):
        if not isinstance(target, DeletionTarget) or target.title_id != self.title_id:
            raise SubmissionNotAccepted()
        response = self.invoke(self.title_id, '/Server/DeletePlayer', {'PlayFabId': target.account})
        if isinstance(response, dict) and response.get('error') == 'APINotEnabledForGameServerAccess':
            raise SubmissionNotAccepted()
        return (isinstance(response, dict) and type(response.get('code')) is int
                and response['code'] == 200 and response.get('status') == 'OK'
                and isinstance(response.get('data'), dict) and not response.get('error'))


class TitleDeletionProvider:
    """bind_confirmed is worker composition only, after fresh auth + confirmation.

    submit(target) must call only Server/DeletePlayer for the pinned title, using
    target.account as PlayFabId. It returns exactly True only for queue acceptance.
    verify_target(target) checks the authenticated title/account/entity binding and
    the deletion fence. reconcile_component must be idempotent for request_id;
    it returns None while pending or an independently verified CompletionProof.
    No lookup absence, elapsed timer or HTTP 200 may stand in for that proof.
    """
    def __init__(self, database, title_id, submit, verify_target, components):
        if not isinstance(title_id, str) or not title_id.strip():
            raise ValueError('A pinned title is required')
        if not callable(submit) or not callable(verify_target):
            raise ValueError('Trusted server adapters are required')
        if (not isinstance(components, dict) or 'title_player' not in components
                or 'tamer_owned_data' not in components
                or any(not isinstance(k, str) or not k or not callable(v) for k, v in components.items())):
            raise ValueError('Explicit title and owned-data completion adapters are required')
        self.database, self.title_id = str(database), title_id
        self.submit, self.verify_target = submit, verify_target
        self.components = dict(components)
        with self.connection() as db:
            db.execute('CREATE TABLE IF NOT EXISTS title_deletion_operations ('
                       'id TEXT PRIMARY KEY, title_id TEXT NOT NULL, account TEXT NOT NULL, '
                       'entity_id TEXT NOT NULL, policy_hash TEXT NOT NULL, components TEXT NOT NULL, '
                       'phase TEXT NOT NULL, evidence TEXT NOT NULL)')

    @contextmanager
    def connection(self):
        db = sqlite3.connect(self.database, timeout=5)
        db.row_factory = sqlite3.Row
        try:
            with db:
                yield db
        finally:
            db.close()

    @staticmethod
    def policy_hash(policy):
        if not policy.ready or policy.scope != 'title':
            raise Rejected('policy_unavailable')
        return hashlib.sha256(json.dumps([policy.revision, policy.scope, policy.retention_plan],
                                        sort_keys=True).encode()).hexdigest()

    def bind_confirmed(self, request_id, target, policy):
        """Not an HTTP endpoint. Persist trusted identity before any destructive work."""
        DeletionService.key(request_id)
        policy_hash = self.policy_hash(policy)
        if (not isinstance(target, DeletionTarget) or target.title_id != self.title_id
                or not isinstance(target.account, str) or not target.account.strip()
                or not isinstance(target.title_entity_id, str) or not target.title_entity_id.strip()):
            raise Rejected('operation_conflict')
        component_names = json.dumps(sorted(self.components))
        expected = (target.title_id, target.account, target.title_entity_id, policy_hash, component_names)
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            request = db.execute('SELECT * FROM deletion_requests WHERE id=?', (request_id,)).fetchone()
            if (request is None or request['account'] != target.account or request['scope'] != 'title'
                    or request['revision'] != policy.revision or request['state'] not in ('queued', 'processing')):
                raise Rejected('operation_conflict')
            row = db.execute('SELECT * FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()
            if row:
                if tuple(row[k] for k in ('title_id', 'account', 'entity_id', 'policy_hash', 'components')) != expected:
                    raise Rejected('operation_conflict')
                return
            db.execute('INSERT INTO title_deletion_operations VALUES (?,?,?,?,?,?,?,?)',
                       (request_id, *expected, 'ready', '{}'))

    def reconcile(self, request_id, account, policy):
        DeletionService.key(request_id)
        policy_hash = self.policy_hash(policy)
        with self.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()
            if (row is None or row['account'] != account or row['title_id'] != self.title_id
                    or row['policy_hash'] != policy_hash or row['components'] != json.dumps(sorted(self.components))):
                raise Rejected('operation_conflict')
            target = DeletionTarget(row['title_id'], row['account'], row['entity_id'])
            phase = row['phase']
            if phase == 'ready':
                # Persist uncertainty BEFORE invoking a non-idempotency-key API.
                db.execute("UPDATE title_deletion_operations SET phase='submission_unknown' WHERE id=?", (request_id,))
        if phase == 'ready':
            try:
                if self.verify_target(target) is not True:
                    raise SubmissionNotAccepted()
                accepted = self.submit(target)
            except SubmissionNotAccepted:
                with self.connection() as db:
                    db.execute("UPDATE title_deletion_operations SET phase='ready' WHERE id=? AND phase='submission_unknown'", (request_id,))
                raise Rejected('provider_unconfirmed') from None
            except Exception:
                # Reconcile evidence on the next worker run, never blindly delete again.
                raise Rejected('provider_unconfirmed') from None
            if accepted is not True:
                raise Rejected('provider_unconfirmed')
            with self.connection() as db:
                db.execute("UPDATE title_deletion_operations SET phase='accepted' WHERE id=? AND phase='submission_unknown'", (request_id,))

        for name, reconcile_component in self.components.items():
            with self.connection() as db:
                evidence = json.loads(db.execute('SELECT evidence FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()[0])
            if name in evidence:
                continue
            proof = reconcile_component(request_id, target, policy)
            if proof is None:
                continue
            if (not isinstance(proof, CompletionProof) or proof.request_id != request_id
                    or proof.target != target or proof.component != name
                    or not isinstance(proof.evidence, str) or not 1 <= len(proof.evidence) <= 8192):
                raise Rejected('provider_unconfirmed')
            with self.connection() as db:
                db.execute('BEGIN IMMEDIATE')
                evidence = json.loads(db.execute('SELECT evidence FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()[0])
                # Preserve prior receipts when another worker completes a different component.
                evidence.setdefault(name, proof.evidence)
                db.execute('UPDATE title_deletion_operations SET evidence=? WHERE id=?',
                           (json.dumps(evidence, sort_keys=True), request_id))
        with self.connection() as db:
            evidence = json.loads(db.execute('SELECT evidence FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()[0])
        if set(evidence) != set(self.components):
            return ProviderResult('pending')
        # The client gets no raw provider receipt or account identifier.
        digest = hashlib.sha256(json.dumps([request_id, policy_hash, evidence], sort_keys=True).encode()).hexdigest()
        return ProviderResult('completed', 'verified-title-components:' + digest)
