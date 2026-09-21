"""Offline bootstrap checks: synthetic credentials, fake upstream, no real sockets."""
from contextlib import closing, redirect_stdout
import hashlib
import io
import json
import logging
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import patch

from server.privacy.config import ConfigurationError, Settings
from server.privacy.runtime import build, initialize, instance_lock, make_server, SafeLogFilter
from server.privacy.__main__ import main


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        # Resolve the OS temp location before the CLI test isolates its environment.
        # Otherwise Windows tempfile falls back to cwd and caches the repo as temp.
        tempfile.gettempdir()
        root = Path(__file__).resolve().parents[2] / 'Logs' / 'revival'
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='privacy-runtime-', dir=root)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.env = {'TAMER_DELETION_' + k: v for k, v in {
            'TITLE_ID': 'ABC12', 'PLAYFAB_SECRET': 'synthetic-secret',
            'PUBLIC_ORIGIN': 'https://deletion.example.invalid/', 'POLICY_REVISION': 'synthetic-v1',
            'SCOPE': 'title', 'RETENTION_PLAN': '["synthetic-test-only"]',
            'RECEIPT_TTL_SECONDS': '600', 'PORT': '8767',
            'DATA_DIR': str(self.root), 'STORAGE_KIND': 'local-persistent',
        }.items()}
        self.calls = []
        guard = patch('urllib.request.OpenerDirector.open', side_effect=AssertionError('Real network forbidden'))
        guard.start()
        self.addCleanup(guard.stop)

    def settings(self, active=False):
        env = dict(self.env)
        if active:
            for key in ('ENABLED', 'RETENTION_APPROVED', 'REJOIN_APPROVED', 'SESSION_CONFIRMATION_ENABLED'):
                env['TAMER_DELETION_' + key] = 'true'
        return Settings.from_env(env)

    def upstream(self, url, headers, body):
        self.assertEqual(headers['X-SecretKey'], 'synthetic-secret')
        route = url.rsplit('/', 1)[-1]
        self.calls.append(route)
        info = {'PlayFabId': 'synthetic-account', 'TitleInfo': {
            'TitlePlayerAccount': {'Id': 'synthetic-entity', 'Type': 'title_player_account'}},
            'CustomIdInfo': {'CustomId': 'synthetic-device'}}
        if getattr(self, 'pgs', False):
            del info['CustomIdInfo']
            info['GooglePlayGamesInfo'] = {'GooglePlayGamesPlayerId': 'synthetic-pgs-player'}
        data = ({'IsSessionTicketExpired': False, 'UserInfo': info} if route == 'AuthenticateSessionTicket'
                else {'UserInfo': info} if route == 'GetUserAccountInfo' else {})
        return {'code': 200, 'status': 'OK', 'data': data}

    def call(self, app, path, body=None, **overrides):
        raw = json.dumps(body).encode() if body is not None else b''
        env = {'REQUEST_METHOD': 'POST' if body is not None else 'GET', 'PATH_INFO': path,
               'wsgi.url_scheme': 'https', 'HTTP_HOST': 'deletion.example.invalid',
               'CONTENT_TYPE': 'application/json', 'CONTENT_LENGTH': str(len(raw)), 'wsgi.input': io.BytesIO(raw)}
        env.update(overrides)
        status = []
        result = b''.join(app(env, lambda s, h: status.append(s)))
        self.assertNotIn(b'synthetic-secret', result)
        return status[0], json.loads(result)

    def test_disabled_default_denies_post_without_upstream(self):
        settings = self.settings()
        initialize(settings, self.upstream)
        app = build(settings, self.upstream)
        self.assertFalse(self.call(app, '/v1/deletion/config')[1]['available'])
        for path in ('session-challenge', 'request', 'receipt-status'):
            self.assertEqual(self.call(app, '/v1/deletion/' + path, {})[0], '503 Service Unavailable')
        self.assertEqual(self.calls, [])

    def test_enabled_flow_and_restart_preserve_database_and_receipt(self):
        settings = self.settings(True)
        initialize(settings, self.upstream)
        app = build(settings, self.upstream)
        self.assertTrue(self.call(app, '/v1/deletion/config')[1]['available'])
        prefix = '/v1/deletion/'
        nonce = self.call(app, prefix + 'session-challenge',
                          {'sessionTicket': 'synthetic-ticket', 'clientKey': 'a' * 32})[1]['nonce']
        proof = self.call(app, prefix + 'session-confirm',
                          {'sessionTicket': 'synthetic-ticket', 'nonce': nonce, 'confirmed': True})[1]['proof']
        request = self.call(app, prefix + 'request', {'proof': proof, 'clientKey': 'a' * 32})[1]
        capability = 'b' * 64
        verifier = hashlib.sha256(capability.encode()).hexdigest()
        status, _ = self.call(app, prefix + 'receipt-register',
                              {'proof': proof, 'requestId': request['requestId'], 'verifier': verifier})
        self.assertEqual(status, '200 OK')
        status, accepted = self.call(app, prefix + 'confirm', {'proof': proof,
            'requestId': request['requestId'], 'challenge': request['challenge'], 'policyRevision': 'synthetic-v1'})
        self.assertEqual(status, '200 OK')
        self.assertEqual(accepted['submissionState'], 'accepted')
        def snapshot():
            with closing(sqlite3.connect(settings.database)) as db:
                return list(db.iterdump())
        before, calls = snapshot(), list(self.calls)
        app = build(settings, self.upstream)
        self.assertEqual(snapshot(), before)
        status, receipt = self.call(app, prefix + 'receipt-status',
                                    {'requestId': request['requestId'], 'capability': capability})
        self.assertEqual(status, '200 OK')
        self.assertEqual(receipt['submissionState'], 'accepted')
        self.assertEqual(self.calls, calls)
        self.assertEqual(self.calls.count('DeletePlayer'), 1)  # Fake transport only.

    def test_pgs_runtime_opt_in_validation_and_accepted_receipt_restart(self):
        self.assertFalse(self.settings(True).policy.google_play_games_session_confirmation_enabled)
        self.env['TAMER_DELETION_GOOGLE_PLAY_GAMES_SESSION_CONFIRMATION_ENABLED'] = '1'
        with self.assertRaises(ConfigurationError): self.settings(True)
        self.env['TAMER_DELETION_GOOGLE_PLAY_GAMES_SESSION_CONFIRMATION_ENABLED'] = 'true'
        self.pgs = True
        self.test_enabled_flow_and_restart_preserve_database_and_receipt()

    def test_invalid_or_missing_configuration_never_creates_database(self):
        for key in self.env:
            with self.subTest(missing=key):
                env = dict(self.env)
                del env[key]
                with self.assertRaises(ConfigurationError): Settings.from_env(env)
        changes = {'TITLE_ID': ['a', 'https://secret.invalid'], 'PLAYFAB_SECRET': [' ', 'secret\r\nvalue'],
            'PUBLIC_ORIGIN': ['http://host/', 'https://user:secret@host/', 'https://host:bad/',
                              'https://host:99999/', 'https://host/path', 'https://host/?secret=1'],
            'POLICY_REVISION': [' ', 'x' * 129], 'SCOPE': ['master'], 'ENABLED': ['1', 'true'],
            'RETENTION_PLAN': ['[]', '{}', '[null]'], 'RECEIPT_TTL_SECONDS': ['0', '2592001'],
            'DATA_DIR': ['relative', str(self.root / 'missing'), tempfile.gettempdir()],
            'STORAGE_KIND': ['ephemeral'], 'PORT': ['0', '65536']}
        for key, values in changes.items():
            for value in values:
                with self.subTest(invalid=key):
                    env = dict(self.env, **{'TAMER_DELETION_' + key: value})
                    with self.assertRaises(ConfigurationError) as error: Settings.from_env(env)
                    self.assertNotIn('synthetic-secret', str(error.exception))
        self.assertFalse((self.root / 'deletion.sqlite').exists())
        self.assertEqual(self.calls, [])

    def test_missing_corrupt_or_wrong_schema_database_is_not_recreated(self):
        settings = self.settings()
        with self.assertRaises(sqlite3.Error): build(settings, self.upstream)
        self.assertFalse(settings.database.exists())
        settings.database.write_bytes(b'preserve-corrupt-evidence')
        with self.assertRaises(sqlite3.Error): build(settings, self.upstream)
        self.assertEqual(settings.database.read_bytes(), b'preserve-corrupt-evidence')
        with self.assertRaises(FileExistsError): initialize(settings, self.upstream)
        wrong = self.root / 'other'
        wrong.mkdir()
        env = dict(self.env, TAMER_DELETION_DATA_DIR=str(wrong))
        settings = Settings.from_env(env)
        with closing(sqlite3.connect(settings.database)) as db:
            db.execute('CREATE TABLE existing_data (value TEXT)')
            db.commit()
        before = settings.database.read_bytes()
        with self.assertRaises(ValueError): build(settings, self.upstream)
        self.assertEqual(settings.database.read_bytes(), before)
        self.assertEqual(self.calls, [])

    def test_transport_boundary_and_single_instance(self):
        settings = self.settings()
        initialize(settings, self.upstream)
        app = build(settings, self.upstream)
        for change in ({'wsgi.url_scheme': 'http'}, {'HTTP_HOST': 'attacker.invalid'}):
            self.assertEqual(self.call(app, '/v1/deletion/config', **change)[0], '403 Forbidden')
        with instance_lock(settings.database):
            with self.assertRaises(OSError):
                with instance_lock(settings.database): self.fail('Second instance obtained lock')
        with instance_lock(settings.database): pass
        with patch('waitress.server.create_server') as create:
            make_server(app, settings.port)
            self.assertEqual(create.call_args.kwargs['threads'], 1)
            self.assertEqual(create.call_args.kwargs['host'], '127.0.0.1')
            self.assertFalse(create.call_args.kwargs['expose_tracebacks'])

    def test_cli_and_logger_hide_unexpected_secret_failures(self):
        output = io.StringIO()
        with patch.dict('os.environ', self.env, clear=True), redirect_stdout(output):
            self.assertEqual(main(['init-db']), 0)
            self.assertEqual(main(['check']), 0)
            with patch('server.privacy.__main__.check_database', side_effect=ValueError('synthetic-secret')):
                self.assertEqual(main(['check']), 1)
        self.assertNotIn('synthetic-secret', output.getvalue())
        self.assertNotIn(str(self.root), output.getvalue())
        record = logging.LogRecord('waitress', logging.ERROR, '', 1, 'synthetic-secret %s', ('token',),
                                   (ValueError, ValueError('synthetic-secret'), None))
        SafeLogFilter().filter(record)
        self.assertEqual(record.getMessage(), 'deletion_service_event')
        self.assertIsNone(record.exc_info)
        self.assertEqual(self.calls, [])


if __name__ == '__main__': unittest.main()
