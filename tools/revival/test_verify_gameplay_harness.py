import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from verify_gameplay_harness import verify


class GameplayVariantVerificationTests(unittest.TestCase):
    def check_variant(self, requested, debuggable, manifest=''):
        badging = "package: name='com.AeDeong.MonsterTamer.revival.gameplay' versionCode='26' versionName='1.0.5'\n" \
            "sdkVersion:'24'\ntargetSdkVersion:'36'\nnative-code: 'arm64-v8a'\n"
        if debuggable:
            badging += 'application-debuggable\n'
        with tempfile.TemporaryDirectory() as temporary:
            apk = Path(temporary) / 'fixture.apk'
            apk.write_bytes(b'synthetic APK fixture')
            with patch('verify_gameplay_harness.subprocess.check_output',
                       side_effect=[badging, manifest, 'Signer #1 certificate DN: CN=Android Debug']):
                return verify(apk, Path(temporary), requested)

    def test_photo_requires_nondevelopment_and_normal_requires_development(self):
        self.assertFalse(self.check_variant('photo', False)['debuggable'])
        self.assertTrue(self.check_variant('development', True)['debuggable'])
        for requested, debuggable in [('photo', True), ('development', False)]:
            with self.subTest(requested=requested):
                with self.assertRaisesRegex(ValueError, 'variant mismatch'):
                    self.check_variant(requested, debuggable)

    def test_photo_does_not_relax_offline_and_purchase_manifest_guards(self):
        for forbidden in ['android.permission.INTERNET', 'android.permission.ACCESS_NETWORK_STATE',
                          'com.android.vending.BILLING', 'com.google.android.gms.permission.AD_ID',
                          'com.google.android.gms.ads.MobileAdsInitProvider']:
            with self.subTest(forbidden=forbidden):
                with self.assertRaisesRegex(ValueError, 'forbidden permission/provider'):
                    self.check_variant('photo', False, forbidden)


if __name__ == '__main__':
    unittest.main()
