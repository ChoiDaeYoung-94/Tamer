"""Undeployed Google Play No Ads verifier. No network calls on import.

The host supplies authenticated upstream adapters and a durable ledger. Receipts,
session tickets, upstream bodies and credentials must never be logged.
"""
import hashlib
import json
import sqlite3
from contextlib import contextmanager

PRODUCT = 'com.aedeong.monstertamer.no_ads'


class Rejected(Exception):
    """Public, non-sensitive rejection code only."""


def account_binding(account):
    return hashlib.sha256(('tamer-iap-v1:' + account).encode()).hexdigest()


def parse_receipt(receipt, package):
    try:
        if not isinstance(receipt, str) or len(receipt) > 32768:
            raise ValueError()
        envelope = json.loads(receipt)
        if envelope['Store'] != 'GooglePlay':
            raise ValueError()
        payload = json.loads(envelope['Payload'])
        purchase = json.loads(payload['json'])
        token = purchase['purchaseToken']
        if (purchase['packageName'] != package or purchase['productId'] != PRODUCT
                or not isinstance(token, str) or not 1 <= len(token) <= 4096):
            raise ValueError()
        return token
    except (ValueError, TypeError, KeyError):
        raise Rejected('invalid_receipt') from None


class Ledger:
    """One local durable SQLite store; multi-host deployment needs a shared DB.

    A token has one owner, bound in the same transaction as its entitlement.
    Only hashes of tokens are retained; no tickets or raw receipt payloads.
    """
    def __init__(self, path):
        self.path = str(path)
        with self.connect() as db:
            db.execute('CREATE TABLE IF NOT EXISTS grants ('
                       'token_hash TEXT PRIMARY KEY, account TEXT NOT NULL, '
                       'product TEXT NOT NULL, package TEXT NOT NULL)')

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.path, timeout=10)
        try:
            db.execute('PRAGMA synchronous=FULL')
            with db:
                yield db
        finally:
            db.close()

    def grant(self, token, account, package):
        digest = hashlib.sha256(token.encode()).hexdigest()
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            existing = db.execute('SELECT account, product, package FROM grants WHERE token_hash=?',
                                  (digest,)).fetchone()
            if existing and existing != (account, PRODUCT, package):
                raise Rejected('ownership_conflict')
            db.execute('INSERT OR IGNORE INTO grants VALUES (?, ?, ?, ?)',
                       (digest, account, PRODUCT, package))
        return digest


class Verifier:
    def __init__(self, authenticate, get_purchase, ledger, package, test_accounts):
        # This first rollout accepts explicitly listed test accounts and test-card
        # purchases only. Production enablement requires a separate reviewed change.
        self.authenticate = authenticate
        self.get_purchase = get_purchase
        self.ledger = ledger
        self.package = package
        self.test_accounts = frozenset(test_accounts)

    def verify(self, body):
        if not isinstance(body, dict):
            raise Rejected('invalid_request')
        ticket = body.get('sessionTicket')
        nonce = body.get('requestId')
        if (not isinstance(ticket, str) or not 1 <= len(ticket) <= 8192 or
                not isinstance(nonce, str) or len(nonce) != 32 or
                any(c not in '0123456789abcdef' for c in nonce)):
            raise Rejected('invalid_request')
        # Identity comes from PlayFab, never the client-provided account id.
        account = self.authenticate(ticket)
        if account not in self.test_accounts:
            raise Rejected('test_account_required')
        token = parse_receipt(body.get('receipt'), self.package)
        purchase = self.get_purchase(self.package, token)
        if not isinstance(purchase, dict):
            raise Rejected('invalid_store_response')
        if purchase.get('purchaseStateContext', {}).get('purchaseState') != 'PURCHASED':
            raise Rejected('purchase_not_completed')
        if purchase.get('testPurchaseContext', {}).get('fopType') != 'TEST':
            raise Rejected('test_purchase_required')
        if purchase.get('obfuscatedExternalAccountId') != account_binding(account):
            # Historic unbound purchases need a separately reviewed migration. Do
            # not let the first presenter claim someone else's historic receipt.
            raise Rejected('account_binding_required')
        items = purchase.get('productLineItem')
        if not isinstance(items, list) or len(items) != 1 or items[0].get('productId') != PRODUCT:
            raise Rejected('product_mismatch')
        offer = items[0].get('productOfferDetails', {})
        if (offer.get('quantity') != 1 or offer.get('refundableQuantity') != 1 or
                offer.get('consumptionState') != 'CONSUMPTION_STATE_YET_TO_BE_CONSUMED' or
                'rentOfferDetails' in offer):
            raise Rejected('unsupported_purchase')
        if purchase.get('acknowledgementState') not in (
                'ACKNOWLEDGEMENT_STATE_PENDING', 'ACKNOWLEDGEMENT_STATE_ACKNOWLEDGED'):
            raise Rejected('invalid_store_response')
        digest = self.ledger.grant(token, account, self.package)
        return dict(verified=True, requestId=nonce, accountId=account,
                    productId=PRODUCT, tokenSha256=digest)
