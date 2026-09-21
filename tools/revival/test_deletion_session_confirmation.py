import copy
from concurrent.futures import ThreadPoolExecutor
from dataclasses import replace
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from server.privacy.deletion import Principal, Rejected
from server.privacy.http_app import create_app
from server.privacy.playfab_session import PlayFabSession, NoRedirect, request_json
from server.privacy.session_confirmation import compose
from server.privacy.deletion import Policy


class SessionConfirmationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'session.sqlite'
        self.now = 1000
        self.calls = []
        self.ticket = 'synthetic-session-ticket'
        self.secret = 'synthetic-server-secret'
        self.info = {'PlayFabId': 'synthetic-a', 'TitleInfo': {
            'TitlePlayerAccount': {'Id': 'entity-a', 'Type': 'title_player_account'}},
            'CustomIdInfo': {'CustomId': 'synthetic-device'}}
        self.expired = False
        self.lost_delete = False
        self.policy = Policy('v1', 'title', True, True, True, ('synthetic-only',), True)
        self.service, self.confirmation = self.make()
        self.app = create_app(self.service, session_confirmation=self.confirmation)
        guard = patch('urllib.request.OpenerDirector.open', side_effect=AssertionError('No real network in these tests'))
        guard.start()
        self.addCleanup(guard.stop)

    def upstream(self, url, headers, body):
        self.assertTrue(url.startswith('https://ABC12.playfabapi.com/Server/'))
        self.assertEqual(headers['X-SecretKey'], self.secret)
        route = url.split('/Server/')[1]
        self.calls.append(route)
        if route == 'AuthenticateSessionTicket':
            self.assertEqual(set(body), {'SessionTicket'})
            data = {'IsSessionTicketExpired': self.expired, 'UserInfo': copy.deepcopy(self.info)}
        elif route == 'GetUserAccountInfo':
            self.assertEqual(body, {'PlayFabId': 'synthetic-a'})
            data = {'UserInfo': copy.deepcopy(self.info)}
        elif route == 'DeletePlayer':
            self.assertEqual(body, {'PlayFabId': 'synthetic-a'})
            if self.lost_delete:
                raise TimeoutError('must-not-leak-secret')
            data = {}
        else:
            self.fail('Unexpected route')
        return {'code': 200, 'status': 'OK', 'data': data}

    def make(self, policy=None):
        return compose(self.path, 'ABC12', self.secret, policy or self.policy,
                       lambda: self.now, self.upstream)

    def post(self, operation, body):
        raw = json.dumps(body).encode()
        status = []
        response = self.app({'REQUEST_METHOD': 'POST', 'PATH_INFO': '/v1/deletion/' + operation,
            'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)), 'wsgi.input': io.BytesIO(raw)},
            lambda s, h: status.append(s))
        return status[0], json.loads(b''.join(response))

    def grant(self, key='a' * 32):
        challenge = self.confirmation.begin(self.ticket, key)
        return self.confirmation.confirm(self.ticket, challenge['nonce'], True)['proof']

    def test_evidence_kinds_are_distinct_and_only_single_anonymous_type_is_allowed(self):
        for field, value in [('CustomIdInfo', {'CustomId': 'device'}),
                             ('AndroidDeviceInfo', {'AndroidDeviceId': 'device'})]:
            with self.subTest(field=field):
                self.info.pop('CustomIdInfo', None)
                self.info.pop('AndroidDeviceInfo', None)
                self.info[field] = value
                principal = self.confirmation.authenticate(self.grant())
                self.assertIsNone(principal.reauthenticated_at)
                self.assertEqual(principal.evidence_kind, 'session_confirmation')
                self.assertEqual(principal.confirmed_at, self.now)
                self.assertEqual(principal.title_entity_id, 'entity-a')
        self.assertNotIn('DeletePlayer', self.calls)

    def test_email_shared_password_native_unknown_and_multiple_types_are_not_anonymous(self):
        base = copy.deepcopy(self.info)
        changes = [dict(PrivateInfo={'Email': 'synthetic@example.invalid'}), dict(Username='synthetic'),
                   dict(AndroidDeviceInfo={'AndroidDeviceId': 'device'}), dict(FutureProviderInfo={'Id': 'other'}),
                   dict(GooglePlayGamesInfo={'GooglePlayGamesPlayerId': 'provider'}),
                   dict(OpenIdInfo=[{'Subject': 'provider'}]), dict(GoogleInfo={}), dict(CustomIdInfo={}), dict(CustomIdInfo=None)]
        for change in changes:
            with self.subTest(fields=list(change)):
                self.info = dict(base, **change)
                with self.assertRaisesRegex(Rejected, 'account_type_unsupported'):
                    self.confirmation.begin(self.ticket, 'a' * 32)
        self.assertNotIn('DeletePlayer', self.calls)

    def test_expired_ticket_and_missing_or_wrong_entity_are_rejected(self):
        self.expired = True
        with self.assertRaises(Rejected): self.confirmation.begin(self.ticket, 'a' * 32)
        self.expired = False
        for entity in ({}, {'Id': 'entity-a', 'Type': 'master_player_account'}, {'Type': 'title_player_account'}):
            self.info['TitleInfo']['TitlePlayerAccount'] = entity
            with self.assertRaises(Rejected): self.confirmation.begin(self.ticket, 'a' * 32)

    def test_default_policy_is_disabled_before_network(self):
        service, confirmation = compose(self.path, 'ABC12', self.secret, request=self.upstream)
        self.assertFalse(service.policy.ready)
        with self.assertRaisesRegex(Rejected, 'policy_unavailable'):
            confirmation.begin(self.ticket, 'a' * 32)
        service, _ = self.make(replace(self.policy, session_confirmation_enabled=False))
        self.assertFalse(service.policy.ready)
        self.assertEqual(self.calls, [])

    def test_nonce_requires_explicit_boolean_confirmation(self):
        nonce = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
        for confirmed in (False, 1, 'true', None):
            with self.assertRaisesRegex(Rejected, 'invalid_confirmation'):
                self.confirmation.confirm(self.ticket, nonce, confirmed)
        self.assertEqual(self.calls, ['AuthenticateSessionTicket'])

    def test_nonce_rejects_other_session_account_entity_and_link_changes(self):
        nonce = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
        with self.assertRaises(Rejected): self.confirmation.confirm('other-ticket', nonce, True)
        self.assertEqual(len(self.calls), 1)
        base = copy.deepcopy(self.info)
        self.info['PlayFabId'] = 'synthetic-other'
        with self.assertRaisesRegex(Rejected, 'operation_conflict'):
            self.confirmation.confirm(self.ticket, nonce, True)
        self.info = copy.deepcopy(base)
        self.info['TitleInfo']['TitlePlayerAccount']['Id'] = 'entity-new'
        with self.assertRaisesRegex(Rejected, 'operation_conflict'):
            self.confirmation.confirm(self.ticket, nonce, True)
        self.info = copy.deepcopy(base)
        del self.info['CustomIdInfo']
        self.info['AndroidDeviceInfo'] = {'AndroidDeviceId': 'device'}
        with self.assertRaisesRegex(Rejected, 'operation_conflict'):
            self.confirmation.confirm(self.ticket, nonce, True)

    def test_nonce_expiry_clock_rollback_and_supersession(self):
        for clock in (999, 1120):
            self.now = 1000
            nonce = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
            self.now = clock
            with self.assertRaises(Rejected): self.confirmation.confirm(self.ticket, nonce, True)
        self.now = 1000
        old = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
        self.confirmation.begin(self.ticket, 'a' * 32)
        with self.assertRaises(Rejected): self.confirmation.confirm(self.ticket, old, True)

    def test_concurrent_confirm_consumes_nonce_once_and_restart_preserves_proof(self):
        nonce = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
        def attempt(_):
            try: return self.confirmation.confirm(self.ticket, nonce, True)
            except Rejected: return None
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = [r for r in pool.map(attempt, range(2)) if r is not None]
        self.assertEqual(len(results), 1)
        _, restarted = self.make()
        self.assertEqual(restarted.authenticate(results[0]['proof']).account, 'synthetic-a')
        with self.assertRaises(Rejected): restarted.confirm(self.ticket, nonce, True)
        raw = self.path.read_bytes()
        self.assertNotIn(self.ticket.encode(), raw)
        self.assertNotIn(nonce.encode(), raw)
        self.assertNotIn(json.loads(results[0]['proof'])['grant'].encode(), raw)

    def test_storage_failure_does_not_consume_nonce_or_issue_proof(self):
        nonce = self.confirmation.begin(self.ticket, 'a' * 32)['nonce']
        with self.confirmation.connection() as db:
            db.execute("CREATE TRIGGER reject_proof BEFORE INSERT ON deletion_session_proofs BEGIN SELECT RAISE(ABORT,'storage fault'); END")
        with self.assertRaises(Exception): self.confirmation.confirm(self.ticket, nonce, True)
        with self.confirmation.connection() as db:
            self.assertEqual(db.execute('SELECT consumed FROM deletion_session_nonces').fetchone()[0], 0)
        self.assertNotIn('DeletePlayer', self.calls)

    def test_proof_is_session_intent_policy_and_lifetime_bound(self):
        proof = self.grant()
        wrong = json.loads(proof)
        wrong['sessionTicket'] = 'different-ticket'
        with self.assertRaises(Rejected): self.confirmation.authenticate(json.dumps(wrong))
        with self.assertRaises(Rejected): self.service.request(proof, 'b' * 32)
        for clock in (999, 1300):
            self.now = clock
            with self.assertRaises(Rejected): self.confirmation.authenticate(proof)
        self.now = 1000
        _, changed = self.make(replace(self.policy, revision='v2'))
        with self.assertRaises(Rejected): changed.authenticate(proof)

    def test_proof_cannot_read_or_confirm_another_intent_for_same_account(self):
        proof = self.grant()
        first = self.service.request(proof, 'a' * 32)
        self.service.cancel(proof, first['requestId'])
        second = self.grant('b' * 32)
        with self.assertRaisesRegex(Rejected, 'operation_conflict'):
            self.service.status(second, first['requestId'])
        with self.assertRaisesRegex(Rejected, 'operation_conflict'):
            self.service.confirm(second, first['requestId'], first['challenge'], 'v1')

    def test_http_confirmed_session_reaches_existing_provider_once(self):
        status, challenge = self.post('session-challenge', {'sessionTicket': self.ticket, 'clientKey': 'a' * 32})
        self.assertEqual(status, '200 OK')
        status, authorization = self.post('session-confirm', {'sessionTicket': self.ticket,
            'nonce': challenge['nonce'], 'confirmed': True})
        self.assertEqual(status, '200 OK')
        proof = authorization['proof']
        status, request = self.post('request', {'proof': proof, 'clientKey': 'a' * 32})
        self.assertEqual(status, '200 OK')
        body = dict(proof=proof, requestId=request['requestId'], challenge=request['challenge'], policyRevision='v1')
        self.assertEqual(self.post('confirm', body)[1]['submissionState'], 'accepted')
        self.expired = True  # Existing bounded capability can read acceptance after ticket invalidation.
        self.service, self.confirmation = self.make()
        self.app = create_app(self.service, session_confirmation=self.confirmation)
        self.assertEqual(self.post('confirm', body)[1]['submissionState'], 'accepted')
        self.assertEqual(self.post('status', dict(proof=proof, requestId=request['requestId']))[1]['submissionState'], 'accepted')
        self.assertEqual(self.calls.count('DeletePlayer'), 1)

    def test_http_unknown_never_resubmits(self):
        proof = self.grant()
        request = self.service.request(proof, 'a' * 32)
        self.lost_delete = True
        for _ in range(2):
            result = self.service.confirm(proof, request['requestId'], request['challenge'], 'v1')
            self.assertEqual(result['submissionState'], 'submission_unknown')
        self.assertEqual(self.calls.count('DeletePlayer'), 1)

    def test_http_rejects_client_account_type_time_and_no_adapter(self):
        for field in ('account', 'entity', 'accountType', 'confirmedAt', 'reauthenticated_at'):
            status, _ = self.post('session-challenge', dict(sessionTicket=self.ticket, clientKey='a' * 32, **{field: 'untrusted'}))
            self.assertEqual(status, '400 Bad Request')
        self.assertEqual(self.calls, [])
        self.app = create_app(self.service)
        self.assertEqual(self.post('session-challenge', dict(sessionTicket=self.ticket, clientKey='a' * 32))[1]['code'], 'unavailable')

    def test_core_rejects_evidence_kind_confusion_and_nonfinite_time(self):
        original = self.service.core.authenticate
        try:
            for principal in (Principal('synthetic-a', self.now, 'entity-a', 'session_confirmation', self.now, 'a' * 32),
                              Principal('synthetic-a', None, 'entity-a', 'provider_reauthentication', self.now),
                              Principal('synthetic-a', float('nan')), Principal('synthetic-a', True)):
                self.service.core.authenticate = lambda _: principal
                with self.assertRaises(Rejected): self.service.core.principal('synthetic')
        finally: self.service.core.authenticate = original

    def test_pinned_transport_denies_other_title_routes_and_redirects(self):
        for title, route, body in [('OTHER', '/Server/DeletePlayer', {'PlayFabId': 'synthetic-a'}),
                                  ('ABC12', '/Admin/DeleteMasterPlayerAccount', {'PlayFabId': 'synthetic-a'}),
                                  ('ABC12', '/Client/LoginWithCustomID', {'CustomId': 'synthetic'}),
                                  ('ABC12', '/Server/DeletePlayer', {'PlayFabId': 'synthetic-a', 'TitleId': 'OTHER'})]:
            with self.assertRaises(Rejected): self.confirmation.upstream.invoke(title, route, body)
        self.assertEqual(self.calls, [])
        self.assertIsNone(NoRedirect().redirect_request(None, None, 302, None, {}, 'https://elsewhere.invalid'))
        with self.assertRaisesRegex(Rejected, '^unavailable$'):
            request_json('https://ABC12.playfabapi.com/Server/AuthenticateSessionTicket', {}, {})


if __name__ == '__main__':
    unittest.main()
