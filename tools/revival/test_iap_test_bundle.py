import unittest
import contextlib
import io
import json
import hashlib
import argparse
import stat
from types import SimpleNamespace
from pathlib import Path
import tempfile
from unittest.mock import patch
import zipfile
import verify_iap_test_bundle as bundle
from test_verify_native_alignment import elf_fixture
from verify_iap_test_bundle import validate_manifest

class IapBundleManifestTests(unittest.TestCase):
    xml = """<manifest xmlns:android="http://schemas.android.com/apk/res/android" android:versionCode="26" package="com.AeDeong.MonsterTamer.iaptest"><uses-sdk android:minSdkVersion="25" android:targetSdkVersion="36"/><uses-permission android:name="android.permission.INTERNET"/><uses-permission android:name="com.android.vending.BILLING"/><application android:debuggable="false"/></manifest>"""
    def test_isolated_release_manifest_is_accepted(self):
        self.assertIn("com.android.vending.BILLING", validate_manifest(self.xml))
    def test_operational_package_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace(".iaptest", ""))
    def test_debuggable_bundle_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('debuggable="false"', 'debuggable="true"'))
    def test_testonly_bundle_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('debuggable="false"', 'testOnly="true"'))
    def test_gms_ad_id_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace('</manifest>', '<uses-permission xmlns:android="http://schemas.android.com/apk/res/android" android:name="com.google.android.gms.permission.AD_ID"/></manifest>'))
    def test_missing_billing_is_rejected(self):
        with self.assertRaises(ValueError): validate_manifest(self.xml.replace("com.android.vending.BILLING", "unrelated"))
    def test_ads_provider_is_rejected(self):
        xml = self.xml.replace('<application android:debuggable="false"/>', '<application><provider android:name="com.google.android.gms.ads.MobileAdsInitProvider"/></application>')
        with self.assertRaises(ValueError): validate_manifest(xml)

class IapBundleNativeExitTests(unittest.TestCase):
    def check_bundle(self, alignment):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Build/revival').mkdir(parents=True)
            (root / 'Logs/revival').mkdir(parents=True)
            with zipfile.ZipFile(root / 'Build/revival/Tamer-iap-test.aab', 'w') as archive:
                archive.writestr('base/lib/arm64-v8a/libsynthetic.so',
                                 elf_fixture([dict(align=alignment)]))
            # Stub external signature/manifest tools only; inspect real synthetic ELF bytes.
            with patch.object(bundle, 'ROOT', root), \
                    patch.object(bundle, 'digest', return_value=bundle.SHA256), \
                    patch.object(bundle.subprocess, 'check_output', side_effect=[
                        '', IapBundleManifestTests.xml, '{"verifiedPayloadEntries": 1}']), \
                    contextlib.redirect_stdout(io.StringIO()):
                status = bundle.main([])
            report = json.loads((root / 'Logs/revival/iap-bundle-verification.json').read_text())
            native = json.loads((root / 'Logs/revival/iap-bundle-native.json').read_text())
            return status, report, native

    def test_load_failure_returns_failure_and_preserves_diagnostics(self):
        status, report, native = self.check_bundle(4096)
        self.assertEqual(status, 1)
        self.assertFalse(report['elfLoadChecksPassed'])
        self.assertTrue(native[0]['elf']['errors'])

    def test_load_success_keeps_relro_failure_separate(self):
        status, report, _ = self.check_bundle(16384)
        self.assertEqual(status, 0)
        self.assertTrue(report['elfLoadChecksPassed'])
        self.assertFalse(report['elfRelroChecksPassed'])


class IapRecoveryInputTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.folder = Path(self.directory.name)
        for name in ('test-upload.jks', 'password.dpapi', 'test-upload.der'):
            (self.folder / name).write_bytes(b'synthetic non-key fixture')
        self.marker = dict(applicationId='com.AeDeong.MonsterTamer.iaptest', alias='tamer-iap-test-upload',
                           keySha256=hashlib.sha256((self.folder / 'test-upload.jks').read_bytes()).hexdigest(),
                           certificateSha256=hashlib.sha256((self.folder / 'test-upload.der').read_bytes()).hexdigest())
        self.write_marker()

    def write_marker(self):
        (self.folder / 'test-only.json').write_text(json.dumps(self.marker), encoding='utf-8')

    def test_external_certificate_is_selected_without_changing_inputs(self):
        before = {p.name: p.read_bytes() for p in self.folder.iterdir()}
        self.assertEqual(bundle.signing_certificate(str(self.folder)), self.folder / 'test-upload.der')
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.folder.iterdir()})
        self.assertEqual(bundle.signing_certificate(None), bundle.ROOT / '.revival-local/iap-signing/test-upload.der')

    def test_relative_empty_and_incomplete_paths_are_rejected(self):
        for value in ('', 'relative', 'C:relative'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                bundle.signing_certificate(value)
        (self.folder / 'password.dpapi').unlink()
        with self.assertRaises(FileNotFoundError):
            bundle.signing_certificate(str(self.folder))

    def test_wrong_app_alias_and_changed_hash_are_rejected(self):
        for field, value in (('applicationId', 'com.AeDeong.MonsterTamer'), ('alias', 'other'),
                             ('keySha256', '0' * 64), ('certificateSha256', 'invalid')):
            original = self.marker[field]
            self.marker[field] = value
            self.write_marker()
            with self.subTest(field=field), self.assertRaises(ValueError):
                bundle.signing_certificate(str(self.folder))
            self.marker[field] = original

    def test_ancestor_and_leaf_reparse_attributes_are_rejected(self):
        original = Path.lstat
        for target in (self.folder.parent, self.folder / 'test-upload.der'):
            def observed(path):
                actual = original(path)
                return SimpleNamespace(st_mode=actual.st_mode, st_file_attributes=stat.FILE_ATTRIBUTE_REPARSE_POINT) if path == target else actual
            with self.subTest(target=target.name), patch.object(Path, 'lstat', autospec=True, side_effect=observed), self.assertRaises(ValueError):
                bundle.signing_certificate(str(self.folder))

    def test_explicit_version_matches_manifest_and_rejects_invalid_values(self):
        xml = IapBundleManifestTests.xml.replace('versionCode="26"', 'versionCode="27"')
        self.assertIn('com.android.vending.BILLING', validate_manifest(xml, 27))
        with self.assertRaises(ValueError):
            validate_manifest(xml, 28)
        for value in ('', '0', '-1', '27.0', ' 27', '2100000001'):
            with self.subTest(value=value), self.assertRaises(argparse.ArgumentTypeError):
                bundle.version_code(value)


if __name__ == "__main__": unittest.main()
