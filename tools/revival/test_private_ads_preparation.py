import json
from pathlib import Path
import tempfile
import unittest

from validate_private_ads_preparation import read_contract


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


if __name__ == '__main__':
    unittest.main()
