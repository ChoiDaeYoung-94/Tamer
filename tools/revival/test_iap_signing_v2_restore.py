"""Synthetic ZIPs and fake child calls only; no keys, GPG, DPAPI or SDK execution."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

import restore_iap_signing_v2 as recovery


def fixture(metadata_change=None, secret_change=None, extra=None, attr=None, manifest_change=None):
    secret = {"storePassword": "SYNTHETIC-ONLY-" + "x" * 32,
              "aliasPassword": "SYNTHETIC-ONLY-" + "x" * 32}
    if secret_change:
        secret.update(secret_change)
    contents = {"keystore": b"synthetic non-key",
                "certificate-der": b"synthetic non-certificate DER",
                "certificate-pem": b"synthetic non-certificate PEM"}
    metadata = dict(applicationId=recovery.APP, alias=recovery.ALIAS, purpose=recovery.PURPOSE,
                    keySha256=recovery.sha(contents["keystore"]),
                    certificateSha256=recovery.sha(contents["certificate-der"]),
                    pemSha256=recovery.sha(contents["certificate-pem"]), dpapiSha256="0" * 64,
                    googleUploadCertificateActive=False, uploadReady=False)
    if metadata_change:
        metadata.update(metadata_change)
    contents["recovery-metadata"] = json.dumps(metadata).encode()
    contents["signing-secret"] = json.dumps(secret).encode()
    manifest = {role: {"sha256": recovery.sha(raw), "bytes": len(raw)} for role, raw in contents.items()}
    if manifest_change:
        manifest_change(manifest)
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w") as archive:
        for role, raw in contents.items():
            entry = zipfile.ZipInfo(role)
            if attr and role == "keystore":
                entry.external_attr = attr
            archive.writestr(entry, raw)
        archive.writestr("manifest.json", json.dumps(manifest).encode())
        if extra:
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", UserWarning)
                archive.writestr(extra, b"synthetic")
    return output.getvalue(), {"certificateSha256": recovery.sha(contents["certificate-der"]),
                               "generationMarkerSha256": recovery.sha(contents["recovery-metadata"])}


class V2FormatTests(unittest.TestCase):
    def test_exact_historical_six_entries_and_old_dpapi_hash_are_accepted(self):
        raw, binding = fixture()
        contents = recovery.validate_archive(raw, binding)
        try:
            self.assertEqual(set(contents), recovery.ROLES)
            metadata = recovery.json_object(contents["recovery-metadata"], 4096)
            self.assertFalse(metadata["googleUploadCertificateActive"])
            self.assertFalse(metadata["uploadReady"])
            self.assertEqual(metadata["dpapiSha256"], "0" * 64)
        finally:
            for data in contents.values():
                recovery.clear(data)

    def test_duplicate_extra_and_non_regular_zip_entries_are_rejected(self):
        for change in (dict(extra="keystore"), dict(extra="../unexpected"),
                       dict(attr=(stat.S_IFLNK | 0o777) << 16)):
            with self.subTest(change=tuple(change)):
                raw, binding = fixture(**change)
                with self.assertRaises(ValueError):
                    recovery.validate_archive(raw, binding)

    def test_changed_scope_secret_and_manifest_are_rejected(self):
        cases = [dict(metadata_change={field: value}) for field, value in (
            ("applicationId", "com.synthetic.other"), ("alias", "other"), ("purpose", "new-key"),
            ("keySha256", "0" * 64))]
        cases += [dict(secret_change={"aliasPassword": "different"}),
                  dict(secret_change={"storePassword": "short", "aliasPassword": "short"}),
                  dict(manifest_change=lambda m: m["keystore"].update(bytes=True)),
                  dict(manifest_change=lambda m: m["keystore"].update(sha256="0" * 64))]
        for case in cases:
            with self.subTest(case=tuple(case)):
                raw, binding = fixture(**case)
                with self.assertRaises(ValueError):
                    recovery.validate_archive(raw, binding)
        raw, binding = fixture()
        binding["certificateSha256"] = "0" * 64
        with self.assertRaises(ValueError):
            recovery.validate_archive(raw, binding)

    def test_json_duplicates_and_limits_fail_closed(self):
        with self.assertRaises(ValueError):
            recovery.json_object(b'{"storePassword":"a","storePassword":"b"}', 4096)
        raw, binding = fixture()
        with patch.object(recovery, "PLAIN_LIMIT", 1), self.assertRaises(ValueError):
            recovery.validate_archive(raw, binding)
        with patch.object(recovery, "ROLE_LIMIT", 1), self.assertRaises(ValueError):
            recovery.validate_archive(raw, binding)


class V2ExecutionGates(unittest.TestCase):
    def test_private_pipe_limits_and_stderr_are_safe_with_fake_children(self):
        class Child:
            pid = 12345
            returncode = 0
            def __init__(self, stdout, stderr=b''):
                self.stdin = io.BytesIO()
                self.stdout = io.BytesIO(stdout)
                self.stderr = io.BytesIO(stderr)
            def poll(self):
                return 0
        for stdout, stderr in ((b'x' * 33, b''), (b'', b'SYNTHETIC-PRIVATE' * 6000)):
            with self.subTest(outputLength=len(stdout)), patch.object(recovery.subprocess, 'Popen', return_value=Child(stdout, stderr)), self.assertRaises(ValueError) as rejected:
                recovery.safe_child('FAKE-NOT-EXECUTED', [], b'', 32, 1)
            self.assertNotIn('SYNTHETIC-PRIVATE', str(rejected.exception))

    def test_certificate_evidence_cannot_bind_another_app_or_app_signing_certificate(self):
        raw, binding = fixture()
        evidence = dict(schema='tamer-iap-upload-certificate-observation-1', applicationId=recovery.APP,
                        certificateType='upload', certificateSha256=binding['certificateSha256'],
                        observedAtUtc='2026-01-01T00:00:00Z')
        for field, value in (('applicationId', 'com.synthetic.other'), ('certificateType', 'app-signing'),
                             ('certificateSha256', '0' * 64), ('observedAtUtc', 'missing')):
            invalid = dict(evidence, **{field: value})
            with self.subTest(field=field), self.assertRaises(ValueError):
                recovery.validate_certificate_evidence(json.dumps(invalid).encode(), binding)

    def test_default_and_partial_approvals_never_open_inputs(self):
        with patch.object(recovery, "recover") as actual, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(recovery.main([]), 0)
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                recovery.main(["--execute-once", "--plan", "missing", "--plan-sha256", "0" * 64])
            actual.assert_not_called()

    def test_failed_fake_external_operation_never_prints_secret_exception(self):
        secret_exception = "SYNTHETIC-SENSITIVE-ERROR"
        output = io.StringIO()
        with patch.object(recovery, "recover", side_effect=RuntimeError(secret_exception)), contextlib.redirect_stdout(output):
            result = recovery.main(["--execute-once", "--approve-secret-restoration", "--plan", "missing", "--plan-sha256", "0" * 64])
        self.assertEqual(result, 1)
        self.assertNotIn(secret_exception, output.getvalue())
        self.assertFalse(json.loads(output.getvalue())["retry"])

    def test_reparse_and_destination_overwrite_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "fixture"
            path.write_bytes(b"synthetic-preserve")
            with self.assertRaises(FileExistsError):
                recovery.write_new(path, b"replace")
            self.assertEqual(path.read_bytes(), b"synthetic-preserve")
            original = Path.lstat
            def observe(item):
                actual = original(item)
                if item == path:
                    from types import SimpleNamespace
                    return SimpleNamespace(st_mode=actual.st_mode, st_nlink=1, st_file_attributes=0x400)
                return actual
            with patch.object(Path, "lstat", autospec=True, side_effect=observe), self.assertRaises(ValueError):
                recovery.regular(path)


class V2FakeRestorePipeline(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.parent = self.root / "TamerPrivateBackups"
        self.parent.mkdir()
        self.source = self.root / "source"
        self.source.mkdir()
        for name in recovery.SOURCE_NAMES:
            (self.source / name).write_bytes(b"synthetic reviewed source")
        self.raw, binding = fixture()
        self.plan = dict(schema="tamer-iap-v2-restore-plan-1", purpose=recovery.PURPOSE,
                         applicationId=recovery.APP, alias=recovery.ALIAS, binding=dict(binding,
                         authority="reviewed-google-play-test-upload-certificate"),
                         destination=dict(parent=str(self.parent), name="iap-v2-restore-" + "1" * 32),
                         sources={name: recovery.sha((self.source / name).read_bytes()) for name in recovery.SOURCE_NAMES}, tools={})
        evidence = json.dumps(dict(schema='tamer-iap-upload-certificate-observation-1',
            applicationId=recovery.APP, certificateType='upload',
            certificateSha256=binding['certificateSha256'], observedAtUtc='2026-01-01T00:00:00Z')).encode()
        for name, raw in (('ciphertext', b"synthetic encrypted fixture"),
                          ('certificateEvidence', evidence)):
            path = self.parent / name
            path.write_bytes(raw)
            self.plan[name] = dict(path=str(path), sha256=recovery.sha(raw))
        receipt = dict(verified=True, backupContentVerified=True, signingSecretIncluded=True,
                       roles=sorted(recovery.ROLES), encryptedSha256=self.plan['ciphertext']['sha256'],
                       generatedInputMarkerSha256=binding['generationMarkerSha256'])
        path = self.parent / "receipt"
        path.write_text(json.dumps(receipt))
        self.plan['receipt'] = dict(path=str(path), sha256=recovery.sha(path.read_bytes()))
        for name in ("python", "powershell", "gpg", "gpgconf", "pinentry"):
            path = self.root / name
            path.write_bytes(b"synthetic tool " + name.encode())
            self.plan['tools'][name] = dict(path=str(path), sha256=recovery.sha(path.read_bytes()))
        self.path = self.parent / "plan"
        self.path.write_text(json.dumps(self.plan))

    def run_fake(self, fail_protect=False):
        class Handle:
            def __init__(self, path, **_):
                self.path = Path(path)
                self.stream = self.path.open('rb')
            def bytes(self):
                return self.path.read_bytes()
            def __enter__(self):
                return self
            def __exit__(self, *_):
                self.stream.close()
        class NewHandle(Handle):
            def __init__(self, path, raw):
                recovery.write_new(Path(path), raw)
                super().__init__(path)
        self.calls = []
        self.decrypted = bytearray(self.raw)
        def external(executable, args, payload, limit, timeout=940):
            mode = args[args.index('-Mode') + 1]
            self.calls.append(mode)
            if mode == 'Decrypt':
                return self.decrypted
            if fail_protect:
                raise RuntimeError('SYNTHETIC-PRIVATE-CHILD-FAILURE')
            # Fake DPAPI/X509 adapter; not a claim about real Windows cryptography.
            return bytearray(json.dumps(dict(dpapi='0' * 64, certificateMatched=True,
                                            dpapiRoundtrip=True)).encode())
        with patch.object(Path, 'home', return_value=self.root), \
                patch.object(recovery, '__file__', str(self.source / 'restore_iap_signing_v2.py')), \
                patch.object(recovery.sys, 'executable', self.plan['tools']['python']['path']), \
                patch.object(recovery.owned, 'locked_directories', side_effect=lambda _: contextlib.nullcontext()), \
                patch.object(recovery.owned, 'LockedFile', Handle), \
                patch.object(recovery, 'NewOwnedFile', NewHandle), \
                patch.object(recovery, 'create_owner_directory', side_effect=lambda p, _: p.mkdir()), \
                patch.object(recovery, 'owner_sid', return_value='S-1-5-21-123'), \
                patch.object(recovery, 'owner_acl'), patch.object(recovery, 'safe_child', side_effect=external):
            return recovery.recover(self.path, recovery.sha(self.path.read_bytes()))

    def test_fake_valid_run_preserves_metadata_and_never_publishes_secret_or_ready(self):
        result = self.run_fake()
        output = self.parent / self.plan['destination']['name']
        self.assertEqual(self.calls, ['Decrypt', 'Protect'])
        self.assertTrue(result['restored'])
        self.assertFalse(result['uploadReady'])
        self.assertFalse(result['privateKeyUseVerified'])
        files = {p.name for p in output.iterdir()}
        self.assertEqual(files, set(recovery.OUTPUT_NAMES.values()) | {
            'password.dpapi', 'restore-attempt.private.json', 'restoration-receipt.private.json'})
        self.assertNotIn('signing-secret', files)
        self.assertNotIn('recovery.zip', files)
        self.assertFalse(any(self.decrypted), 'Consumed plaintext buffer must be cleared')
        restored = json.loads((output / 'test-only.json').read_text())
        self.assertEqual(restored['dpapiSha256'], '0' * 64)
        self.assertFalse(restored['googleUploadCertificateActive'])
        # New destination name cannot rerun the same approved ciphertext.
        self.plan['destination']['name'] = 'iap-v2-restore-' + '2' * 32
        self.path.write_text(json.dumps(self.plan))
        with self.assertRaises(FileExistsError):
            self.run_fake()
        self.assertEqual(self.calls, [])

    def test_fake_protection_failure_keeps_originals_and_no_success_or_key_files(self):
        original = {name: Path(self.plan[name]['path']).read_bytes()
                    for name in ('ciphertext', 'receipt', 'certificateEvidence')}
        with self.assertRaises(ValueError):
            self.run_fake(fail_protect=True)
        output = self.parent / self.plan['destination']['name']
        self.assertTrue((output / 'restoration-failed.private.json').is_file())
        self.assertFalse((output / 'restoration-receipt.private.json').exists())
        self.assertFalse((output / 'test-upload.jks').exists())
        self.assertFalse(any(self.decrypted))
        for name, raw in original.items():
            self.assertEqual(Path(self.plan[name]['path']).read_bytes(), raw)


if __name__ == "__main__":
    unittest.main()
