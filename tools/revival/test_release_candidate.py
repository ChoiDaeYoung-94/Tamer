import unittest
import tempfile
import json
from pathlib import Path
from verify_release_candidate import validate_manifest, source_checks


class ReleaseCandidateTests(unittest.TestCase):
    manifest='''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.AeDeong.MonsterTamer" android:versionCode="27"><uses-sdk android:minSdkVersion="24" android:targetSdkVersion="36"/><application/></manifest>'''

    def test_reviewed_candidate_manifest(self):
        self.assertEqual(validate_manifest(self.manifest,27,26)['versionCode'],27)

    def test_wrong_identity_debug_test_or_sdk_is_rejected(self):
        for old,new in [('MonsterTamer"','MonsterTamer.revival"'),('<application/>','<application android:debuggable="true"/>'),
                        ('<application/>','<application android:testOnly="true"/>'),('targetSdkVersion="36"','targetSdkVersion="35"'),
                        ('minSdkVersion="24"','minSdkVersion="23"'),('versionCode="27"','versionCode="28"'),('<application/>','')]:
            with self.subTest(new=new), self.assertRaises(ValueError):
                validate_manifest(self.manifest.replace(old,new),27,26)

    def test_already_published_or_unknown_maximum_rejected(self):
        for candidate,published in [(27,27),(27,28),(27,-1)]:
            with self.subTest(published=published), self.assertRaises(ValueError):
                validate_manifest(self.manifest,candidate,published)

    def test_source_check_rejects_harness_symbols_and_disabled_login(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder)
            (root/'ProjectSettings').mkdir()
            (root/'Assets/Resources').mkdir(parents=True)
            settings=root/'ProjectSettings/ProjectSettings.asset'
            settings.write_text('''  applicationIdentifier:
    Android: com.AeDeong.MonsterTamer
  AndroidMinSdkVersion: 24
  AndroidTargetSdkVersion: 36
  AndroidTargetArchitectures: 2
  AndroidBundleVersionCode: 26
  scriptingBackend:
    Android: 1
  scriptingDefineSymbols:
    Android: DOTWEEN
''')
            (root/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.0.81f1')
            scenes=root/'ProjectSettings/EditorBuildSettings.asset'
            scenes.write_text('  - enabled: 1\n    path: Assets/Scenes/Login.unity\n')
            (root/'Assets/Resources/IAPProductCatalog.json').write_text(json.dumps({'enableCodelessAutoInitialization':False,'enableUnityGamingServicesAutoInitialization':False}))
            self.assertTrue(source_checks(root)['sourceChecksPassed'])
            self.assertFalse(source_checks(root)['releaseReady'])
            settings.write_text(settings.read_text().replace('DOTWEEN','DOTWEEN;TAMER_GAMEPLAY_HARNESS'))
            self.assertFalse(source_checks(root)['checks']['noGlobalHarnessDefines'])
            scenes.write_text('  - enabled: 0\n    path: Assets/Scenes/Login.unity\n  - enabled: 1\n    path: Assets/Scenes/Main.unity\n')
            self.assertFalse(source_checks(root)['checks']['loginFirstScene'])


if __name__=='__main__': unittest.main()
