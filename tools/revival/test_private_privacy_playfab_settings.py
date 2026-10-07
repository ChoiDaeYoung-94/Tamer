"""Exercise reviewed title binding and the actual guard with inert Unity probes."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import uuid

import private_ads_producer as producer

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


class PrivacyPlayFabSettingsChecks(unittest.TestCase):
    def test_title_is_read_only_from_the_reviewed_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            backup = root / 'before.snapshot'
            for raw, accepted in [(b'  TitleId: ABCDE\n', True),
                                  (b'  TitleId: \n', False),
                                  (b'  TitleId: ABCDE \n', False),
                                  (b'  TitleId: ABCDE\n  TitleId: ABCDE\n', False)]:
                backup.write_bytes(raw)
                digest = hashlib.sha256(raw).hexdigest()
                plan = {'before': {producer.PLAYFAB_SETTINGS: {
                    'backup': 'before.snapshot', 'sha256': digest}}}
                if accepted:
                    self.assertEqual(producer.reviewed_playfab_binding(root, plan), ('ABCDE', digest))
                    backup.write_bytes(b'  TitleId: FEDCB\n')
                    with self.assertRaises(ValueError):
                        producer.reviewed_playfab_binding(root, plan)
                else:
                    with self.assertRaises(ValueError):
                        producer.reviewed_playfab_binding(root, plan)

    @unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
    def test_actual_guard_rejects_missing_changed_or_mismatched_settings(self):
        template = (ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs').read_text()
        start = template.index('    private static void ValidatePlayFabSettings(')
        method = template[start:template.index('    private static void PrepareGooglePlaySettings()', start)]
        call = template.index('                ValidatePlayFabSettings(')
        self.assertLess(call, template.index('                PrepareGooglePlaySettings();'))
        self.assertLess(call, template.index('report = BuildPipeline.BuildPlayer'))
        output = ROOT / 'Logs/revival/privacy-playfab-settings-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        settings = output / producer.PLAYFAB_SETTINGS
        settings.parent.mkdir(parents=True)
        settings.write_bytes(b'inert-settings')
        source = '''using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
public class PlayFabSharedSettings { public string TitleId = "ABCDE"; }
namespace PlayFab { public static class PlayFabSettings { public static string TitleId; } }
namespace UnityEditor {
 public enum ImportAssetOptions { ForceSynchronousImport }
 public static class AssetDatabase {
  public static void ImportAsset(string p, ImportAssetOptions o) { Probe.Imported = true; }
  public static T LoadAssetAtPath<T>(string p) where T:class { return Probe.Asset as T; }
 }
}
namespace UnityEngine {
 public static class Resources {
  public static T[] LoadAll<T>(string p) where T:class {
   if (!Probe.Imported || p != "PlayFabSharedSettings") throw new Exception();
   return Array.ConvertAll(Probe.ResourceAssets, value => value as T);
  }
 }
}
class Probe {
 public static PlayFabSharedSettings Asset;
 public static PlayFabSharedSettings[] ResourceAssets;
 public static bool Imported;
 private static Exception Rejected() => new InvalidOperationException();
 private static string Hash(byte[] raw) {
  using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
 }
METHOD
 public static int Main() {
  string digest = Hash(File.ReadAllBytes("SETTINGS_PATH"));
  for (int c = 0; c < 10; c++) {
   Imported = false; Asset = new PlayFabSharedSettings(); ResourceAssets = new[] { Asset };
   PlayFab.PlayFabSettings.TitleId = "ABCDE";
   string expected = "ABCDE", expectedHash = digest;
   if (c == 1) Asset = null;
   if (c == 2) ResourceAssets = new PlayFabSharedSettings[0];
   if (c == 3) ResourceAssets = new[] { Asset, Asset };
   if (c == 4) ResourceAssets = new[] { new PlayFabSharedSettings() };
   if (c == 5) Asset.TitleId = " ";
   if (c == 6) Asset.TitleId = "FEDCB";
   if (c == 7) PlayFab.PlayFabSettings.TitleId = "FEDCB";
   if (c == 8) expectedHash = new string('0', 64);
   if (c == 9) expected = " ";
   bool mayBuild = false;
   try { ValidatePlayFabSettings(expected, expectedHash); mayBuild = true; }
   catch (InvalidOperationException) { }
   if (mayBuild != (c == 0)) return 1;
  }
  Console.WriteLine(10); return 0;
 }
}'''.replace('METHOD', method).replace('SETTINGS_PATH', producer.PLAYFAB_SETTINGS)
        file = output / 'probe.cs'
        file.write_text(source)
        exe = output / 'probe.exe'
        args = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                '-nologo', '-langversion:9.0', '-target:exe', '-out:' + str(exe)]
        args += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
                 for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        compiled = subprocess.run([*args, str(file)], capture_output=True, cwd=output)
        (output / 'compile.log').write_bytes(compiled.stdout + compiled.stderr)
        self.assertEqual(compiled.returncode, 0)
        executed = subprocess.run([str(exe)], capture_output=True, cwd=output)
        (output / 'execute.log').write_bytes(executed.stdout + executed.stderr)
        self.assertEqual(executed.returncode, 0)
        self.assertEqual(executed.stdout.decode().strip(), '10')
        (output / 'result.json').write_text(json.dumps({'casesPassed': 10,
            'meaning': 'Actual C# method with synthetic settings and inert import/resource probes; no Unity build, SDK, API or device verification.'}))


if __name__ == '__main__':
    unittest.main()
