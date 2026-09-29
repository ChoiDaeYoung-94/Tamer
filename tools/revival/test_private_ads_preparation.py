import json
from pathlib import Path
import tempfile
import unittest

from validate_private_ads_preparation import read_contract, audit


class PrivateAdsContractTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.gettempdir()).resolve()
        self.config = dict(checkout=str(self.root),
                           androidAppId='ca-app-pub-1111111111111111~2222222222',
                           productionRewardedAdUnit='ca-app-pub-1111111111111111/3333333333',
                           consoleInventoryConfirmed=True,
                           productionActivationApproved=False, regionalReviewApproved=False)

    def test_valid_disabled_contract(self):
        self.assertEqual(read_contract(json.dumps(self.config), self.root), self.config)

    def test_required_booleans_reject_missing_wrong_type_or_value(self):
        for key in ('consoleInventoryConfirmed', 'productionActivationApproved', 'regionalReviewApproved'):
            for value in (None, 0, 1, 'false', {}, not self.config[key]):
                with self.subTest(key=key, value=value):
                    candidate = dict(self.config, **{key: value})
                    with self.assertRaises(ValueError):
                        read_contract(json.dumps(candidate), self.root)
            candidate = dict(self.config)
            del candidate[key]
            with self.assertRaises(ValueError):
                read_contract(json.dumps(candidate), self.root)

    def test_duplicate_decoded_keys_including_nested_objects_rejected(self):
        for extra in ('"productionActivationApproved":false',
                      '"productionActivationApprov\\u0065d":false',
                      '"evidence":{"x":1,"x":2}'):
            with self.subTest(extra=extra), self.assertRaises(ValueError):
                read_contract(json.dumps(self.config)[:-1] + ',' + extra + '}', self.root)

    def test_nested_or_textual_keys_cannot_supply_root_approval(self):
        candidate = dict(self.config)
        del candidate['productionActivationApproved']
        candidate['evidence'] = {'productionActivationApproved': False}
        candidate['note'] = '"productionActivationApproved":false'
        with self.assertRaises(ValueError):
            read_contract(json.dumps(candidate), self.root)

    def test_wrong_checkout_sample_and_cross_publisher_rejected(self):
        for changes in ({'checkout': '.'}, {'checkout': str(self.root / 'other')},
                        {'androidAppId': 'ca-app-pub-3940256099942544~2222222222'},
                        {'productionRewardedAdUnit': 'ca-app-pub-4444444444444444/3333333333'}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                read_contract(json.dumps(dict(self.config, **changes)), self.root)


class PrivateAdsSourceAuditTests(unittest.TestCase):
    def test_audit_rejects_false_declarations_in_comments_strings_and_branches(self):
        declaration = 'public const bool ProductionAdsEnabled = false;'
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            config = dict(checkout=str(root.resolve()),
                          androidAppId='ca-app-pub-1111111111111111~2222222222',
                          productionRewardedAdUnit='ca-app-pub-1111111111111111/3333333333',
                          consoleInventoryConfirmed=True, productionActivationApproved=False,
                          regionalReviewApproved=False)
            config_path = root / 'private.json'
            config_path.write_text(json.dumps(config), encoding='utf-8')
            files = {
                'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset':
                    'adMobAndroidAppId: ' + config['androidAppId'],
                'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml':
                    '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application>'
                    '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="'
                    + config['androidAppId'] + '"/></application></manifest>',
                'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs':
                    'public static bool RegionalConsentReviewed => false;\n' + '\n'.join(
                        'private const bool ' + cohort + 'ConsentReviewed = false;'
                        for cohort in ('Under13', 'From13To15', 'From16To17', 'Adult'))}
            for name, content in files.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding='utf-8')
            policy = root / 'Assets/Scripts/Advertising/AdRequestPolicy.cs'
            for source in (declaration, '#if UNUSED\nint other;\n#endif\n' + declaration):
                policy.write_text(source, encoding='utf-8')
                self.assertTrue(audit(root, config_path)['disabledPreparationValid'])
            invalid = [declaration.replace('false', 'true') + '\n/*\n' + declaration + '\n*/',
                       '/*\n' + declaration + '\n*/',
                       'string example = @"\n' + declaration + '\n";',
                       '', declaration + '\n' + declaration,
                       '#if NEVER\n' + declaration + '\n#endif',
                       '/*\n' + declaration]
            for source in invalid:
                with self.subTest(source=source), self.assertRaises(ValueError):
                    policy.write_text(source, encoding='utf-8')
                    audit(root, config_path)


if __name__ == '__main__':
    unittest.main()
