import unittest
from contextlib import contextmanager
import hashlib
import tempfile
import json
import os
import sys
import zipfile
from unittest.mock import patch
from pathlib import Path
from verify_release_candidate import validate_manifest, source_checks, ensure_output_safe, inspect_bundle_libraries, candidate_checks, main, PIPELINE_RUNTIME_CONFIG
from test_verify_native_alignment import elf_fixture


class ReleaseCandidateTests(unittest.TestCase):
    manifest='''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.AeDeong.MonsterTamer" android:versionCode="27"><uses-sdk android:minSdkVersion="25" android:targetSdkVersion="36"/><application/></manifest>'''

    def assert_fixture_paths(self, root, *paths):
        root = root.resolve()
        for path in paths:
            resolved = path.resolve()
            self.assertNotEqual(root, resolved, 'Do not mutate the fixture root itself')
            self.assertTrue(resolved.is_relative_to(root),
                            'Synthetic mutation must stay within its temporary fixture root')

    @contextmanager
    def synthetic_tools(self, root):
        # Real temporary files exercise Windows handles; no SDK or source launcher
        # is executed. Patch only the known jar pin to these actual fixture bytes.
        bundletool = root / 'bundletool/mock.jar'
        java = root / 'jdk/bin/java.exe'
        source = root / 'tools/revival/VerifyAabSignature.java'
        self.assert_fixture_paths(root, bundletool, java, source)
        for path, raw in ((bundletool, b'synthetic bundletool'),
                          (java, b'synthetic Java executable'),
                          (source, b'// synthetic signature source')):
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        with patch('verify_release_candidate.ROOT', root), \
                patch('verify_release_candidate.SHA256', hashlib.sha256(bundletool.read_bytes()).hexdigest()):
            yield bundletool, java, source

    @staticmethod
    def synthetic_aab(path, segments=None):
        path.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(path, 'w') as archive:
            archive.writestr('base/lib/arm64-v8a/test.so', elf_fixture(segments or [dict(memsz=4096)]))

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
            pipeline = root / PIPELINE_RUNTIME_CONFIG
            pipeline.parent.mkdir(parents=True)
            pipeline.write_text('{"enableInBuilds":true,"autoStart":false}')
            enabled = source_checks(root)
            self.assertFalse(enabled['sourceChecksPassed'])
            self.assertEqual(['pipelineRuntimeDisabled'], [key for key, value in enabled['checks'].items() if not value])
            pipeline.write_text('{"enableInBuilds":false}')
            self.assertTrue(source_checks(root)['sourceChecksPassed'])
            settings.write_text(settings.read_text().replace('DOTWEEN','DOTWEEN;TAMER_GAMEPLAY_HARNESS'))
            self.assertFalse(source_checks(root)['checks']['noGlobalHarnessDefines'])
            scenes.write_text('  - enabled: 0\n    path: Assets/Scenes/Login.unity\n  - enabled: 1\n    path: Assets/Scenes/Main.unity\n')
            self.assertFalse(source_checks(root)['checks']['loginFirstScene'])

    def test_pipeline_runtime_settings_reject_enablement_and_ambiguous_input_without_writes(self):
        from verify_release_candidate import pipeline_runtime_disabled
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / PIPELINE_RUNTIME_CONFIG
            self.assertTrue(pipeline_runtime_disabled(root))
            path.parent.mkdir(parents=True)
            cases = [('{}', True), ('{"autoStart":true}', True),
                     ('{"enableInBuilds":false,"autoStart":true}', True),
                     ('{"enableInBuilds":true,"autoStart":false}', False),
                     ('{"enableInBuilds":false,"enableInBuilds":true}', False),
                     ('{"enableInBuilds":true,"enableInBuilds":false}', False),
                     ('{"enableInBuilds":"false"}', False), ('{"enableInBuilds":0}', False),
                     ('{"enableInBuilds":null}', False), ('[]', False), ('{broken', False),
                     ('{"enableInBuilds":false,"port":NaN}', False),
                     ('{"enableInBuilds":false,"port":Infinity}', False)]
            for content, expected in cases:
                with self.subTest(content=content):
                    path.write_bytes(content.encode('utf-8'))
                    original = path.read_bytes()
                    self.assertEqual(expected, pipeline_runtime_disabled(root))
                    self.assertEqual(original, path.read_bytes())
            path.write_bytes(b'\xff\xfe\x00')
            self.assertFalse(pipeline_runtime_disabled(root))

    def test_pipeline_resources_detect_nested_resources_and_preserve_nonresource_assets(self):
        from verify_release_candidate import no_staged_pipeline_resources
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            outside = root / 'Assets/Settings/RuntimePipelineConfig.asset'
            outside.parent.mkdir(parents=True)
            outside.write_bytes(b'non-Resources fixture; preserve')
            self.assertTrue(no_staged_pipeline_resources(root))
            for relative in ['Assets/Resources/RuntimePipelineConfig.asset',
                             'Assets/Settings/Pipeline/Resources/RuntimePipelineBuildInfo.asset',
                             'Assets/Other/Nested/Resources/RuntimePipelineConfig.asset']:
                with self.subTest(relative=relative):
                    path = root / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(b'staged fixture; never delete it from the verifier')
                    original = path.read_bytes()
                    self.assertFalse(no_staged_pipeline_resources(root))
                    self.assertEqual(original, path.read_bytes())
                    path.unlink()
            self.assertEqual(b'non-Resources fixture; preserve', outside.read_bytes())
            self.assertTrue(no_staged_pipeline_resources(root))

    def test_pipeline_settings_are_protected_report_inputs_even_when_missing(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / PIPELINE_RUNTIME_CONFIG
            args = ['verify_release_candidate', '--output', str(path), 'source']
            with patch('verify_release_candidate.ROOT', root), patch.object(sys, 'argv', args), \
                    patch('verify_release_candidate.source_checks') as check:
                with self.assertRaisesRegex(ValueError, 'Output collides'):
                    main()
                check.assert_not_called()
            self.assertFalse(path.exists())

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

    @unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
    def test_candidate_accepts_absent_relro_but_keeps_present_end_and_load_blockers(self):
        cases = [([dict(memsz=4096)], True, False),
                 ([dict(memsz=4096), dict(kind=0x6474E552, memsz=4096)], True, True),
                 ([dict(memsz=32768), dict(kind=0x6474E552, memsz=4096)], False, True),
                 ([dict(memsz=32768), dict(kind=0x6474E552, memsz=16384)], True, True),
                 ([dict(memsz=4096, align=4096)], False, False)]
        with tempfile.TemporaryDirectory() as folder:
            artifact=Path(folder)/'synthetic.aab'
            for segments, passed, present in cases:
                with self.subTest(segments=segments):
                    with zipfile.ZipFile(artifact,'w') as archive:
                        archive.writestr('base/lib/arm64-v8a/test.so',elf_fixture(segments))
                    with self.synthetic_tools(Path(folder).resolve()) as (bundletool, java, _), \
                         patch('verify_release_candidate.subprocess.check_output',
                               side_effect=['',self.manifest,'{}']):
                        result=candidate_checks(artifact,27,26,'0'*64,bundletool,java)
                    self.assertEqual(passed,result['artifactChecksPassed'])
                    self.assertEqual(present,result['libraries'][0]['relroPresent'])
                    self.assertFalse(result['releaseReady'])
                    self.assertFalse(result['deliveredApkZipAlignmentVerified'])
                    self.assertFalse(result['arm64NativeRuntimeVerified'])
                    self.assertFalse(result['productionBinary16KBVerified'])
                    self.assertIn('AAB',result['artifactCheckScope'])
                    if present and segments[-1]['memsz'] == 4096:
                        self.assertFalse(result['libraries'][0]['relroPassed'])
                        self.assertTrue(result['libraries'][0]['relroErrors'])
                    self.assertEqual(passed if present else True,
                                     result['libraries'][0]['relroLayoutPassed'])

    @unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
    def test_candidate_rejects_nested_load_inside_whole_relro(self):
        with tempfile.TemporaryDirectory() as folder:
            artifact = Path(folder) / 'synthetic.aab'
            with zipfile.ZipFile(artifact, 'w') as archive:
                archive.writestr('base/lib/arm64-v8a/test.so', elf_fixture([
                    dict(memsz=4096), dict(offset=512, vaddr=512, memsz=512),
                    dict(kind=0x6474E552, memsz=4096)]))
            with self.synthetic_tools(Path(folder).resolve()) as (bundletool, java, _), \
                    patch('verify_release_candidate.subprocess.check_output',
                          side_effect=['', self.manifest, '{}']):
                result = candidate_checks(artifact, 27, 26, '0'*64,
                                          bundletool, java)
            self.assertTrue(result['libraries'][0]['loadPassed'])
            self.assertFalse(result['libraries'][0]['relroLayoutPassed'])
            self.assertFalse(result['artifactChecksPassed'])
            self.assertFalse(result['releaseReady'])

    @unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
    def test_candidate_aab_write_rename_replace_are_denied_through_native_inspection(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            artifact = root / 'candidate/synthetic.aab'
            self.synthetic_aab(artifact)
            replacement = root / 'replacement.aab'
            replacement.write_bytes(b'synthetic replacement; preserve')
            before, other = artifact.read_bytes(), replacement.read_bytes()
            stages = []

            def blocked_changes(stage):
                stages.append(stage)
                self.assert_fixture_paths(root, artifact, replacement, root / 'renamed.aab',
                                          artifact.parent, root / 'renamed-candidate')
                with self.assertRaises(OSError):
                    artifact.write_bytes(b'changed')
                with self.assertRaises(OSError):
                    artifact.rename(root / 'renamed.aab')
                with self.assertRaises(OSError):
                    os.replace(replacement, artifact)
                with self.assertRaises(OSError):
                    artifact.parent.rename(root / 'renamed-candidate')
                self.assertEqual(before, artifact.read_bytes())
                self.assertEqual(other, replacement.read_bytes())

            outputs = iter(['', self.manifest, '{}'])
            def command(*args, **kwargs):
                blocked_changes('command')
                return next(outputs)
            def native(stream):
                self.assertFalse(isinstance(stream, (str, Path)))
                result = inspect_bundle_libraries(stream)
                blocked_changes('after-native')
                return result

            with self.synthetic_tools(root) as (bundletool, java, _), \
                    patch('verify_release_candidate.subprocess.check_output', side_effect=command), \
                    patch('verify_release_candidate.inspect_bundle_libraries', side_effect=native):
                result = candidate_checks(artifact, 27, 26, '0'*64, bundletool, java)
            self.assertEqual(['command'] * 3 + ['after-native'], stages)
            self.assertTrue(result['artifactChecksPassed'])
            self.assertEqual(hashlib.sha256(before).hexdigest(), result['artifactSha256'])
            self.assertFalse(result['releaseReady'])
            self.assertEqual(before, artifact.read_bytes())
            self.assertEqual(other, replacement.read_bytes())

    @unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
    def test_candidate_tool_write_delete_replace_and_parent_rename_are_denied(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            artifact = root / 'candidate/synthetic.aab'
            self.synthetic_aab(artifact)
            replacement = root / 'replacement-tool'
            replacement.write_bytes(b'synthetic replacement; preserve')
            with self.synthetic_tools(root) as tools:
                before = {path: path.read_bytes() for path in (artifact, *tools, replacement)}
                outputs = iter(['', self.manifest, '{}'])
                def command(*args, **kwargs):
                    for path in tools:
                        with self.subTest(tool=path.name):
                            self.assert_fixture_paths(root, path, replacement, path.with_name('renamed-tool'),
                                                      path.parent, path.parent.with_name('renamed-tool-parent'))
                            with self.assertRaises(OSError):
                                path.write_bytes(b'changed')
                            with self.assertRaises(OSError):
                                path.unlink()
                            with self.assertRaises(OSError):
                                path.rename(path.with_name('renamed-tool'))
                            with self.assertRaises(OSError):
                                os.replace(replacement, path)
                            with self.assertRaises(OSError):
                                path.parent.rename(path.parent.with_name('renamed-tool-parent'))
                    return next(outputs)
                with patch('verify_release_candidate.subprocess.check_output', side_effect=command):
                    result = candidate_checks(artifact, 27, 26, '0'*64, *tools[:2])
                self.assertTrue(result['artifactChecksPassed'])
                self.assertEqual(before, {path: path.read_bytes() for path in before})

    @unittest.skipUnless(os.name == 'nt', 'Windows handle semantics required')
    def test_candidate_lock_or_command_failure_preserves_inputs_and_creates_no_report(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            artifact = root / 'candidate/synthetic.aab'
            self.synthetic_aab(artifact)
            report = root / 'new-report.json'
            with self.synthetic_tools(root) as (bundletool, java, source):
                self.assert_fixture_paths(root, artifact, bundletool, java, source, report)
                before = {path: path.read_bytes() for path in (artifact, bundletool, java, source)}
                args = ['verify_release_candidate', '--output', str(report), 'aab', '--aab', str(artifact),
                        '--version-code', '27', '--published-max-code', '26',
                        '--upload-cert-sha256', '0'*64, '--bundletool', str(bundletool), '--java', str(java)]
                with artifact.open('r+b'), patch.object(sys, 'argv', args), \
                        patch('verify_release_candidate.subprocess.check_output') as command:
                    with self.assertRaises(OSError):
                        main()
                    command.assert_not_called()
                self.assertFalse(report.exists())
                with patch.object(sys, 'argv', args), \
                        patch('verify_release_candidate.subprocess.check_output',
                              side_effect=OSError('synthetic command failure')):
                    with self.assertRaises(OSError):
                        main()
                self.assertFalse(report.exists())
                # Both failure paths release their handles; original bytes remain
                # readable through a newly acquired write-capable handle.
                for path, raw in before.items():
                    with path.open('r+b') as stream:
                        self.assertEqual(raw, stream.read())


if __name__=='__main__': unittest.main()
