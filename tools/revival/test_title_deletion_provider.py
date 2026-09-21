import tempfile
import threading
import unittest
from dataclasses import replace
from pathlib import Path

from server.privacy.deletion import DeletionService, Policy, Principal, Rejected
from server.privacy.title_deletion_provider import (
    CompletionProof, DeletionTarget, ServerDeletePlayerSubmission,
    TitleDeletionProvider,
)


class TitleDeletionProviderTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'ledger.sqlite'
        self.target = DeletionTarget('SYNTHETIC', 'synthetic-a', 'synthetic-entity-a')
        self.policy = Policy('synthetic-v1', 'title', True, True, True, ('synthetic-only',))
        self.calls, self.polls, self.proofs = [], [], {}
        self.accept = lambda: {'code': 200, 'status': 'OK', 'data': {}}
        self.verify = lambda target: target == self.target
        self.provider = self.make_provider()
        self.service = DeletionService(self.path, lambda proof: Principal('synthetic-a', 100),
                                       self.provider, self.policy, lambda: 100)
        self.request = self.service.request('synthetic-proof', 'a' * 32)
        self.id = self.request['requestId']
        self.service.confirm('synthetic-proof', self.id, self.request['challenge'], self.policy.revision)

    def invoke(self, title, route, body):
        self.calls.append((title, route, body))
        return self.accept()

    def component(self, name):
        def reconcile(request_id, target, policy):
            self.polls.append(name)
            proof = self.proofs.get(name)
            if isinstance(proof, Exception):
                raise proof
            return proof
        return reconcile

    def make_provider(self):
        return TitleDeletionProvider(self.path, 'SYNTHETIC',
            ServerDeletePlayerSubmission('SYNTHETIC', self.invoke), lambda target: self.verify(target),
            {name: self.component(name) for name in ('title_player', 'tamer_owned_data')})

    def bind(self):
        self.provider.bind_confirmed(self.id, self.target, self.policy)

    def complete(self, name):
        self.proofs[name] = CompletionProof(self.id, self.target, name, 'synthetic-proof-' + name)

    def test_queue_receipt_is_pending_and_uses_only_pinned_title_delete(self):
        self.bind()
        result = self.service.advance(self.id)
        self.assertEqual(result['state'], 'processing')
        self.assertEqual(result['completionEvidence'], '')
        self.assertEqual(self.calls, [('SYNTHETIC', '/Server/DeletePlayer', {'PlayFabId': 'synthetic-a'})])
        self.service.advance(self.id)
        self.assertEqual(len(self.calls), 1)

    def test_intake_without_completion_observers_returns_durable_acceptance(self):
        self.provider = TitleDeletionProvider(self.path, 'SYNTHETIC',
            ServerDeletePlayerSubmission('SYNTHETIC', self.invoke), lambda target: target == self.target)
        self.bind()
        self.assertEqual(self.provider.submit_confirmed(self.id, 'synthetic-a', self.policy), 'accepted')
        restarted = TitleDeletionProvider(self.path, 'SYNTHETIC',
            ServerDeletePlayerSubmission('SYNTHETIC', self.invoke), lambda target: target == self.target)
        self.assertEqual(restarted.submit_confirmed(self.id, 'synthetic-a', self.policy), 'accepted')
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.service.status('synthetic-proof', self.id)['state'], 'processing')
        with self.assertRaises(Rejected):
            restarted.reconcile(self.id, 'synthetic-a', self.policy)
        self.assertEqual(self.polls, [])

    def test_intake_response_loss_stays_unknown_without_second_delete(self):
        self.bind()
        def lost():
            raise TimeoutError()
        self.accept = lost
        with self.assertRaises(Rejected):
            self.provider.submit_confirmed(self.id, 'synthetic-a', self.policy)
        self.assertEqual(self.provider.submit_confirmed(self.id, 'synthetic-a', self.policy), 'submission_unknown')
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.polls, [])

    def test_cancelled_bound_request_cannot_submit(self):
        self.bind()
        self.service.cancel('synthetic-proof', self.id)
        with self.assertRaises(Rejected):
            self.provider.submit_confirmed(self.id, 'synthetic-a', self.policy)
        self.assertEqual(self.calls, [])

    def test_unbound_and_wrong_account_never_submit(self):
        with self.assertRaises(Rejected):
            self.service.advance(self.id)
        self.bind()
        with self.assertRaises(Rejected):
            self.provider.reconcile(self.id, 'synthetic-b', self.policy)
        self.assertEqual(self.calls, [])

    def test_unconfirmed_request_cannot_bind(self):
        other_service = DeletionService(self.path, lambda _: Principal('synthetic-b', 100),
                                        self.provider, self.policy, lambda: 100)
        pending = other_service.request('synthetic-proof', 'b' * 32)
        with self.assertRaises(Rejected):
            self.provider.bind_confirmed(pending['requestId'], replace(self.target, account='synthetic-b'), self.policy)
        self.assertEqual(self.calls, [])

    def test_two_workers_do_not_submit_twice(self):
        self.bind()
        entered, release = threading.Event(), threading.Event()
        failures = []
        def wait_for_release():
            entered.set()
            if not release.wait(5):
                raise TimeoutError()
            return {'code': 200, 'status': 'OK', 'data': {}}
        self.accept = wait_for_release
        def first_worker():
            try:
                self.service.advance(self.id)
            except Exception as error:
                failures.append(error)
        worker = threading.Thread(target=first_worker)
        worker.start()
        try:
            self.assertTrue(entered.wait(5))
            self.assertEqual(self.service.advance(self.id)['state'], 'processing')
        finally:
            release.set()
            worker.join(5)
        self.assertFalse(worker.is_alive())
        self.assertEqual(failures, [])
        self.assertEqual(len(self.calls), 1)

    def test_target_and_policy_rebinding_are_rejected(self):
        self.bind()
        for target, policy in [
            (replace(self.target, title_id='OTHER'), self.policy),
            (replace(self.target, title_entity_id='recreated-account'), self.policy),
            (replace(self.target, account='synthetic-b'), self.policy),
            (self.target, replace(self.policy, scope='master')),
            (self.target, replace(self.policy, retention_plan=('changed',))),
        ]:
            with self.subTest(target=target, policy=policy), self.assertRaises(Rejected):
                self.provider.bind_confirmed(self.id, target, policy)
        self.assertEqual(self.calls, [])

    def test_identity_fence_failure_never_submits(self):
        self.bind()
        self.verify = lambda _: False
        with self.assertRaises(Rejected):
            self.service.advance(self.id)
        self.assertEqual(self.calls, [])

    def test_definitive_disabled_rejection_can_retry_without_duplicate_request(self):
        self.bind()
        self.accept = lambda: {'code': 400, 'error': 'APINotEnabledForGameServerAccess'}
        with self.assertRaises(Rejected):
            self.service.advance(self.id)
        self.accept = lambda: {'code': 200, 'status': 'OK', 'data': {}}
        self.assertEqual(self.service.advance(self.id)['state'], 'processing')
        self.assertEqual(len(self.calls), 2)

    def test_response_loss_and_process_restart_do_not_resubmit(self):
        self.bind()
        def lost():
            raise TimeoutError('private transport detail')
        self.accept = lost
        with self.assertRaisesRegex(Rejected, '^provider_unconfirmed$'):
            self.service.advance(self.id)
        self.service.provider = self.make_provider()
        self.assertEqual(self.service.advance(self.id)['state'], 'processing')
        self.assertEqual(len(self.calls), 1)

    def test_malformed_receipt_stays_unknown(self):
        self.bind()
        self.accept = lambda: {'code': 200, 'status': 'OK'}
        with self.assertRaises(Rejected):
            self.service.advance(self.id)
        self.assertEqual(self.service.advance(self.id)['state'], 'processing')
        self.assertEqual(len(self.calls), 1)

    def test_partial_completion_survives_restart_and_retries_only_pending_component(self):
        self.bind()
        self.complete('title_player')
        self.proofs['tamer_owned_data'] = RuntimeError('transient cleanup failure')
        with self.assertRaises(RuntimeError):
            self.service.advance(self.id)
        self.polls.clear()
        self.complete('tamer_owned_data')
        self.service.provider = self.make_provider()
        result = self.service.advance(self.id)
        self.assertEqual(result['state'], 'completed')
        self.assertTrue(result['completionEvidence'].startswith('verified-title-components:'))
        self.assertNotIn(self.target.account, result['completionEvidence'])
        self.assertEqual(self.polls, ['tamer_owned_data'])
        self.assertEqual(len(self.calls), 1)

    def test_wrong_request_target_or_component_proof_cannot_complete(self):
        self.bind()
        base = CompletionProof(self.id, self.target, 'title_player', 'synthetic-evidence')
        for proof in [True, 'not-found', replace(base, request_id='b' * 32),
                      replace(base, target=replace(self.target, title_entity_id='other')),
                      replace(base, component='master'), replace(base, evidence='')]:
            self.proofs['title_player'] = proof
            with self.subTest(proof=proof), self.assertRaises(Rejected):
                self.service.advance(self.id)
        self.assertEqual(len(self.calls), 1)

    def test_reconfigured_component_set_and_unapproved_policy_block_resume(self):
        self.bind()
        changed = self.make_provider()
        changed.components['additional_data'] = self.component('additional_data')
        with self.assertRaises(Rejected):
            changed.reconcile(self.id, self.target.account, self.policy)
        for policy in [replace(self.policy, enabled=False), replace(self.policy, scope='master')]:
            with self.subTest(policy=policy), self.assertRaises(Rejected):
                self.provider.reconcile(self.id, self.target.account, policy)
        self.assertEqual(self.calls, [])


if __name__ == '__main__':
    unittest.main()
