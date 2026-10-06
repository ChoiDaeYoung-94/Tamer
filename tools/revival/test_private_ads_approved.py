"""New producer mode/consumer source boundary tests; no Unity or signing."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import private_ads_approved as approved
import private_ads_producer as producer
import private_ads_build as base

ROOT = Path(__file__).resolve().parents[2]

def fixture():
    value = {'schema': 1, 'mode': 'adult_only_release', 'packageId': 'com.synthetic.contract',
        'androidAppId': 'ca-app-pub-1111111111111111~2222222222',
        'productionRewardedAdUnit': 'ca-app-pub-1111111111111111/3333333333',
        'countryCodes': 'KR;US', 'sourceHead': 'a' * 40}
    value.update({k: 'b' * 64 for k in approved.REFERENCES})
    value.update({k: True for k in approved.TRUE_FLAGS})
    value.update({k: False for k in approved.FALSE_FLAGS})
    return value

class ApprovedModeTests(unittest.TestCase):
    def test_callback_receipt_cannot_cross_accept_producer_mode(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve(); run = root / '.revival-local/run'; run.mkdir(parents=True)
            state = {'run': '.revival-local/run', 'mode': 'approved_adult_release', 'receiptModeVersion': 1,
                     'runId': 'a' * 32, 'head': 'b' * 40, 'configSha256': 'c' * 64,
                     'resourceSha256': 'd' * 64, 'editorVersion': 'synthetic'}
            receipt = {'schema': 1, 'runId': state['runId'], 'sourceHead': state['head'],
                'configSha256': state['configSha256'], 'resourceSha256': state['resourceSha256'],
                'unityVersion': 'synthetic', 'injectedManagers': 1, 'loginScenes': 1,
                'preprocessed': True, 'postprocessed': True, 'buildSceneValueMatched': True,
                'compiledEditorGatesDisabled': True, 'productionContractVerified': False,
                'binaryVerified': False, 'distributable': False, 'configuredAndroidDefines': '',
                'artifactSha256': 'e' * 64, 'prospectivePlayerDefinePlanSha256': 'f' * 64,
                'mode': 'disabled_candidate', 'approvedBuildContractMatched': False}
            (run / 'hook-receipt.json').write_bytes(producer.encoded(receipt))
            with self.assertRaises(ValueError): base.verify_hook_receipt(root, state)

    def test_binding_conditional_string_spoof_and_malformed_directives_rejected(self):
        pins = ['a' * 64, 'b' * 40, *(['c' * 64] * 5)]
        declaration = 'private static readonly Binding ApprovedRelease = new Binding(' + ', '.join('"' + pin + '"' for pin in pins) + ');'
        approved.require_immutable_binding(declaration, pins)
        for source in ('#if NEVER_DEFINED\n' + declaration + '\n#endif',
                       'var text = @"' + declaration.replace('"', '""') + '";',
                       '#if NEVER_DEFINED\n#endif\n#endif\n' + declaration,
                       declaration + '\n#if NEVER_DEFINED',
                       '/* ' + declaration + ' */',
                       declaration.replace('new Binding', '#if TEST\nnew Binding\n#endif')):
            with self.subTest(source=source):
                with self.assertRaises(ValueError): approved.require_immutable_binding(source, pins)

    def test_receipt_mode_version_exact_integer_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve(); run = root / '.revival-local/run'; run.mkdir(parents=True)
            path = run / 'plan.private.json'
            for version in (None, True, '1', 1.0, 2):
                plan = {'schema': 2, 'mode': 'approved_adult_release', 'receiptModeVersion': version,
                        'checkout': str(root), 'producerPrepared': False, 'binaryVerified': False, 'distributable': False}
                path.write_bytes(producer.encoded(plan))
                with self.assertRaises(ValueError): producer.load_plan(root, path, base.digest(path.read_bytes()))

    def test_schema_cross_acceptance_rejected(self):
        value = fixture()
        self.assertEqual(approved.read_approved(json.dumps(value).encode())['mode'], 'adult_only_release')
        value['mode'] = 'disabled_candidate'
        with self.assertRaises(ValueError): approved.read_approved(json.dumps(value).encode())
        with self.assertRaises(ValueError): approved.read_approved(b'{"consoleInventoryConfirmed":true}')

    def test_approval_types_minors_provenance_rejected(self):
        for key, bad in [('adultConsentReviewed', 'true'), ('under13ConsentReviewed', True),
                         ('sourceHead', 'bad'), ('inventorySha256', 'bad'), ('countryCodes', 'US;KR')]:
            with self.subTest(key=key):
                value = fixture(); value[key] = bad
                with self.assertRaises(ValueError): approved.read_approved(json.dumps(value).encode())

    def test_current_false_source_blocks_before_launch_and_staging(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            config = root / 'synthetic.json'; config.write_text(json.dumps(fixture()))
            folder = root / 'Assets/Scripts/Advertising'; folder.mkdir(parents=True)
            (folder / 'AdRequestPolicy.cs').write_text('public const bool ProductionAdsEnabled = false;')
            (folder / 'AgeTreatmentPolicy.cs').write_text('public static bool RegionalConsentReviewed => false;')
            plan = {'mode': 'approved_adult_release', 'head': 'a' * 40, 'editorVersion': 'synthetic',
                    'config': str(config), 'run': '.revival-local/run'}
            with patch.object(producer, 'load_plan', return_value=plan), patch.object(producer, 'require_private'), \
                 patch.object(base, 'preflight'), patch.object(producer.subprocess, 'run', side_effect=AssertionError('No launch')):
                with self.assertRaises(ValueError):
                    producer.build_once(root, config, 'a' * 40, 'synthetic', config, 'b' * 64, config, config, allow_unity_build=True)
            self.assertFalse((root / base.JOURNAL).exists())
            self.assertFalse((root / '.revival-local/run/launch-once.private.json').exists())
            self.assertFalse((root / producer.RESOURCE).exists())

    def test_noads_blocks_load_without_blocking_consent_privacy(self):
        source = (ROOT / 'Assets/Scripts/Managers/GoogleAdMobManager.cs').read_text()
        consent = source.split('private bool CanBeginConsent =>', 1)[1].split('private bool TryGetRewardedAdUnit', 1)[0]
        privacy = source.split('public void ShowPrivacyOptions()', 1)[1].split('// This project', 1)[0]
        self.assertNotIn('HasNoAds', consent)
        self.assertNotIn('HasNoAds', privacy)
        self.assertIn('public bool CanRequestAds => !HasNoAds', source)
        init = source.split('private void InitializeConfiguredSdk', 1)[1].split('// Pure', 1)[0]
        self.assertLess(init.index('!CanRequestAds'), init.index('MobileAds.Initialize'))
        load = source.split('private void LoadConfiguredAd', 1)[1].split('public bool ShowRewardedAd', 1)[0]
        self.assertLess(load.index('!_consent.CanRequestAds'), load.index('RewardedAd.Load'))
        show = source.split('public bool ShowRewardedAd', 1)[1]
        self.assertLess(show.index('if (HasNoAds)'), show.index('if (!CanRequestAds)'))
        self.assertIn('session.EarnReward();', show)

    def test_consumer_and_mode_receipt_are_explicit(self):
        source = (ROOT / 'Assets/Scripts/Managers/GoogleAdMobManager.cs').read_text()
        self.assertIn('PrivateAdsReleaseContract.AllowsCurrentAndroidRelease(_productionRewardedAdUnit, ConsentAge)', source)
        template = (ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs').read_text()
        self.assertIn('public static void BuildApprovedAdultRelease()', template)
        self.assertIn('compiledEditorGatesDisabled = !approved', template)
        self.assertIn('approvedBuildContractMatched = approved', template)
        self.assertIn('PrivateAdsContract.Read(raw, root)', template)

if __name__ == '__main__': unittest.main()
