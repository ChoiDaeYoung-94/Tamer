"""Synthetic lifecycle/contract tests: no Unity, device, real identifiers or signing."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import private_ads_build as build
from validate_private_ads_preparation import read_contract


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        for path in build.PROTECTED:
            file = self.root / path
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(b'synthetic original')
        (self.root / 'Assets/Scripts/Editor').mkdir(parents=True)
        self.config = self.root / 'config.json'
        self.config.write_bytes(b'{}')
        self.fake_git = patch.object(build, 'git', side_effect=lambda root, *args:
                                    'head' if args[:2] == ('rev-parse', 'HEAD') else '')
        self.fake_git.start()
        self.addCleanup(self.fake_git.stop)

    def stage(self, owner=None):
        return build.stage(self.root, self.config, {'configSha256': build.digest(b'{}')},
                           'head', 'synthetic', owner)

    def test_roundtrip_removes_only_owned_files_and_keeps_snapshots(self):
        state = self.stage()
        self.assertTrue((self.root / build.JOURNAL).is_file())
        result = build.finalize(self.root, state)
        self.assertTrue(result['originalsUnchanged'])
        self.assertFalse(result['binaryVerified'])
        self.assertTrue((self.root / build.HOOK).exists())
        self.assertTrue((self.root / build.JOURNAL).exists())
        self.assertTrue(result['manualRecoveryRequired'])
        self.assertTrue((self.root / state['run'] / 'journal.completed.json').is_file())
        self.assertEqual((self.root / build.PROTECTED[0]).read_bytes(), b'synthetic original')

    def test_force_stop_journal_blocks_next_stage(self):
        state = self.stage()
        with self.assertRaises(ValueError):
            self.stage()
        self.assertTrue((self.root / build.JOURNAL).exists())
        build.finalize(self.root, state)

    def test_changed_hook_meta_or_unknown_contents_preserved(self):
        state = self.stage()
        hook = self.root / build.HOOK / 'PrivateAdsContract.cs.meta'
        hook.write_bytes(b'other owner change')
        with self.assertRaises(ValueError):
            build.finalize(self.root, state)
        self.assertEqual(hook.read_bytes(), b'other owner change')
        self.assertTrue((self.root / build.JOURNAL).exists())

    def test_unknown_child_is_never_recursively_deleted(self):
        state = self.stage()
        child = self.root / build.HOOK / 'unowned.txt'
        child.write_bytes(b'keep')
        with self.assertRaises(ValueError):
            build.finalize(self.root, state)
        self.assertEqual(child.read_bytes(), b'keep')

    def test_source_drift_preserves_new_bytes_and_original_snapshot(self):
        state = self.stage()
        source = self.root / build.PROTECTED[0]
        source.write_bytes(b'concurrent edit')
        with self.assertRaises(ValueError):
            build.finalize(self.root, state)
        self.assertEqual(source.read_bytes(), b'concurrent edit')
        self.assertEqual((self.root / state['sources'][build.PROTECTED[0]]['backup']).read_bytes(),
                         b'synthetic original')
        self.assertTrue((self.root / build.JOURNAL).exists())
        self.assertTrue((self.root / build.HOOK).exists())

    def test_partial_stage_failure_can_clean_exact_owner(self):
        owner = {}
        original_open = Path.open

        def fail_write(path, mode='r', *args, **kwargs):
            if str(path).endswith('PrivateAdsContract.cs') and mode == 'xb':
                raise OSError('simulated write failure')
            return original_open(path, mode, *args, **kwargs)

        with patch.object(Path, 'open', fail_write), self.assertRaises(OSError):
            self.stage(owner)
        self.assertTrue(owner)
        self.assertTrue(build.finalize(self.root, owner)['originalsUnchanged'])

    def test_tampered_journal_rejected_before_deleting_hook(self):
        state = self.stage()
        (self.root / build.JOURNAL).write_text('{}')
        with self.assertRaises(ValueError):
            build.finalize(self.root, state)
        self.assertTrue((self.root / build.HOOK / 'PrivateAdsContract.cs').exists())

    def test_traversal_and_external_absolute_paths_rejected(self):
        for path in ('../other', str(self.root.parent / 'other')):
            with self.subTest(path=path), self.assertRaises(ValueError):
                build.safe_path(self.root, path)

    def test_config_drift_during_staging_rejected_with_cleanup(self):
        self.config.write_bytes(b'{"changed":true}')
        owner = {}
        with self.assertRaises(ValueError):
            self.stage(owner)
        self.assertTrue(build.finalize(self.root, owner)['originalsUnchanged'])

    def test_same_bytes_created_by_competitor_are_preserved(self):
        owner = {}
        original_open = Path.open
        template = Path(build.__file__).parent / 'private_ads_build/PrivateAdsContract.cs'
        raw = template.read_bytes()

        def collide(path, mode='r', *args, **kwargs):
            if str(path).endswith('PrivateAdsContract.cs') and mode == 'xb':
                with original_open(path, 'wb') as stream:
                    stream.write(raw)
            return original_open(path, mode, *args, **kwargs)

        with patch.object(Path, 'open', collide), self.assertRaises(FileExistsError):
            self.stage(owner)
        with self.assertRaises(ValueError):
            build.finalize(self.root, owner)
        self.assertEqual((self.root / build.HOOK / 'PrivateAdsContract.cs').read_bytes(), raw)

    def test_competitor_empty_folder_is_preserved(self):
        owner = {}
        original_mkdir = Path.mkdir

        def collide(path, *args, **kwargs):
            if path == self.root / build.HOOK:
                original_mkdir(path)
            return original_mkdir(path, *args, **kwargs)

        with patch.object(Path, 'mkdir', collide), self.assertRaises(FileExistsError):
            self.stage(owner)
        self.assertTrue(build.finalize(self.root, owner)['manualRecoveryRequired'])
        self.assertTrue((self.root / build.HOOK).is_dir())

    def test_execute_is_blocked_before_any_preflight_or_mutation(self):
        with patch.object(build, 'preflight', side_effect=AssertionError('must not run')):
            with self.assertRaises(ValueError):
                build.execute(self.root, self.config, 'head', 'synthetic')
        self.assertFalse((self.root / build.JOURNAL).exists())


class CSharpContractTests(unittest.TestCase):
    def test_actual_csharp_template_agrees_with_python_on_adversarial_json(self):
        # Compile only the pure contract template with installed .NET, no packages.
        # This does not compile the Unity callback or validate Editor compatibility.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            config = dict(checkout=str(root), androidAppId='ca-app-pub-1111111111111111~2222222222',
                          productionRewardedAdUnit='ca-app-pub-1111111111111111/3333333333',
                          consoleInventoryConfirmed=True, productionActivationApproved=False,
                          regionalReviewApproved=False)
            valid = json.dumps(config)
            candidates = [valid, valid.replace('Approved', 'Approv\\u0065d'),
                          valid[:-1] + ',"a b":1,"a\\u0020b":2}',
                          valid[:-1] + ',"nested":{"x":1,"x":2}}',
                          valid[:-1] + ',"productionActivationApprov\\u0065d":false}',
                          valid + '{}', '[]', valid[:-1] + ',}', valid.replace('false', 'NaN', 1)]
            for evidence in ('[1,]', '{"x":1,}', 'NaN', 'Infinity', '-Infinity',
                             '01', '+1', '.1', '1.', '1e', '0x10', '/*comment*/true',
                             "'text'", '{bare:true}', 'undefined', '"bad\\q"'):
                candidates.append(valid[:-1] + ',"evidence":' + evidence + '}')
            candidates.extend([valid + '/*comment*/', valid + '\x00', valid[:-1],
                               valid[:-1] + ',"evidence":[true,false,null,-1.25e+2,"\\uAC00"]}',
                               ' \r\n' + valid + '\t ',
                               valid[:-1] + ',"evidence":{"when":"2026-09-29","items":[]}}'])
            for key in ('consoleInventoryConfirmed', 'productionActivationApproved', 'regionalReviewApproved'):
                for value in (None, 0, 1, 'false', [], {}, not config[key]):
                    candidates.append(json.dumps(dict(config, **{key: value})))
                missing = dict(config)
                del missing[key]
                missing['note'] = '"' + key + '":false'
                missing['nested'] = {key: config[key]}
                candidates.append(json.dumps(missing))
            for updates in ({'checkout': '.'}, {'checkout': str(root / 'other')},
                            {'androidAppId': 'ca-app-pub-3940256099942544~2222222222'},
                            {'productionRewardedAdUnit': 'ca-app-pub-4444444444444444/3333333333'}):
                candidates.append(json.dumps(dict(config, **updates)))
            expected = []
            for index, raw in enumerate(candidates):
                (root / ('case-' + str(index) + '.json')).write_text(raw, encoding='utf-8')
                try:
                    read_contract(raw, root)
                    expected.append('accept')
                except ValueError:
                    expected.append('reject')
            self.assertEqual(len(expected), 59)
            self.assertEqual(expected.count('accept'), 5)
            shutil.copyfile(Path(build.__file__).parent / 'private_ads_build/PrivateAdsContract.cs',
                            root / 'PrivateAdsContract.cs')
            checkout = Path(build.__file__).resolve().parents[2]
            packages = [p for p in (checkout / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*')
                        if json.loads((p / 'package.json').read_text(encoding='utf-8'))['version'] == '3.2.2']
            self.assertEqual(len(packages), 1, 'Exactly one existing locked Newtonsoft 3.2.2 required')
            shutil.copyfile(packages[0] / 'Runtime/Newtonsoft.Json.dll', root / 'Newtonsoft.Json.dll')
            (root / 'Contract.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework>'
                '<EnableDefaultCompileItems>true</EnableDefaultCompileItems>'
                '</PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json">'
                '<HintPath>Newtonsoft.Json.dll</HintPath></Reference></ItemGroup></Project>')
            (root / 'Program.cs').write_text('''using System; using System.IO;
class Program { static void Main(string[] args) {
for (int i = 0; i < int.Parse(args[1]); i++) {
try { PrivateAdsContract.Read(File.ReadAllBytes(Path.Combine(args[0], "case-"+i+".json")), args[0]); Console.WriteLine("accept"); }
catch { Console.WriteLine("reject"); }
}}}''')
            compiled = subprocess.run(['dotnet', 'build', str(root / 'Contract.csproj'), '--nologo',
                                       '-v:q', '-p:NuGetAudit=false'], capture_output=True, text=True,
                                      encoding='utf-8', errors='replace')
            self.assertEqual(compiled.returncode, 0, compiled.stdout + compiled.stderr)
            result = subprocess.run(['dotnet', str(root / 'bin/Debug/net9.0/Contract.dll'),
                                     str(root), str(len(candidates))], capture_output=True, text=True,
                                    encoding='utf-8', errors='replace')
            self.assertEqual(result.returncode, 0)
            self.assertEqual(result.stdout.splitlines(), expected)
            print('Synthetic strict contract: ' + str(len(candidates)) + ' matched; ' +
                  str(expected.count('accept')) + ' accepted, ' + str(expected.count('reject')) + ' rejected.')


if __name__ == '__main__':
    unittest.main()
