"""Loopback-only integration with synthetic providers; no external service calls."""
from contextlib import closing
import io
import json
import logging
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from server.receipts.config import Settings, ConfigurationError
from server.receipts import database
from server.receipts.runtime import application, make_server, SafeLogFilter
from server.receipts.verification import Ledger, Verifier, PRODUCT, account_binding


class ReceiptRuntimeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'secret').write_text('synthetic-secret')
        (self.root / 'credential').write_text('{}')
        self.env = {
            'TAMER_RECEIPT_ENV': 'staging', 'TAMER_RECEIPT_TITLE_ID': 'TEST123',
            'TAMER_RECEIPT_PRODUCTION_TITLE_ID': 'PROD123',
            'TAMER_RECEIPT_PACKAGE': 'example.tamer.iaptest',
            'TAMER_RECEIPT_TEST_ACCOUNTS': '["TEST-A"]',
            'TAMER_RECEIPT_PUBLIC_ORIGIN': 'https://receipt.example.test',
            'TAMER_RECEIPT_PORT': '8088', 'TAMER_RECEIPT_DATA_DIR': str(self.root),
            'TAMER_RECEIPT_PLAYFAB_SECRET_FILE': str(self.root / 'secret'),
            'TAMER_RECEIPT_GOOGLE_CREDENTIALS_FILE': str(self.root / 'credential')}
        self.settings = Settings.from_env(self.env)

    def test_config_rejects_prod_shared_title_wildcards_and_public_bind(self):
        bad = [('ENV', 'production'), ('TITLE_ID', 'PROD123'),
               ('PACKAGE', 'com.AeDeong.MonsterTamer'), ('TEST_ACCOUNTS', '["*"]'),
               ('TEST_ACCOUNTS', '[]'), ('TEST_ACCOUNTS', '["TEST-A","TEST-A"]'),
               ('BIND', '0.0.0.0'), ('PUBLIC_ORIGIN', 'http://receipt.example.test'),
               ('DATA_DIR', 'relative'), ('PORT', '0')]
        for key, value in bad:
            with self.subTest(key=key, value=value), self.assertRaises(ConfigurationError):
                Settings.from_env(dict(self.env, **{'TAMER_RECEIPT_' + key: value}))
        self.assertNotIn('synthetic-secret', repr(self.settings))

    def test_migration_adopts_legacy_ledger_and_rejects_unknown_version(self):
        self.assertFalse(database.ready(self.settings.database))
        Ledger(self.settings.database).grant('synthetic-token', 'TEST-A', self.settings.package)
        self.assertFalse(database.ready(self.settings.database))
        database.migrate(self.settings.database)
        database.migrate(self.settings.database)
        self.assertTrue(database.ready(self.settings.database))
        with closing(sqlite3.connect(self.settings.database)) as db, db:
            self.assertEqual(db.execute('SELECT count(*) FROM grants').fetchone()[0], 1)
            db.execute('PRAGMA user_version=999')
        with self.assertRaises(ValueError): database.migrate(self.settings.database)
        self.assertFalse(database.ready(self.settings.database))

    def test_backup_restore_preserves_grants_and_never_overwrites(self):
        database.migrate(self.settings.database)
        Ledger(self.settings.database).grant('synthetic-token', 'TEST-A', self.settings.package)
        backup, restored = self.root / 'backup.sqlite', self.root / 'restored.sqlite'
        database.backup(self.settings.database, backup)
        database.backup(backup, restored)
        self.assertTrue(database.ready(restored))
        before = backup.read_bytes()
        with self.assertRaises(FileExistsError): database.backup(self.settings.database, backup)
        self.assertEqual(before, backup.read_bytes())
        with closing(sqlite3.connect(restored)) as db:
            self.assertEqual(db.execute('SELECT account FROM grants').fetchone()[0], 'TEST-A')

    def test_wrong_schema_is_not_marked_ready_or_migrated(self):
        with closing(sqlite3.connect(self.settings.database)) as db:
            db.execute('CREATE TABLE grants (wrong TEXT)')
        with self.assertRaises(ValueError): database.migrate(self.settings.database)
        self.assertFalse(database.ready(self.settings.database))

    def test_log_filter_removes_messages_arguments_and_tracebacks(self):
        record = logging.LogRecord('test', logging.ERROR, 'file', 1, 'secret %s',
                                   ('synthetic-token',), (ValueError, ValueError('secret'), None))
        record.stack_info = 'secret-stack'
        SafeLogFilter().filter(record)
        self.assertEqual(record.getMessage(), 'receipt_service_event')
        self.assertIsNone(record.exc_info)
        self.assertIsNone(record.stack_info)

    def test_cli_preflight_migration_backup_and_sanitized_failure(self):
        def run(*args, environment=None):
            return subprocess.run([sys.executable, '-m', 'server.receipts', *args],
                cwd=Path(__file__).resolve().parents[3],
                env=dict(os.environ, **(environment or self.env)),
                capture_output=True, text=True, timeout=10)
        before = run('check')
        self.assertEqual(before.returncode, 0)
        self.assertFalse(json.loads(before.stdout)['databaseReady'])
        self.assertEqual(run('migrate').returncode, 0)
        after = json.loads(run('check').stdout)
        self.assertTrue(after['databaseReady'])
        self.assertFalse(after['upstreamVerified'])
        destination = str(self.root / 'cli-backup.sqlite')
        self.assertEqual(run('backup', '--destination', destination).returncode, 0)
        self.assertEqual(run('backup', '--destination', destination).returncode, 1)
        failed = run('serve', environment=dict(self.env, TAMER_RECEIPT_TITLE_ID='secret-invalid!'))
        self.assertEqual(failed.returncode, 1)
        self.assertNotIn('secret', failed.stdout + failed.stderr)
        self.assertNotIn('Traceback', failed.stdout + failed.stderr)

    def test_loopback_http_verify_replay_health_tls_body_limit_and_outage(self):
        database.migrate(self.settings.database)
        calls = []
        outage = [False]
        def authenticate(ticket):
            calls.append('authenticate')
            if outage[0]: raise RuntimeError('synthetic-secret-should-not-leak')
            return 'TEST-A' if ticket == 'synthetic-ticket' else 'UNKNOWN'
        def purchase(package, token):
            calls.append('google')
            return {'purchaseStateContext': {'purchaseState': 'PURCHASED'},
                'testPurchaseContext': {'fopType': 'TEST'},
                'obfuscatedExternalAccountId': account_binding('TEST-A'),
                'acknowledgementState': 'ACKNOWLEDGEMENT_STATE_PENDING',
                'productLineItem': [{'productId': PRODUCT, 'productOfferDetails': {
                    'quantity': 1, 'refundableQuantity': 1,
                    'consumptionState': 'CONSUMPTION_STATE_YET_TO_BE_CONSUMED'}}]}
        verifier = Verifier(authenticate, purchase, Ledger(self.settings.database),
                            self.settings.package, self.settings.accounts)
        app = application(self.settings, verifier, lambda: database.ready(self.settings.database))
        server = make_server(app, 0)  # OS-selected local port, never configured staging port.
        thread = threading.Thread(target=server.run, daemon=True)
        thread.start()
        def request(path, body=None, tls=True, host='receipt.example.test'):
            headers = {'Host': host, 'Content-Type': 'application/json'}
            if tls: headers['X-Forwarded-Proto'] = 'https'
            req = urllib.request.Request('http://127.0.0.1:' + str(server.effective_port) + path,
                data=body, headers=headers)
            try:
                with urllib.request.urlopen(req, timeout=5) as response:
                    return response.status, response.read()
            except urllib.error.HTTPError as error:
                with error: return error.code, error.read()
        try:
            self.assertEqual(request('/health/live')[0], 200)
            self.assertEqual(request('/health/ready')[0], 200)
            receipt = json.dumps({'Store': 'GooglePlay', 'Payload': json.dumps({'json': json.dumps({
                'purchaseToken': 'synthetic-token', 'packageName': self.settings.package, 'productId': PRODUCT})})})
            body = json.dumps({'requestId': 'a' * 32, 'sessionTicket': 'synthetic-ticket', 'receipt': receipt}).encode()
            self.assertEqual(request('/v1/no-ads/verify', body, tls=False)[0], 403)
            self.assertEqual(request('/v1/no-ads/verify', body, host='wrong.example')[0], 403)
            self.assertEqual(calls, [])
            for _ in range(2):
                status, response = request('/v1/no-ads/verify', body)
                self.assertEqual(status, 200)
                self.assertTrue(json.loads(response)['verified'])
            with closing(sqlite3.connect(self.settings.database)) as db:
                self.assertEqual(db.execute('SELECT count(*) FROM grants').fetchone()[0], 1)
            self.assertEqual(request('/v1/no-ads/verify', b'x' * 65537)[0], 413)
            status, response = request('/v1/no-ads/verify', body.replace(b'synthetic-ticket', b'unknown-ticket'))
            self.assertEqual(status, 403)
            outage[0] = True
            status, response = request('/v1/no-ads/verify', body)
            self.assertEqual(status, 503)
            self.assertNotIn(b'secret', response)
            with closing(sqlite3.connect(self.settings.database)) as db, db: db.execute('PRAGMA user_version=999')
            self.assertEqual(request('/health/ready')[0], 503)
            self.assertEqual(request('/health/live')[0], 200)
        finally:
            server.close()
            thread.join(timeout=5)
            self.assertFalse(thread.is_alive())


if __name__ == '__main__': unittest.main()

