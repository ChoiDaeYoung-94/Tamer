import unittest
import tempfile
import json
import os
import sys
import zipfile
from unittest.mock import patch
from pathlib import Path
from verify_release_candidate import validate_manifest, source_checks, ensure_output_safe, inspect_bundle_libraries, candidate_checks, SHA256, main
from test_verify_native_alignment import elf_fixture


class ReleaseCandidateTests(unittest.TestCase):
    manifest='''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.AeDeong.MonsterTamer" android:versionCode="27"><uses-sdk android:minSdkVersion="25" android:targetSdkVersion="36"/><application/></manifest>'''

    def test_reviewed_candidate_manifest(self):
        self.assertEqual(validate_manifest(self.manifest,27,26)['versionCode'],27)

    def test_wrong_identity_debug_test_or_sdk_is_rejected(self):
        for old,new in [('MonsterTamer"','MonsterTamer.revival"'),('<application/>','<application android:debuggable="true"/>'),
                        ('<application/>','<application android:testOnly="true"/>'),('targetSdkVersion="36"','targetSdkVersion="35"'),
                        ('minSdkVersion="25"','minSdkVersion="23"'),('versionCode="27"','versionCode="28"'),('<application/>','')]:
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
  AndroidMinSdkVersion: 25
  AndroidTargetSdkVersion: 36
  AndroidTargetArchitectures: 2
  AndroidBundleVersionCode: 26
  scriptingBackend:
    Android: 1
  scriptingDefineSymbols:
    Android: DOTWEEN
''')
            (root/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.3.25f1')
            scenes=root/'ProjectSettings/EditorBuildSettings.asset'
            scenes.write_text('  - enabled: 1\n    path: Assets/Scenes/Login.unity\n')
            (root/'Assets/Resources/IAPProductCatalog.json').write_text(json.dumps({'enableCodelessAutoInitialization':False,'enableUnityGamingServicesAutoInitialization':False}))
            self.assertTrue(source_checks(root)['sourceChecksPassed'])
            self.assertFalse(source_checks(root)['releaseReady'])
            settings.write_text(settings.read_text().replace('DOTWEEN','DOTWEEN;TAMER_GAMEPLAY_HARNESS'))
            self.assertFalse(source_checks(root)['checks']['noGlobalHarnessDefines'])
            scenes.write_text('  - enabled: 0\n    path: Assets/Scenes/Login.unity\n  - enabled: 1\n    path: Assets/Scenes/Main.unity\n')
            self.assertFalse(source_checks(root)['checks']['loginFirstScene'])

    def test_output_collision_rejected_before_candidate_is_opened(self):
        with tempfile.TemporaryDirectory() as folder:
            artifact=Path(folder)/'candidate.aab'
            artifact.write_bytes(b'original artifact')
            args=['verify_release_candidate','--output',str(artifact),'aab','--aab',str(artifact),
                  '--version-code','27','--published-max-code','26','--upload-cert-sha256','0'*64]
            with patch.object(sys,'argv',args), self.assertRaisesRegex(ValueError,'Output collides'):
                main()
            self.assertEqual(artifact.read_bytes(),b'original artifact')

    def test_output_hardlink_collision_preserves_input(self):
        with tempfile.TemporaryDirectory() as folder:
            source=Path(folder)/'input.jar'
            source.write_bytes(b'original tool')
            output=Path(folder)/'report.json'
            os.link(source,output)
            with self.assertRaisesRegex(ValueError,'Output collides'):
                ensure_output_safe(output,[source])
            self.assertEqual(source.read_bytes(),b'original tool')

    def test_existing_report_rejected_before_candidate_commands(self):
        with tempfile.TemporaryDirectory() as folder:
            report=Path(folder)/'report.json'
            report.write_bytes(b'prior evidence')
            args=['verify_release_candidate','--output',str(report),'aab','--aab','not-opened.aab',
                  '--version-code','27','--published-max-code','26','--upload-cert-sha256','0'*64]
            with patch.object(sys,'argv',args), patch('verify_release_candidate.candidate_checks') as check:
                with self.assertRaisesRegex(ValueError,'Output already exists'):
                    main()
                check.assert_not_called()
            self.assertEqual(report.read_bytes(),b'prior evidence')

    def test_report_created_during_check_is_preserved(self):
        with tempfile.TemporaryDirectory() as folder:
            report=Path(folder)/'report.json'
            def concurrent_report(root):
                report.write_bytes(b'concurrent evidence')
                return {'sourceChecksPassed':True}
            args=['verify_release_candidate','--output',str(report),'source']
            with patch.object(sys,'argv',args), patch('verify_release_candidate.source_checks',side_effect=concurrent_report):
                with self.assertRaises(FileExistsError):
                    main()
            self.assertEqual(report.read_bytes(),b'concurrent evidence')

    def test_big_endian_aarch64_is_rejected_even_with_aligned_load_and_relro(self):
        segments=[dict(memsz=16384),dict(kind=0x6474E552,memsz=16384)]
        with tempfile.TemporaryDirectory() as folder:
            artifact=Path(folder)/'synthetic.aab'
            for endian in ['<','>']:
                with zipfile.ZipFile(artifact,'w') as archive:
                    archive.writestr('base/lib/arm64-v8a/test.so',elf_fixture(segments,endian=endian))
                if endian=='<':
                    self.assertTrue(all(x['loadPassed'] and x['relroPassed'] for x in inspect_bundle_libraries(artifact)))
                else:
                    with self.assertRaisesRegex(ValueError,'little-endian'):
                        inspect_bundle_libraries(artifact)

    def test_candidate_accepts_absent_relro_but_keeps_present_end_and_load_blockers(self):
        cases = [([dict(memsz=4096)], True, False),
                 ([dict(memsz=4096), dict(kind=0x6474E552, memsz=4096)], False, True),
                 ([dict(memsz=4096, align=4096)], False, False)]
        with tempfile.TemporaryDirectory() as folder:
            artifact=Path(folder)/'synthetic.aab'
            for segments, passed, present in cases:
                with self.subTest(segments=segments):
                    with zipfile.ZipFile(artifact,'w') as archive:
                        archive.writestr('base/lib/arm64-v8a/test.so',elf_fixture(segments))
                    with patch('verify_release_candidate.digest',return_value=SHA256), \
                         patch('verify_release_candidate.subprocess.check_output',
                               side_effect=['',self.manifest,'{}']):
                        result=candidate_checks(artifact,27,26,'0'*64,Path('mock.jar'),Path('mock.java'))
                    self.assertEqual(passed,result['artifactChecksPassed'])
                    self.assertEqual(present,result['libraries'][0]['relroPresent'])
                    self.assertFalse(result['releaseReady'])
                    self.assertFalse(result['deliveredApkZipAlignmentVerified'])
                    self.assertFalse(result['arm64NativeRuntimeVerified'])
                    self.assertFalse(result['productionBinary16KBVerified'])
                    self.assertIn('AAB',result['artifactCheckScope'])
                    if present:
                        self.assertFalse(result['libraries'][0]['relroPassed'])
                        self.assertTrue(result['libraries'][0]['relroErrors'])


if __name__=='__main__': unittest.main()
