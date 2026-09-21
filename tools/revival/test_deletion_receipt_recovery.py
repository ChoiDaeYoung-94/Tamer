import hashlib
import sqlite3
import unittest
from server.privacy.http_app import create_app
from server.privacy.session_confirmation import compose
from server.privacy.receipt_recovery import bound_hash
from tools.revival import test_deletion_session_confirmation as fixtures


class ReceiptRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.f = fixtures.SessionConfirmationTests('runTest')
        self.f.setUp()
        self.addCleanup(self.f.doCleanups)
        self.capability = 'd' * 64
        self.verifier = hashlib.sha256(self.capability.encode()).hexdigest()
        self.start()
        self.proof = self.f.grant()
        self.request = self.f.service.request(self.proof, 'a' * 32)

    def start(self, ttl=1000):
        f = self.f
        f.service, f.confirmation = compose(f.path, 'ABC12', f.secret, f.policy, lambda: f.now, f.upstream,
            recovery_origin='https://example.invalid/', recovery_ttl_seconds=ttl)
        f.app = create_app(f.service, session_confirmation=f.confirmation)

    def register(self, verifier=None):
        return self.f.post('receipt-register', {'proof': self.proof, 'requestId': self.request['requestId'],
            'verifier': verifier or self.verifier})

    def read(self, op='receipt-status', capability=None):
        return self.f.post(op, {'requestId': self.request['requestId'], 'capability': capability or self.capability})

    def submit(self):
        return self.f.service.confirm(self.proof, self.request['requestId'], self.request['challenge'], self.request['policyRevision'])

    def test_accepted_response_lost_ticket_invalid_process_restart_still_reads_receipt(self):
        status, registered = self.register()
        self.assertEqual(status, '200 OK')
        # Lost registration response: same verifier is idempotent, lifetime does not slide.
        self.assertEqual(self.register(), (status, registered))
        self.assertEqual(self.submit()['submissionState'], 'accepted') # Client never receives this response.
        self.f.expired = True
        self.f.now += 301 # The short session proof is also expired.
        self.start()
        calls = list(self.f.calls)
        status, receipt = self.read()
        self.assertEqual(status, '200 OK')
        self.assertEqual(receipt['submissionState'], 'accepted')
        self.assertEqual(receipt['ownerHash'], bound_hash('https://example.invalid/', 'ABC12', 'synthetic-a'))
        self.assertEqual(receipt['binding'], bound_hash('https://example.invalid/', 'ABC12', 'synthetic-a', 'entity-a'))
        self.assertEqual(receipt['clientKey'], 'a' * 32)
        self.assertEqual(self.f.calls, calls, 'Receipt reads must never reauthenticate, resolve, or delete')
        self.assertEqual(calls.count('DeletePlayer'), 1)
        for op in ('request', 'confirm', 'cancel'):
            body = {'proof': self.capability, 'requestId': self.request['requestId']}
            if op == 'request': body = {'proof': self.capability, 'clientKey': 'a' * 32}
            if op == 'confirm': body.update(challenge=self.request['challenge'], policyRevision='v1')
            self.assertNotEqual(self.f.post(op, body)[0], '200 OK')
        self.assertEqual(self.f.calls, calls)
        self.assertNotIn(self.capability.encode(), self.f.path.read_bytes())

    def test_registration_required_before_submit_and_verifier_cannot_be_replaced(self):
        with self.assertRaisesRegex(Exception, 'receipt_unavailable'): self.submit()
        self.assertNotIn('DeletePlayer', self.f.calls)
        self.assertEqual(self.register()[0], '200 OK')
        self.assertEqual(self.register('e' * 64)[1]['code'], 'operation_conflict')
        self.assertEqual(self.read(capability='e' * 64)[1]['code'], 'receipt_unavailable')

    def test_terminal_ack_is_idempotent_and_retires_only_this_receipt(self):
        self.register()
        self.assertEqual(self.read('receipt-ack')[1]['code'], 'receipt_unavailable')
        self.submit()
        self.assertEqual(self.read('receipt-ack'), ('200 OK', {'acknowledged': True}))
        self.start()
        self.assertEqual(self.read('receipt-ack'), ('200 OK', {'acknowledged': True}))
        self.assertEqual(self.read()[1]['code'], 'receipt_unavailable')
        self.assertEqual(self.f.calls.count('DeletePlayer'), 1)

    def test_unknown_read_replay_is_bounded_and_expiry_does_not_mean_acceptance(self):
        self.register()
        self.f.lost_delete = True
        self.assertEqual(self.submit()['submissionState'], 'submission_unknown')
        for _ in range(6): self.assertEqual(self.read()[1]['submissionState'], 'submission_unknown')
        self.assertEqual(self.read()[1]['code'], 'receipt_rate_limited')
        self.f.now += 60
        self.assertEqual(self.read()[1]['submissionState'], 'submission_unknown')
        self.f.now += 1000
        self.assertEqual(self.read()[1]['code'], 'receipt_unavailable')
        self.assertEqual(self.f.calls.count('DeletePlayer'), 1)

    def test_recreated_entity_cannot_register_or_inherit_receipt(self):
        self.register()
        self.f.info['TitleInfo']['TitlePlayerAccount']['Id'] = 'recreated-entity'
        self.proof = self.f.grant()
        self.assertEqual(self.register()[1]['code'], 'operation_conflict')
        with sqlite3.connect(self.f.path) as db:
            self.assertEqual(db.execute('SELECT entity_id FROM deletion_receipt_access').fetchone()[0], 'entity-a')


if __name__ == '__main__': unittest.main()
