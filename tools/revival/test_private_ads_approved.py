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
    def test_both_ad_preflights_reject_separate_privacy_approval(self):
        from validate_private_ads_preparation import audit
        declaration = 'public static bool PrivacySdkEnvironmentReviewed => false;'
        invalid = [declaration.replace('false', 'true'), '', '// ' + declaration,
                   '#if UNUSED\n' + declaration + '\n#endif', declaration + '\n' + declaration]
        for is_approved in (False, True):
            with self.subTest(approved=is_approved), tempfile.TemporaryDirectory() as temp:
                root = Path(temp).resolve()
                value = fixture() if is_approved else dict(checkout=str(root),
                    androidAppId=fixture()['androidAppId'], productionRewardedAdUnit=fixture()['productionRewardedAdUnit'],
                    consoleInventoryConfirmed=True, productionActivationApproved=False, regionalReviewApproved=False)
                config = root / 'synthetic.json'; config.write_text(json.dumps(value))
                advertising = root / 'Assets/Scripts/Advertising'; advertising.mkdir(parents=True)
                (advertising / 'AdRequestPolicy.cs').write_text(
                    'public const bool ProductionAdsEnabled = ' + ('true' if is_approved else 'false') + ';')
                age_prefix = 'public static bool RegionalConsentReviewed => ' + ('true' if is_approved else 'false') + ';\n'
                age_prefix += '\n'.join('private const bool ' + cohort + 'ConsentReviewed = ' +
                    ('true' if is_approved and cohort == 'Adult' else 'false') + ';'
                    for cohort in ('Under13', 'From13To15', 'From16To17', 'Adult')) + '\n'
                files = {
                    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset': 'adMobAndroidAppId: ' + value['androidAppId'],
                    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml':
                        '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application>'
                        '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="' +
                        value['androidAppId'] + '"/></application></manifest>',
                    'ProjectSettings/ProjectSettings.asset': '  applicationIdentifier:\n    Android: com.synthetic.contract\n  buildNumber:\n    Android: 1\n',
                    'Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs': 'synthetic binding input'}
                for name, content in files.items():
                    path = root / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_text(content)
                age = advertising / 'AgeTreatmentPolicy.cs'
                with patch.object(base, 'preflight'), patch.object(approved, 'require_immutable_binding') as binding:
                    invoke = lambda: approved.preflight_approved(root, config, 'a' * 40, 'synthetic') if is_approved else audit(root, config)
                    age.write_text(age_prefix + declaration)
                    self.assertFalse(invoke()['binaryVerified'])
                    binding.reset_mock()
                    for source in invalid:
                        with self.subTest(source=source), self.assertRaises(ValueError):
                            age.write_text(age_prefix + source); invoke()
                        binding.assert_not_called()
                    self.assertFalse((root / base.JOURNAL).exists())
                    self.assertFalse((root / producer.RESOURCE).exists())

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

    def test_package_validation_uses_application_identifier_section(self):
        value = fixture()
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            config = root / 'synthetic.json'; config.write_text(json.dumps(value))
            contents = {
                'Assets/Scripts/Advertising/AdRequestPolicy.cs': 'synthetic',
                'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs': 'synthetic',
                'Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs': 'synthetic',
                'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset': 'adMobAndroidAppId: ' + value['androidAppId'],
                'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml':
                    '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application>'
                    '<meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="' + value['androidAppId'] + '"/></application></manifest>'}
            for name, content in contents.items():
                path = root / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_text(content)
            project = root / 'ProjectSettings/ProjectSettings.asset'; project.parent.mkdir()
            valid = '  applicationIdentifier:\n    Android: com.synthetic.contract\n'
            unrelated = '  icons:\n    Android: icon-value\n  settings:\n    Android: quality-value\n'
            with patch.object(base, 'preflight'), patch.object(approved, 'require_disabled_declaration'), patch.object(approved, 'require_immutable_binding'):
                project.write_text(unrelated + valid + '  nextSetting: 1\n')
                self.assertTrue(approved.preflight_approved(root, config, 'a' * 40, 'synthetic')['approvedSourceContractValid'])
                for section in ('', valid.replace('com.synthetic.contract', 'com.synthetic.other'), valid + valid, valid + '  applicationIdentifier:\n', '  applicationIdentifier:\n' + valid,
                                '  applicationIdentifier:\n    Standalone: com.synthetic.contract\n',
                                valid.replace('    Android: com.synthetic.contract', '    Android: com.synthetic.contract\n    Android: com.synthetic.contract')):
                    project.write_text(unrelated + section + '  nextSetting: 1\n')
                    with self.assertRaises(ValueError):
                        approved.preflight_approved(root, config, 'a' * 40, 'synthetic')

    def test_binding_comparisons_are_not_second_assignments(self):
        pins = ['a' * 64, 'b' * 40, *(['c' * 64] * 5)]
        for name in ('ApprovedRelease', 'ApprovedPrivacy'):
            declaration = 'private static readonly Binding ' + name + ' = new Binding(' + ', '.join('"' + pin + '"' for pin in pins) + ');'
            for comparison in (name + ' == null', name + ' != null', name + ' == other'):
                approved.require_immutable_binding(declaration + '\nif (' + comparison + ') return;', pins, name)
            for assignment in (name + ' = null;', name + ' = new Binding();', declaration):
                with self.assertRaises(ValueError):
                    approved.require_immutable_binding(declaration + '\n' + assignment, pins, name)

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
        self.assertIn('receipt.compiledEditorGatesDisabled = !approved && !privacy', template)
        self.assertIn('receipt.approvedBuildContractMatched = approved', template)
        self.assertIn('PrivateAdsContract.Read(raw, root)', template)

class PrivacyProducerTests(unittest.TestCase):
    def value(self):
        value = {k: v for k, v in fixture().items() if k in approved.PRIVACY_FIELDS}
        value.update(mode='privacy_only_adult', privacyEnvironmentReviewSha256='b' * 64)
        value.update({k: True for k in approved.PRIVACY_TRUE_FLAGS})
        value.update({k: False for k in approved.PRIVACY_FALSE_FLAGS})
        return value

    def source_tree(self, root):
        value = self.value(); config = root / 'synthetic.json'; config.write_bytes(producer.encoded(value))
        pins = [base.digest(config.read_bytes()), value['sourceHead'], *(value[k] for k in approved.PRIVACY_REFERENCES)]
        files = {
            'Assets/Scripts/Advertising/AdRequestPolicy.cs': 'public const bool ProductionAdsEnabled = false;',
            'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs':
                'public static bool PrivacySdkEnvironmentReviewed => true;\npublic static bool RegionalConsentReviewed => true;\n' +
                '\n'.join('private const bool ' + c + 'ConsentReviewed = ' + ('true' if c == 'Adult' else 'false') + ';'
                          for c in ('Adult', 'Under13', 'From13To15', 'From16To17')),
            'Assets/Scripts/Advertising/PrivatePrivacyReleaseContract.cs':
                'private static readonly Binding ApprovedPrivacy = new Binding(' + ', '.join('"' + p + '"' for p in pins) + ');',
            'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset': 'adMobAndroidAppId: ' + value['androidAppId'],
            'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml':
                '<manifest xmlns:android="http://schemas.android.com/apk/res/android"><application><meta-data '
                'android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="' + value['androidAppId'] + '"/></application></manifest>',
            'ProjectSettings/ProjectSettings.asset': '  applicationIdentifier:\n    Android: ' + value['packageId'] + '\n  buildNumber:\n    Android: 1\n'}
        for name, text in files.items():
            p = root / name; p.parent.mkdir(parents=True, exist_ok=True); p.write_text(text, encoding='utf-8')
        return config, files

    def test_privacy_schema_and_cross_mode_types(self):
        value = self.value(); self.assertEqual(approved.read_approved(producer.encoded(value), privacy_only=True), value)
        with self.assertRaises(ValueError): approved.read_approved(producer.encoded(value))
        with self.assertRaises(ValueError): approved.read_approved(producer.encoded(fixture()), privacy_only=True)
        for key in approved.PRIVACY_FIELDS:
            invalid = dict(value); invalid[key] = None
            with self.subTest(key=key), self.assertRaises(ValueError): approved.read_approved(producer.encoded(invalid), privacy_only=True)
        for key in approved.PRIVACY_TRUE_FLAGS + approved.PRIVACY_FALSE_FLAGS:
            invalid = dict(value); invalid[key] = not invalid[key]
            with self.subTest(key=key), self.assertRaises(ValueError): approved.read_approved(producer.encoded(invalid), privacy_only=True)

    def test_source_binding_gates_and_manifest_package(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(base, 'preflight'):
            root = Path(tmp).resolve(); config, files = self.source_tree(root)
            invoke = lambda: approved.preflight_approved(root, config, 'a' * 40, 'synthetic', privacy_only=True)
            self.assertTrue(invoke()['approvedPrivacySourceContractValid'])
            alterations = [('AdRequestPolicy.cs', 'false', 'true'), ('AgeTreatmentPolicy.cs', '=> true', '=> false'),
                           ('AgeTreatmentPolicy.cs', 'AdultConsentReviewed = true', 'AdultConsentReviewed = false'),
                           ('AgeTreatmentPolicy.cs', 'Under13ConsentReviewed = false', 'Under13ConsentReviewed = true'),
                           ('PrivatePrivacyReleaseContract.cs', 'new Binding(', 'null; // new Binding(')]
            for name, old, new in alterations:
                p = root / 'Assets/Scripts/Advertising' / name; original = p.read_text(encoding='utf-8')
                p.write_text(original.replace(old, new), encoding='utf-8')
                with self.subTest(name=name, old=old), self.assertRaises(ValueError): invoke()
                p.write_text(original, encoding='utf-8')
            for name in ('Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
                         'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
                         'ProjectSettings/ProjectSettings.asset'):
                p = root / name; original = p.read_text(encoding='utf-8'); p.write_text(original.replace('1111111111111111', '4444444444444444').replace('com.synthetic.contract', 'com.other'), encoding='utf-8')
                with self.subTest(name=name), self.assertRaises(ValueError): invoke()
                p.write_text(original, encoding='utf-8')
            p = root / producer.RESOURCE; p.parent.mkdir(parents=True); p.write_bytes(b'synthetic collision')
            with self.assertRaises(ValueError): invoke()

    def test_current_default_stops_before_staging_or_launch(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(base, 'preflight'), patch.object(producer, 'require_private'), patch.object(producer.subprocess, 'run', side_effect=AssertionError('No launch')):
            root = Path(tmp).resolve(); config, files = self.source_tree(root)
            (root / 'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs').write_bytes((ROOT / 'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs').read_bytes())
            plan = {'mode': 'privacy_only_adult', 'head': 'a' * 40, 'editorVersion': 'synthetic', 'config': str(config), 'run': '.revival-local/run'}
            with patch.object(producer, 'load_plan', return_value=plan), self.assertRaises(ValueError):
                producer.build_once(root, config, 'a' * 40, 'synthetic', config, 'b' * 64, config, config, allow_unity_build=True)
            self.assertFalse((root / base.JOURNAL).exists()); self.assertFalse((root / producer.PRIVACY_RESOURCE).exists())

    def test_exact_resource_payload_and_mode_specific_plan(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(producer, 'inventory', return_value={}):
            root = Path(tmp).resolve(); config, _ = self.source_tree(root); raw = config.read_bytes()
            pre = {'approvedPrivacySourceContractValid': True, 'configSha256': base.digest(raw)}
            result = producer.prepare_plan(root, config, 'a' * 40, 'synthetic', branch='synthetic', preflight_result=pre, mode='privacy_only_adult')
            plan = producer.load_plan(root, Path(result['plan']), result['planSha256'])
            self.assertIn(producer.PRIVACY_RESOURCE, plan['owned']); self.assertNotIn(producer.RESOURCE, plan['owned'])
            self.assertEqual((root / plan['owned'][producer.PRIVACY_RESOURCE]['payload']).read_bytes(), raw)
            self.assertFalse((root / producer.PRIVACY_RESOURCE).exists())
            self.assertEqual(producer.artifact_name(plan['mode']), 'privacy-only-adult.apk')
            self.assertEqual(producer.build_entry(plan['mode']), 'PrivateProductionAdsBuild.BuildApprovedAdultPrivacy')
            path = Path(result['plan']); plan['mode'] = 'approved_adult_release'; path.write_bytes(producer.encoded(plan))
            with self.assertRaises(ValueError): producer.load_plan(root, path, base.digest(path.read_bytes()))
            self.assertEqual(producer.artifact_name('disabled_candidate'), 'disabled-preparation.aab')

    def test_private_apk_receipt_and_legacy_cross_acceptance(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve(); run = root / '.revival-local/run'; run.mkdir(parents=True)
            config = root / 'synthetic.json'; config.write_bytes(producer.encoded(self.value())); artifact = run / 'privacy-only-adult.apk'; artifact.write_bytes(b'synthetic artifact')
            state = {'run': '.revival-local/run', 'config': str(config), 'mode': 'privacy_only_adult', 'receiptModeVersion': 1,
                     'runId': 'a' * 32, 'head': 'b' * 40, 'configSha256': base.digest(config.read_bytes()), 'resourceSha256': 'd' * 64, 'editorVersion': 'synthetic'}
            receipt = dict(schema=1, runId=state['runId'], sourceHead=state['head'], configSha256=state['configSha256'], resourceSha256='d' * 64,
                unityVersion='synthetic', injectedManagers=1, loginScenes=1, preprocessed=True, postprocessed=True, buildSceneValueMatched=True,
                compiledEditorGatesDisabled=False, productionContractVerified=False, binaryVerified=False, distributable=False, configuredAndroidDefines='',
                artifactSha256=base.digest(artifact.read_bytes()), prospectivePlayerDefinePlanSha256='f' * 64, mode='privacy_only_adult', approvedBuildContractMatched=False,
                approvedPrivacyBuildContractMatched=True, productionAdsEnabled=False)
            p = run / 'hook-receipt.json'; p.write_bytes(producer.encoded(receipt)); self.assertTrue(base.verify_hook_receipt(root, state)['hookReceiptVerified'])
            for key, value in [('mode', 'approved_adult_release'), ('productionAdsEnabled', True), ('approvedPrivacyBuildContractMatched', False)]:
                invalid = dict(receipt); invalid[key] = value; p.write_bytes(producer.encoded(invalid))
                with self.subTest(key=key), self.assertRaises(ValueError): base.verify_hook_receipt(root, state)
            p.write_bytes(producer.encoded(receipt)); artifact.write_bytes(b'changed')
            with self.assertRaises(ValueError): base.verify_hook_receipt(root, state)

    def test_build_template_compiles_without_editor_launch(self):
        import subprocess
        from private_ads_evidence import create_private_directory
        output = ROOT / 'Logs/revival/privacy-only-build-route-pure-1007'; create_private_directory(output)
        editor = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')
        api = editor / 'UnityReferenceAssemblies/unity-4.8-api'; managed = editor / 'Managed'
        sources = ['Assets/Scripts/Advertising/PrivatePrivacyReleaseContract.cs', 'Assets/Scripts/Advertising/PrivateAdsReleaseContract.cs',
                   'Assets/Scripts/Advertising/AgeTreatmentPolicy.cs', 'Assets/Scripts/Advertising/AdRequestPolicy.cs', 'Assets/Scripts/Advertising/LocalAgeChoice.cs']
        sources += ['tools/revival/private_ads_build/' + name for name in base.TEMPLATES]
        stub = output / 'compile-stubs.cs'; stub.write_text('namespace AD { public class GoogleAdMobManager : UnityEngine.MonoBehaviour {} } public static class RevivalBuild { public static System.IDisposable AndroidRelroLinkScope() { return null; } }')
        refs = [api / name for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')]
        refs += list(managed.glob('Unity*.dll')) + list((managed / 'UnityEngine').glob('*.dll'))
        refs += [ROOT / 'Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll', api / 'Facades/netstandard.dll']
        args = ['-nologo', '-langversion:9.0', '-target:library', '-define:UNITY_EDITOR', '-out:"' + str(output / 'template.dll') + '"']
        args += ['-r:"' + str(p) + '"' for p in refs] + ['"' + str(ROOT / p) + '"' for p in sources] + ['"' + str(stub) + '"']
        rsp = output / 'compile.rsp'; rsp.write_text('\n'.join(args))
        with (output / 'compile.private.log').open('xb') as log:
            result = subprocess.run([str(editor / 'netcorerun/netcorerun.exe'), str(editor / 'DotNetSdkRoslyn/csc.dll'), '@' + str(rsp)], stdout=log, stderr=subprocess.STDOUT)
        (output / 'result.private.json').write_text(json.dumps({'compileExit':result.returncode, 'unityBuilds':0, 'sdkCalls':0, 'realApprovalRecordsModified':0, 'binaryVerified':False, 'runtimePrivacyVerified':False,
            'sourceSha256':{p:base.digest((ROOT/p).read_bytes()) for p in sources}}, indent=2))
        self.assertEqual(result.returncode, 0, 'Pure template compilation failed; preserve private log')

if __name__ == '__main__': unittest.main()
