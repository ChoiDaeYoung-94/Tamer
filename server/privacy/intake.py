"""Opt-in HTTP composition over the existing SQLite core; no listener or credentials.

authenticate must validate an independently issued reauthentication proof and return
Principal with the server-verified authentication time. Cached session validity and
client timestamps are not fresh authentication. No default verifier is supplied.
"""
from .deletion import DeletionService, Policy, Rejected
from .title_deletion_provider import DeletionTarget, TitleDeletionProvider


class IntakeService:
    def __init__(self, database, title_id, authenticate=None, resolve_target=None,
                 submit=None, verify_target=None, policy=None, clock=None):
        configured = all(callable(x) for x in (authenticate, resolve_target, submit, verify_target))
        self.policy = policy if configured and policy is not None else Policy()
        if self.policy.scope and self.policy.scope != 'title':
            raise ValueError('Title-only intake required')
        self.resolve_target = resolve_target
        self.provider = (TitleDeletionProvider(database, title_id, submit, verify_target)
                         if configured else None)
        kwargs = {} if clock is None else {'clock': clock}
        self.core = DeletionService(database, authenticate or self.unavailable,
                                    self.provider, self.policy, **kwargs)

    @staticmethod
    def unavailable(*_):
        raise Rejected('unavailable')

    def principal(self, proof):
        principal = self.core.principal(proof)
        if not isinstance(principal.title_entity_id, str) or not principal.title_entity_id:
            raise Rejected('reauthentication_required')
        return principal

    def project(self, snapshot, principal):
        # Pure read: status/request retries may never trigger a destructive call.
        with self.core.connection() as db:
            row = (db.execute('SELECT phase,entity_id FROM title_deletion_operations WHERE id=? AND account=?',
                              (snapshot['requestId'], principal.account)).fetchone() if self.provider else None)
        if row and row['entity_id'] != principal.title_entity_id:
            raise Rejected('operation_conflict')
        phase = row['phase'] if row else 'not_submitted'
        return dict(snapshot, submissionState=phase)

    def request(self, proof, client_key):
        self.core.ready()
        principal = self.principal(proof)
        account = principal.account
        # A recreated title entity must not inherit an old acceptance and wipe its new progress.
        target = self.resolve_target(account)
        if (not isinstance(target, DeletionTarget) or target.account != account or target.title_id != self.provider.title_id
                or target.title_entity_id != principal.title_entity_id):
            raise Rejected('operation_conflict')
        with self.core.connection() as db:
            db.execute('BEGIN IMMEDIATE')
            prior = db.execute("SELECT r.id, o.entity_id, o.phase FROM deletion_requests r "
                "JOIN title_deletion_operations o ON o.id=r.id WHERE r.account=? AND r.state='processing'",
                (account,)).fetchall()
            for row in prior:
                if row['entity_id'] != target.title_entity_id:
                    if row['phase'] != 'accepted':
                        raise Rejected('operation_conflict')
                    db.execute("UPDATE deletion_requests SET state='accepted' WHERE id=?", (row['id'],))
        return self.project(self.core.request(proof, client_key), principal)

    def confirm(self, proof, request_id, challenge, revision):
        self.core.ready()
        principal = self.principal(proof)
        account = principal.account
        snapshot = self.core.confirm(proof, request_id, challenge, revision)
        # Resolve only once. After deletion, profile lookup may no longer work.
        with self.core.connection() as db:
            bound = db.execute('SELECT account,entity_id FROM title_deletion_operations WHERE id=?', (request_id,)).fetchone()
        if bound is None:
            target = self.resolve_target(account)
            if (not isinstance(target, DeletionTarget) or target.account != account
                    or target.title_entity_id != principal.title_entity_id):
                raise Rejected('operation_conflict')
            self.provider.bind_confirmed(request_id, target, self.policy)
        elif bound['account'] != account or bound['entity_id'] != principal.title_entity_id:
            raise Rejected('operation_conflict')
        try:
            self.provider.submit_confirmed(request_id, account, self.policy)
        except Rejected as error:
            if str(error) != 'provider_unconfirmed':
                raise
        with self.core.connection() as db:
            snapshot = self.core.view(self.core.owned(db, request_id, account))
        return self.project(snapshot, principal)

    def status(self, proof, request_id):
        self.core.ready()
        principal = self.principal(proof)
        return self.project(self.core.status(proof, request_id), principal)

    def cancel(self, proof, request_id):
        self.core.ready()
        principal = self.principal(proof)
        return self.project(self.core.cancel(proof, request_id), principal)
