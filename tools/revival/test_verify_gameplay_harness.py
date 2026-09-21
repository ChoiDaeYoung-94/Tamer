import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from verify_gameplay_harness import verify


class GameplayVariantVerificationTests(unittest.TestCase):
    def check_variant(self, requested, debuggable, manifest='', rules=''):
        package = requested if requested in ('playerrestore', 'agechoice') else 'gameplay'
        badging = f"package: name='com.AeDeong.MonsterTamer.revival.{package}' versionCode='26' versionName='1.0.5'\n" \
            "sdkVersion:'24'\ntargetSdkVersion:'36'\nnative-code: 'arm64-v8a'\n"
        if debuggable:
            badging += 'application-debuggable\n'
        with tempfile.TemporaryDirectory() as temporary:
            apk = Path(temporary) / 'fixture.apk'
            apk.write_bytes(b'synthetic APK fixture')
            with patch('verify_gameplay_harness.subprocess.check_output',
                       side_effect=[badging, manifest, 'Signer #1 certificate DN: CN=Android Debug', rules]):
                return verify(apk, Path(temporary), requested)

    def test_playerrestore_requires_backup_and_transfer_exclusion(self):
        manifest = ('A: android:allowBackup(0x1)=(type 0x12)0x0\n'
                    'A: android:fullBackupContent(0x2)=(type 0x12)0x0\n'
                    'A: android:dataExtractionRules(0x3)=@0x1\n')
        rules = 'E: cloud-backup\nE: device-transfer\n' + 'E: exclude\n' * 18
        self.assertTrue(self.check_variant('playerrestore', True, manifest, rules)['debuggable'])
        self.assertTrue(self.check_variant('playerrestore', True,
            manifest.replace('(type 0x12)0x0', 'false'), rules)['debuggable'])
        with self.assertRaisesRegex(ValueError, 'Backup must be disabled'):
            self.check_variant('playerrestore', True, manifest.replace('0x0', '0xffffffff'), rules)
        with self.assertRaisesRegex(ValueError, 'exclusions'):
            self.check_variant('playerrestore', True, manifest, 'E: cloud-backup')

    def test_agechoice_requires_offline_and_backup_exclusions(self):
        manifest = 'A: android:allowBackup=false\nA: android:fullBackupContent=false\nA: android:dataExtractionRules=@0x1\n'
        rules = 'E: cloud-backup\nE: device-transfer\n' + 'E: exclude\n' * 18
        result = self.check_variant('agechoice', True, manifest, rules)
        self.assertTrue(result['applicationId'].endswith('.agechoice'))
        with self.assertRaisesRegex(ValueError, 'Backup must be disabled'):
            self.check_variant('agechoice', True)
        with self.assertRaisesRegex(ValueError, 'forbidden permission/provider'):
            self.check_variant('agechoice', True, manifest + 'android.permission.INTERNET', rules)

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
