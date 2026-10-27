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
    def test_actual_entries_route_login_checks_before_build_for_both_approved_modes(self):
        template = (ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs').read_text()
        entries = template[template.index('    public static void Build()'):
                           template.index('    private static void BuildMode(')]
        start = template.index('            failureStage = "D07";')
        end = template.index('            failureStage = "D08";', start)
        preparation = template[start:end]
        self.assertLess(end, template.index('report = BuildPipeline.BuildPlayer'))
        output = ROOT / 'Logs/revival/approved-login-entry-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = '''using System;
class Probe {
 private static int next, test, built;
 private static string stage;
 private static void Step(int value) { if (next++ != value) throw new Exception("Order changed"); }
 private static string Required(string name) {
  if (name == "TAMER_PRIVATE_ADS_PLAYFAB_TITLE") {
   if (test == 1) throw new InvalidOperationException(); return "aBc123";
  }
  if (name == "TAMER_PRIVATE_ADS_PLAYFAB_SETTINGS_SHA256") {
   if (test == 2) throw new InvalidOperationException(); return new string('b', 64);
  }
  throw new Exception("Unexpected binding");
 }
 private static void CheckSnapshot() { Step(next == 0 ? 0 : 3); }
 private static void ValidatePlayFabSettings(string title, string hash) {
  Step(1);
  if (title != "aBc123" || hash != new string('b', 64)) throw new Exception("Changed binding");
  if (test == 3) throw new InvalidOperationException();
 }
 private static void PrepareGooglePlaySettings() { Step(2); if (test == 4) throw new InvalidOperationException(); }
ENTRIES
 private static void BuildMode(bool approved, bool privacy = false) {
  string failureStage = "";
  try {
PREPARATION
   built++;
  } finally { stage = failureStage; }
 }
 public static int Main() {
  int cases = 0;
  for (int mode = 0; mode < 3; mode++) {
   for (test = 0; test < 5; test++) {
    next = built = 0; stage = "";
    bool accepted = false;
    try {
     if (mode == 0) Build();
     if (mode == 1) BuildApprovedAdultRelease();
     if (mode == 2) BuildApprovedAdultPrivacy();
     accepted = true;
    } catch (InvalidOperationException) { }
    bool expected = mode == 0 || test == 0;
    if (accepted != expected || built != (expected ? 1 : 0)) return 1;
    int expectedSteps = mode == 0 ? 1 : test == 0 ? 4 : test < 3 ? 1 : test == 3 ? 2 : 3;
    if (next != expectedSteps) return 2;
    if (mode != 0 && test != 0 && stage != (test == 4 ? "D12" : "D13")) return 3;
    cases++;
   }
  }
  Console.WriteLine(cases); return 0;
 }
}'''.replace('ENTRIES', entries).replace('PREPARATION', preparation)
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
        self.assertEqual(executed.stdout.decode().strip(), '15')
        (output / 'result.json').write_text(json.dumps({'casesPassed': 15,
            'meaning': 'Actual entry methods and BuildMode preparation block with inert check probes; helper semantics are tested separately. No Unity build or SDK import.'}))

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
 public class GPGSProjectSettings {
  public static readonly GPGSProjectSettings Instance = new GPGSProjectSettings();
  public string Get(string key, string fallback) {
   Probe.Step(key == GPGSUtil.APPIDKEY ? 0 : 1);
   return key == GPGSUtil.APPIDKEY ? "synthetic-game" : "synthetic-client";
  }
 }
 public static class GPGSUtil {
  public const string APPIDKEY = "game", WEBCLIENTIDKEY = "client";
  public static void UpdateGameInfo() { Probe.Step(2); }
 }
}
namespace UnityEditor {
 public enum ImportAssetOptions { ForceSynchronousImport }
 public static class AssetDatabase {
  public static void SaveAssets() { Probe.Step(3); }
  public static void ImportAsset(string p, ImportAssetOptions o) { Probe.Step(4); Probe.Imported = true; }
  public static T LoadAssetAtPath<T>(string p) where T:class { Probe.Step(5); return Probe.Asset as T; }
 }
}
namespace UnityEngine {
 public static class Resources {
  public static T Load<T>(string p) where T:class {
   Probe.Step(6); if (!Probe.Imported || p != "PlayGamesSettings") throw new Exception();
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
  for (int c = 0; c < 8; c++) {
   next = 0; Imported = false;
   Asset = new GooglePlayGames.PlayGamesSettings(); Resource = Asset;
   if (c == 1) Asset = null;
   if (c == 2) Resource = null;
   if (c == 3) Resource = new GooglePlayGames.PlayGamesSettings();
   if (c == 4) Asset.AppId = " ";
   if (c == 5) Asset.WebClientId = " ";
   if (c == 6) Asset.AppId = "different-game";
   if (c == 7) Asset.WebClientId = "different-client";
   bool built = false;
   try { PrepareGooglePlaySettings(); built = true; }
   catch (InvalidOperationException) { }
   if (built != (c == 0) || next != 7 || !Imported) return 1;
  }
  Console.WriteLine(8); return 0;
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
        self.assertEqual(executed.stdout.decode().strip(), '8')
        (output / 'result.json').write_text(json.dumps({'casesPassed': 8,
            'meaning': 'Actual method with inert import/resource probes; no Unity build, SDK, OAuth mapping or device verification.'}))


if __name__ == '__main__':
    unittest.main()
