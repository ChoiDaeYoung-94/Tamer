"""Compile the actual pre-build settings preparation against inert Unity probes."""
import json
from pathlib import Path
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class PrivacyGoogleSettingsChecks(unittest.TestCase):
    def test_settings_import_and_load_are_required_before_build(self):
        template = (ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs').read_text()
        start = template.index('    private static void PrepareGooglePlaySettings()')
        method = template[start:template.index('    public void OnPreprocessBuild', start)]
        call = template.index('                PrepareGooglePlaySettings();')
        self.assertLess(call, template.index('report = BuildPipeline.BuildPlayer'))
        output = ROOT / 'Logs/revival/privacy-gpgs-settings-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = '''using System;
using UnityEditor;
using UnityEngine;
namespace GooglePlayGames {
 public class PlayGamesSettings { public string AppId = "synthetic-game", WebClientId = "synthetic-client"; }
}
namespace GooglePlayGames.Editor {
 public static class GPGSUtil { public static void UpdateGameInfo() { Probe.Step(0); } }
}
namespace UnityEditor {
 public enum ImportAssetOptions { ForceSynchronousImport }
 public static class AssetDatabase {
  public static void SaveAssets() { Probe.Step(1); }
  public static void ImportAsset(string p, ImportAssetOptions o) { Probe.Step(2); Probe.Imported = true; }
  public static T LoadAssetAtPath<T>(string p) where T:class { Probe.Step(3); return Probe.Asset as T; }
 }
}
namespace UnityEngine {
 public static class Resources {
  public static T Load<T>(string p) where T:class {
   Probe.Step(4); if (!Probe.Imported || p != "PlayGamesSettings") throw new Exception();
   return Probe.Resource as T;
  }
 }
}
class Probe {
 public static GooglePlayGames.PlayGamesSettings Asset, Resource;
 public static bool Imported;
 private static int next;
 public static void Step(int value) { if (next++ != value) throw new Exception(); }
 private static Exception Rejected() => new InvalidOperationException();
METHOD
 public static int Main() {
  for (int c = 0; c < 6; c++) {
   next = 0; Imported = false;
   Asset = new GooglePlayGames.PlayGamesSettings(); Resource = Asset;
   if (c == 1) Asset = null;
   if (c == 2) Resource = null;
   if (c == 3) Resource = new GooglePlayGames.PlayGamesSettings();
   if (c == 4) Asset.AppId = " ";
   if (c == 5) Asset.WebClientId = " ";
   bool built = false;
   try { PrepareGooglePlaySettings(); built = true; }
   catch (InvalidOperationException) { }
   if (built != (c == 0) || next != 5 || !Imported) return 1;
  }
  Console.WriteLine(6); return 0;
 }
}'''.replace('METHOD', method)
        file = output / 'probe.cs'
        file.write_text(source)
        exe = output / 'probe.exe'
        args = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                '-nologo', '-langversion:9.0', '-target:exe', '-out:' + str(exe)]
        args += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
                 for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        compiled = subprocess.run([*args, str(file)], capture_output=True)
        (output / 'compile.log').write_bytes(compiled.stdout + compiled.stderr)
        self.assertEqual(compiled.returncode, 0)
        executed = subprocess.run([str(exe)], capture_output=True)
        (output / 'execute.log').write_bytes(executed.stdout + executed.stderr)
        self.assertEqual(executed.returncode, 0)
        self.assertEqual(executed.stdout.decode().strip(), '6')
        (output / 'result.json').write_text(json.dumps({'casesPassed': 6,
            'meaning': 'Actual method with inert import/resource probes; no Unity build, SDK, OAuth mapping or device verification.'}))


if __name__ == '__main__':
    unittest.main()
