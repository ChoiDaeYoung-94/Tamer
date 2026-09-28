"""Offline APK-verifier boundary checks with synthetic identities and tool receipts."""
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import verify_ad_harness as verifier


class PublisherUmpVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.config = self.root / 'Logs/revival/production-ads-prepared/production-ads.private.json'
        self.config.parent.mkdir(parents=True)
        self.app = 'ca-app-pub-1234567890123456~1234567890'
        self.values = dict(checkout=str(self.root), androidAppId=self.app,
                           productionActivationApproved=False, regionalReviewApproved=False)
        self.config.write_text(json.dumps(self.values), encoding='utf-8')
        self.apk = self.root / 'test.apk'
        self.apk.write_bytes(b'synthetic apk; no native execution')
        self.root_patch = patch.object(verifier, 'ROOT', self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)

    def receipt(self, variant, manifest):
        package = 'umppublisher' if variant == 'ump-publisher' else 'ads'
        badging = (f"package: name='com.AeDeong.MonsterTamer.revival.{package}' versionCode='26' versionName='1.0.5'\n"
                   "sdkVersion:'25'\ntargetSdkVersion:'36'\nnative-code: 'arm64-v8a'\napplication-debuggable")
        with patch.object(verifier.subprocess, 'check_output', side_effect=[badging, manifest, 'CN=Android Debug']):
            return verifier.verify(self.apk, self.root, variant)

    def manifest(self, app):
        return f'E: manifest\n E: meta-data\n com.google.android.gms.ads.APPLICATION_ID\n android:value="{app}"\n'

    def test_publisher_receipt_matches_private_id_without_disclosing_it(self):
        result = self.receipt('ump-publisher', self.manifest(self.app))
        self.assertTrue(result['publisherAppIdVerified'])
        self.assertFalse(result['sampleAppIdVerified'])
        self.assertNotIn(self.app, json.dumps(result))

    def test_existing_sample_receipt_remains_sample(self):
        result = self.receipt('sample', self.manifest(verifier.expected_app_id('sample')))
        self.assertTrue(result['sampleAppIdVerified'])
        self.assertFalse(result['publisherAppIdVerified'])

    def test_publisher_rejects_sample_or_duplicate_manifest_inventory(self):
        for manifest in [self.manifest(verifier.expected_app_id('sample')),
                         self.manifest(self.app) + self.manifest(self.app)]:
            with self.subTest(manifest_case='sample-or-duplicate'), self.assertRaises(ValueError):
                self.receipt('ump-publisher', manifest)

    def test_private_config_rejects_missing_duplicate_and_wrong_checkout(self):
        cases = ['{}', json.dumps(self.values)[:-1] + ',"androidAppId":"other"}',
                 json.dumps(dict(self.values, checkout=str(self.root / 'other')))]
        for data in cases:
            with self.subTest(config_case='missing-duplicate-checkout'), self.assertRaises(ValueError):
                self.config.write_text(data, encoding='utf-8')
                verifier.expected_app_id('ump-publisher')


if __name__ == '__main__':
    unittest.main()
