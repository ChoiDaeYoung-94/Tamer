import unittest
import contextlib
import io
import json
from pathlib import Path
import tempfile
from unittest.mock import patch
import zipfile
import verify_iap_test_bundle as bundle
from test_verify_native_alignment import elf_fixture
from verify_iap_test_bundle import validate_manifest

class IapBundleManifestTests(unittest.TestCase):
    xml = """<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.AeDeong.MonsterTamer.iaptest"><uses-sdk android:minSdkVersion="24" android:targetSdkVersion="36"/><uses-permission android:name="android.permission.INTERNET"/><uses-permission android:name="com.android.vending.BILLING"/><application android:debuggable="false"/></manifest>"""
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
                status = bundle.main()
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


if __name__ == "__main__": unittest.main()
