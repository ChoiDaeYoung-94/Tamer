import io
import json
import tempfile
import unittest
from pathlib import Path
from dataclasses import replace
from server.privacy.deletion import DeletionService, Policy, Principal, ProviderResult, Rejected, SyntheticProvider
from server.privacy.http_app import create_app


class DeletionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'requests.sqlite'
        self.now = 1000
        self.provider = SyntheticProvider()
        self.policy = Policy('synthetic-v1', 'title', True, True, True, ('synthetic retention decision',))
        self.service = self.new_service()
        self.app = create_app(self.service)

    def authenticate(self, proof):
        if proof not in ('fresh-a', 'fresh-b', 'expired'):
            raise Rejected('reauthentication_required')
        return Principal('synthetic-b' if proof == 'fresh-b' else 'synthetic-a', self.now - (301 if proof == 'expired' else 0))

    def new_service(self):
        return DeletionService(self.path, self.authenticate, self.provider, self.policy, lambda: self.now)

    def call(self, action, body=None, app=None):
        raw = json.dumps(body or {}).encode()
        environ = {'REQUEST_METHOD': 'POST', 'PATH_INFO': '/v1/deletion/' + action,
                   'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)), 'wsgi.input': io.BytesIO(raw)}
        statuses = []
        output = b''.join((app or self.app)(environ, lambda status, headers: statuses.append(status)))
        return statuses[0], json.loads(output)

    def request(self, key='a' * 32):
        return self.service.request('fresh-a', key)

    def confirm(self, request):
        return self.service.confirm('fresh-a', request['requestId'], request['challenge'], request['policyRevision'])

    def test_wsgi_request_confirm_worker_status_end_to_end(self):
        status, request = self.call('request', {'proof': 'fresh-a', 'clientKey': 'a' * 32})
        self.assertEqual(status, '200 OK')
        self.assertEqual(request['state'], 'awaiting_confirmation')
        self.assertEqual(self.provider.operations, {})
        status, queued = self.call('confirm', dict(proof='fresh-a', requestId=request['requestId'], challenge=request['challenge'], policyRevision=request['policyRevision']))
        self.assertEqual(queued['state'], 'queued')
        self.assertEqual(self.service.advance(request['requestId'])['state'], 'processing')
        self.assertEqual(self.call('status', dict(proof='fresh-a', requestId=request['requestId']))[1]['state'], 'processing')
        self.provider.completed.add(request['requestId'])
        self.assertEqual(self.service.advance(request['requestId'])['state'], 'completed')
        final = self.call('status', dict(proof='fresh-a', requestId=request['requestId']))[1]
        self.assertTrue(final['completionEvidence'].startswith('synthetic-completion-'))

    def test_default_policy_blocks_without_authentication(self):
        self.service.policy = Policy()
        self.service.authenticate = lambda _: self.fail('must not authenticate')
        with self.assertRaisesRegex(Rejected, 'policy_unavailable'):
            self.request()

    def test_each_unresolved_policy_blocks(self):
        for field, value in [('revision', ''), ('scope', ''), ('retention_approved', False), ('rejoin_approved', False), ('enabled', False), ('retention_plan', ())]:
            self.service.policy = replace(self.policy, **{field: value})
            with self.subTest(field=field), self.assertRaises(Rejected):
                self.request()

    def test_fresh_proof_required(self):
        for proof in ('expired', 'cached-account-id', '', None):
            with self.subTest(proof=proof), self.assertRaises(Rejected):
                self.service.request(proof, 'a' * 32)

    def test_response_loss_reuses_request_and_rotates_challenge(self):
        first, second = self.request(), self.request()
        self.assertEqual(first['requestId'], second['requestId'])
        self.assertNotEqual(first['challenge'], second['challenge'])
        with self.assertRaises(Rejected):
            self.confirm(first)
        self.assertEqual(self.confirm(second)['state'], 'queued')

    def test_confirm_retry_and_service_restart_are_idempotent(self):
        request = self.request()
        self.confirm(request)
        self.service = self.new_service()
        self.assertEqual(self.confirm(request)['state'], 'queued')
        self.service.advance(request['requestId'])
        self.service.advance(request['requestId'])
        self.assertEqual(len(self.provider.operations), 1)
        self.assertEqual(self.request()['state'], 'processing')

    def test_other_account_cannot_status_confirm_or_cancel(self):
        request = self.request()
        for action in [lambda: self.service.status('fresh-b', request['requestId']),
                       lambda: self.service.cancel('fresh-b', request['requestId']),
                       lambda: self.service.confirm('fresh-b', request['requestId'], request['challenge'], request['policyRevision'])]:
            with self.assertRaisesRegex(Rejected, 'request_not_found'):
                action()

    def test_confirmation_expiry_requires_new_challenge(self):
        request = self.request()
        self.now += 301
        with self.assertRaisesRegex(Rejected, 'confirmation_expired'):
            self.confirm(request)
        self.assertEqual(self.confirm(self.request())['state'], 'queued')

    def test_policy_change_blocks_confirmation_and_worker(self):
        request = self.request()
        self.service.policy = replace(self.policy, revision='synthetic-v2')
        with self.assertRaisesRegex(Rejected, 'policy_changed'):
            self.confirm(request)
        self.service.policy = self.policy
        self.confirm(request)
        self.service.policy = replace(self.policy, revision='synthetic-v2')
        with self.assertRaises(Rejected):
            self.service.advance(request['requestId'])
        self.assertEqual(self.provider.operations, {})

    def test_cancel_before_submission_cannot_reach_provider(self):
        request = self.request()
        self.confirm(request)
        self.assertEqual(self.service.cancel('fresh-a', request['requestId'])['state'], 'cancelled')
        self.assertEqual(self.service.advance(request['requestId'])['state'], 'cancelled')
        self.assertEqual(self.provider.operations, {})

    def test_cancel_after_submission_rejected(self):
        request = self.request()
        self.confirm(request)
        self.service.advance(request['requestId'])
        with self.assertRaisesRegex(Rejected, 'already_submitted'):
            self.service.cancel('fresh-a', request['requestId'])

    def test_provider_exception_never_marks_completed_and_reconciles_same_id(self):
        request = self.request()
        self.confirm(request)
        original = self.provider.reconcile
        def lose_response(*args):
            original(*args)
            raise RuntimeError('synthetic-secret-error')
        self.provider.reconcile = lose_response
        with self.assertRaises(RuntimeError):
            self.service.advance(request['requestId'])
        self.assertEqual(self.service.status('fresh-a', request['requestId'])['state'], 'processing')
        self.provider.reconcile = original
        self.service = self.new_service()
        self.service.advance(request['requestId'])
        self.assertEqual(len(self.provider.operations), 1)

    def test_completion_requires_explicit_evidence(self):
        request = self.request()
        self.confirm(request)
        self.provider.reconcile = lambda *args: ProviderResult('completed')
        with self.assertRaisesRegex(Rejected, 'provider_unconfirmed'):
            self.service.advance(request['requestId'])
        self.assertEqual(self.service.status('fresh-a', request['requestId'])['state'], 'processing')

    def test_second_active_request_rejected(self):
        self.request()
        with self.assertRaisesRegex(Rejected, 'request_already_exists'):
            self.request('b' * 32)

    def test_http_rejects_client_supplied_account_and_worker_route(self):
        status, _ = self.call('request', {'proof': 'fresh-a', 'clientKey': 'a' * 32, 'account': 'synthetic-b'})
        self.assertTrue(status.startswith('400'))
        status, _ = self.call('advance', {'proof': 'fresh-a', 'requestId': 'a' * 32})
        self.assertTrue(status.startswith('404'))

    def test_http_sanitizes_upstream_errors(self):
        self.service.authenticate = lambda _: (_ for _ in ()).throw(RuntimeError('private-ticket'))
        status, result = self.call('request', {'proof': 'fresh-a', 'clientKey': 'a' * 32})
        self.assertTrue(status.startswith('503'))
        self.assertNotIn('private-ticket', json.dumps(result))

    def test_synthetic_provider_rejects_real_identity(self):
        with self.assertRaisesRegex(Rejected, 'synthetic_account_required'):
            self.provider.reconcile('a' * 32, 'real-account', self.policy)

    def test_unknown_rejection_text_is_not_exposed(self):
        self.service.authenticate = lambda _: (_ for _ in ()).throw(Rejected('private-proof'))
        status, result = self.call('request', {'proof': 'fresh-a', 'clientKey': 'a' * 32})
        self.assertNotIn('private-proof', json.dumps(result))

    def test_normal_web_page_cannot_offer_synthetic_completion(self):
        statuses = []
        page = b''.join(self.app({'REQUEST_METHOD': 'GET', 'PATH_INFO': '/privacy/deletion'}, lambda status, headers: statuses.append(status))).decode()
        self.assertEqual(statuses, ['200 OK'])
        self.assertNotIn('/demo/advance', page)
        self.assertIn('계정은 변경되지 않았습니다', page)

    def test_demo_worker_is_absent_from_default_app(self):
        request = self.request()
        self.confirm(request)
        raw = json.dumps(dict(proof='fresh-a', requestId=request['requestId'], complete=True)).encode()
        result = b''.join(self.app({'REQUEST_METHOD': 'POST', 'PATH_INFO': '/demo/advance',
                                   'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)),
                                   'wsgi.input': io.BytesIO(raw)}, lambda *args: None))
        self.assertEqual(json.loads(result)['code'], 'unavailable')
        self.assertEqual(self.provider.operations, {})

    def test_duplicate_json_keys_rejected(self):
        raw = b'{"proof":"fresh-a","proof":"fresh-b","clientKey":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}'
        statuses = []
        self.app({'REQUEST_METHOD': 'POST', 'PATH_INFO': '/v1/deletion/request', 'CONTENT_TYPE': 'application/json',
                  'CONTENT_LENGTH': str(len(raw)), 'wsgi.input': io.BytesIO(raw)}, lambda status, headers: statuses.append(status))
        self.assertEqual(statuses, ['400 Bad Request'])


if __name__ == '__main__':
    unittest.main()
