import copy
import io
import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
from contextlib import closing
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from server.receipts.verification import PRODUCT, Ledger, Rejected, Verifier, account_binding
from server.receipts.upstream import Upstream
from server.receipts.http_app import create_app


class ReceiptServerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.db = Path(self.temp.name) / 'grants.sqlite'
        self.account = 'TEST-ACCOUNT-A'
        self.package = 'example.test.game'
        self.purchase = {
            'purchaseStateContext': {'purchaseState': 'PURCHASED'},
            'testPurchaseContext': {'fopType': 'TEST'},
            'obfuscatedExternalAccountId': account_binding(self.account),
            'acknowledgementState': 'ACKNOWLEDGEMENT_STATE_PENDING',
            'productLineItem': [{'productId': PRODUCT, 'productOfferDetails': {
                'quantity': 1, 'refundableQuantity': 1,
                'consumptionState': 'CONSUMPTION_STATE_YET_TO_BE_CONSUMED'}}]}
        receipt = {'Store': 'GooglePlay', 'Payload': json.dumps({'json': json.dumps({
            'purchaseToken': 'synthetic-token', 'packageName': self.package, 'productId': PRODUCT})})}
        self.body = {'sessionTicket': 'synthetic-ticket', 'requestId': 'a' * 32,
                     'receipt': json.dumps(receipt)}
        self.calls = []
        self.verifier = Verifier(lambda ticket: self.account, self.store, Ledger(self.db),
                                 self.package, ['TEST-ACCOUNT-A', 'TEST-ACCOUNT-B'])

    def store(self, package, token):
        self.calls.append((package, token))
        return copy.deepcopy(self.purchase)

    def count(self):
        with closing(sqlite3.connect(self.db)) as db:
            return db.execute('SELECT count(*) FROM grants').fetchone()[0]

    def test_paid_test_purchase_is_durable_and_replay_is_idempotent(self):
        first = self.verifier.verify(self.body)
        self.verifier.ledger = Ledger(self.db)  # Reopen, simulating process restart.
        self.assertEqual(self.verifier.verify(self.body), first)
        self.assertEqual(self.count(), 1)
        self.assertEqual(len(self.calls), 2)  # Recheck refunds even on replay.
        self.assertNotIn('synthetic-token', self.db.read_bytes().decode(errors='ignore'))

    def test_confirmed_non_consumable_restores(self):
        self.purchase['acknowledgementState'] = 'ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED'
        self.assertTrue(self.verifier.verify(self.body)['verified'])

    def test_unpaid_cancelled_unknown_never_grant(self):
        for state in ['PENDING', 'CANCELLED', 'PURCHASE_STATE_UNSPECIFIED', None]:
            self.purchase['purchaseStateContext']['purchaseState'] = state
            with self.assertRaises(Rejected): self.verifier.verify(self.body)
        self.assertEqual(self.count(), 0)

    def test_binding_missing_or_wrong_never_grants(self):
        for binding in [None, '', account_binding('TEST-ACCOUNT-B')]:
            self.purchase['obfuscatedExternalAccountId'] = binding
            with self.assertRaises(Rejected): self.verifier.verify(self.body)
        self.assertEqual(self.count(), 0)

    def test_token_cannot_transfer_even_if_upstream_binding_changes(self):
        self.verifier.verify(self.body)
        self.account = 'TEST-ACCOUNT-B'
        self.purchase['obfuscatedExternalAccountId'] = account_binding(self.account)
        with self.assertRaisesRegex(Rejected, 'ownership_conflict'):
            self.verifier.verify(self.body)
        self.assertEqual(self.count(), 1)

    def test_concurrent_claims_have_exactly_one_owner(self):
        barrier = Barrier(2)
        def claim(account):
            barrier.wait()
            try:
                self.verifier.ledger.grant('same-token', account, self.package)
                return True
            except Rejected:
                return False
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(claim, ['TEST-ACCOUNT-A', 'TEST-ACCOUNT-B']))
        self.assertEqual(sorted(results), [False, True])
        self.assertEqual(self.count(), 1)

    def test_non_test_account_or_real_payment_rejected(self):
        self.account = 'NOT-ALLOWLISTED'
        with self.assertRaisesRegex(Rejected, 'test_account_required'): self.verifier.verify(self.body)
        self.assertEqual(self.calls, [])
        self.account = 'TEST-ACCOUNT-A'
        self.purchase.pop('testPurchaseContext')
        with self.assertRaisesRegex(Rejected, 'test_purchase_required'): self.verifier.verify(self.body)
        self.assertEqual(self.count(), 0)

    def test_wrong_product_mixed_cart_and_quantity_rejected(self):
        original = copy.deepcopy(self.purchase)
        for field, value in [('productId', 'wrong'), ('quantity', 2), ('refundableQuantity', 0),
                             ('consumptionState', 'CONSUMPTION_STATE_CONSUMED'), ('rentOfferDetails', {})]:
            self.purchase = copy.deepcopy(original)
            item = self.purchase['productLineItem'][0]
            (item if field == 'productId' else item['productOfferDetails'])[field] = value
            with self.assertRaises(Rejected): self.verifier.verify(self.body)
        self.purchase = original
        self.purchase['productLineItem'].append(copy.deepcopy(original['productLineItem'][0]))
        with self.assertRaises(Rejected): self.verifier.verify(self.body)
        self.assertEqual(self.count(), 0)

    def test_receipt_package_and_store_are_only_hints_not_authority(self):
        for receipt in ['', '{}', '[]', '{', json.dumps({'Store': 'AppleAppStore'}), 'x' * 32769]:
            with self.assertRaises(Rejected): self.verifier.verify(dict(self.body, receipt=receipt))
        body = copy.deepcopy(self.body)
        envelope = json.loads(body['receipt']); payload = json.loads(envelope['Payload'])
        purchase = json.loads(payload['json']); purchase['packageName'] = 'wrong.package'
        payload['json'] = json.dumps(purchase); envelope['Payload'] = json.dumps(payload)
        body['receipt'] = json.dumps(envelope)
        with self.assertRaises(Rejected): self.verifier.verify(body)
        self.assertEqual(self.calls, [])

    def test_server_auth_uses_ticket_and_rejects_expired_or_unknown(self):
        seen = []
        result = {'code': 200, 'data': {'IsSessionTicketExpired': False, 'UserInfo': {'PlayFabId': self.account}}}
        def request(*args): seen.append(args); return result
        upstream = Upstream('TESTTITLE', 'server-only-fake', lambda: 'synthetic-access', request)
        self.assertEqual(upstream.authenticate('synthetic-ticket'), self.account)
        self.assertEqual(seen[0][2], {'SessionTicket': 'synthetic-ticket'})
        for expired in [True, None]:
            result['data']['IsSessionTicketExpired'] = expired
            with self.assertRaises(Rejected): upstream.authenticate('synthetic-ticket')

    def test_google_read_path_encodes_token_and_never_acknowledges(self):
        seen = []
        upstream = Upstream('TESTTITLE', 'server-only-fake', lambda: 'synthetic-access',
                            lambda *args: seen.append(args) or {})
        upstream.get_purchase('example.test.game', 'a/b?c')
        self.assertTrue(seen[0][0].endswith('/productsv2/tokens/a%2Fb%3Fc'))
        self.assertEqual(len(seen[0]), 2)  # No request body => GET.

    def http(self, body):
        raw = json.dumps(body).encode(); statuses = []
        output = create_app(self.verifier)({'REQUEST_METHOD': 'POST', 'PATH_INFO': '/v1/no-ads/verify',
            'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)), 'wsgi.input': io.BytesIO(raw)},
            lambda status, headers: statuses.append(status))
        return statuses[0], json.loads(b''.join(output))

    def test_http_success_binds_nonce_and_authenticated_identity(self):
        status, result = self.http(dict(self.body, accountId='forged-client-account'))
        self.assertEqual(status, '200 OK')
        self.assertEqual(result['accountId'], self.account)
        self.assertEqual(result['requestId'], 'a' * 32)

    def test_upstream_or_database_failure_returns_no_verified_result_or_secret(self):
        for target in ['get_purchase', 'authenticate']:
            def fail(*args): raise RuntimeError('secret-ticket-and-token')
            saved = getattr(self.verifier, target); setattr(self.verifier, target, fail)
            status, result = self.http(self.body)
            self.assertEqual(status, '503 Service Unavailable')
            self.assertFalse(result['verified']); self.assertNotIn('secret', json.dumps(result))
            setattr(self.verifier, target, saved)
        self.verifier.ledger.path = str(Path(self.temp.name) / 'missing' / 'db')
        self.assertEqual(self.http(self.body)[0], '503 Service Unavailable')
        self.assertEqual(self.count(), 0)


if __name__ == '__main__': unittest.main()
