import io
import json
import tempfile
import unittest
from pathlib import Path

from server.privacy.deletion import Policy, Principal, Rejected
from server.privacy.http_app import create_app
from server.privacy.intake import IntakeService
from server.privacy.title_deletion_provider import DeletionTarget


class DeletionIntakeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'intake.db'
        self.calls = []
        self.lost = False
        self.entity = 'entity-a'
        self.policy = Policy('v1', 'title', True, True, True, ('test-only',))
        self.service = self.make_service()
        self.app = create_app(self.service)

    def make_service(self):
        def auth(proof):
            if proof != 'verified-server-proof':
                raise Rejected('reauthentication_required')
            return Principal('synthetic-a', 100, self.entity)
        def submit(target):
            self.calls.append(target)
            if self.lost:
                raise TimeoutError()
            return True
        return IntakeService(self.path, 'SYNTHETIC', auth,
            lambda account: DeletionTarget('SYNTHETIC', account, 'entity-a'),
            submit, lambda target: target.account == 'synthetic-a', self.policy, lambda: 100)

    def post(self, operation, **body):
        raw = json.dumps(dict(proof='verified-server-proof', **body)).encode()
        status = []
        result = self.app({'REQUEST_METHOD': 'POST', 'PATH_INFO': '/v1/deletion/' + operation,
            'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)),
            'wsgi.input': io.BytesIO(raw)}, lambda s, h: status.append(s))
        return status[0], json.loads(b''.join(result))

    def request(self):
        status, request = self.post('request', clientKey='a' * 32)
        self.assertEqual(status, '200 OK')
        return request

    def confirm(self, request):
        return self.post('confirm', requestId=request['requestId'],
            challenge=request['challenge'], policyRevision=request['policyRevision'])

    def test_http_acceptance_survives_restart_without_resubmit_or_completion(self):
        request = self.request()
        status, receipt = self.confirm(request)
        self.assertEqual(status, '200 OK')
        self.assertEqual(receipt['submissionState'], 'accepted')
        self.assertEqual(receipt['state'], 'processing')
        self.assertEqual(receipt['completionEvidence'], '')
        self.service = self.make_service()
        self.app = create_app(self.service)
        self.assertEqual(self.confirm(request)[1]['submissionState'], 'accepted')
        self.assertEqual(self.post('status', requestId=request['requestId'])[1]['submissionState'], 'accepted')
        self.assertEqual(len(self.calls), 1)

    def test_unknown_is_not_acceptance_and_never_blindly_retried(self):
        self.lost = True
        request = self.request()
        self.assertEqual(self.confirm(request)[1]['submissionState'], 'submission_unknown')
        self.assertEqual(self.confirm(request)[1]['submissionState'], 'submission_unknown')
        self.assertEqual(len(self.calls), 1)

    def test_unconfigured_policy_cannot_enable_http(self):
        service = IntakeService(self.path, 'SYNTHETIC', policy=self.policy)
        self.assertFalse(service.policy.ready)
        with self.assertRaises(Rejected):
            service.request('anything', 'a' * 32)

    def test_client_identity_and_timestamp_are_not_authentication(self):
        with self.assertRaises(Rejected):
            self.service.request('{"account":"synthetic-a","reauthenticated_at":100}', 'a' * 32)
        request = self.request()
        status, _ = self.post('confirm', requestId=request['requestId'], challenge=request['challenge'],
                              policyRevision='v1', account='synthetic-other')
        self.assertEqual(status, '400 Bad Request')
        self.assertEqual(self.calls, [])

    def test_cancel_wins_before_submission(self):
        request = self.request()
        self.post('cancel', requestId=request['requestId'])
        self.assertEqual(self.confirm(request)[0], '409 Conflict')
        self.assertEqual(self.calls, [])

    def test_wrong_server_target_is_rejected(self):
        request = self.request()
        self.service.resolve_target = lambda account: DeletionTarget('SYNTHETIC', 'other', 'entity')
        self.assertEqual(self.confirm(request)[0], '409 Conflict')
        self.assertEqual(self.calls, [])

    def test_recreated_entity_cannot_inherit_old_acceptance(self):
        request = self.request()
        self.confirm(request)
        same = self.post('request', clientKey='b' * 32)[1]
        self.assertEqual(same['requestId'], request['requestId'])
        self.service.resolve_target = lambda account: DeletionTarget('SYNTHETIC', account, 'entity-new')
        self.entity = 'entity-new'
        new = self.post('request', clientKey='c' * 32)[1]
        self.assertNotEqual(new['requestId'], request['requestId'])
        self.assertEqual(new['state'], 'awaiting_confirmation')
        self.assertEqual(new['submissionState'], 'not_submitted')
        self.assertEqual(len(self.calls), 1)
        self.assertEqual(self.post('status', requestId=request['requestId'])[0], '409 Conflict')
        self.assertEqual(self.confirm(request)[0], '409 Conflict')
