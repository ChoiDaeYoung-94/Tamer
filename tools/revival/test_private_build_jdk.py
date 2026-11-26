"""Exercise the actual producer JDK guard with inert settings and synthetic files."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class PrivateBuildJdkChecks(unittest.TestCase):
    def test_selected_path_hash_and_read_only_entry_guard(self):
        template_path = ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs'
        template_raw = template_path.read_bytes()
        template = template_raw.decode('utf-8')
        first = template.index('            CheckAndroidJdk();')
        self.assertLess(first, template.index('using (var settings = new SettingsScope())'))
        snapshot = template[template.index('    private static void CheckSnapshot()'):]
        self.assertTrue(snapshot.split('{', 1)[1].lstrip().startswith('CheckAndroidJdk();'))
        methods = template[template.index('    private static void CheckAndroidJdk()'):
                           template.index('    private static void ValidatePlayFabSettings(')]
        methods += template[template.index('    private static bool IsFullyQualifiedKeystorePath('):
                            template.index('    private static void CheckSourceManifest(')]
        output = ROOT / 'Logs/revival/private-build-jdk-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = r'''using System;
using System.IO;
using System.Security.Cryptography;
namespace UnityEditor.Android {
 public static class AndroidExternalToolsSettings {
  public static string selected; public static bool fail; public static int reads, writes;
  public static string jdkRootPath {
   get { reads++; if (fail) throw new InvalidOperationException(); return selected; }
   set { writes++; throw new Exception("Must not change shared preferences"); }
  }
 }
}
class Probe {
 private static string editor, expected;
 private static string Required(string name) {
  if (name == "TAMER_PRIVATE_ADS_EDITOR") return editor;
  if (name == "TAMER_PRIVATE_ADS_JAVA_SHA256") return expected;
  throw new Exception("Unexpected binding");
 }
 private static string Hash(byte[] bytes) {
  using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
 }
 private static Exception Rejected() { return new InvalidOperationException("rejected"); }
METHODS
 private static int cases;
 private static void Check(string root, bool accepted) {
  UnityEditor.Android.AndroidExternalToolsSettings.selected = root;
  bool result = true;
  try { CheckAndroidJdk(); } catch { result = false; }
  if (result != accepted || UnityEditor.Android.AndroidExternalToolsSettings.writes != 0)
   throw new Exception("Guard case failed: " + cases);
  cases++;
 }
 public static int Main(string[] args) {
  var root = Path.GetFullPath(args[0]);
  editor = Path.Combine(root, "Hub/Editor/Unity.exe");
  var jdk = Path.Combine(Path.GetDirectoryName(editor), "Data/PlaybackEngines/AndroidPlayer/OpenJDK");
  var java = Path.Combine(jdk, "bin/java.exe");
  Directory.CreateDirectory(Path.GetDirectoryName(java)); File.WriteAllBytes(java, new byte[] {1,2,3});
  expected = Hash(File.ReadAllBytes(java));
  Check(jdk, true); Check(jdk + Path.DirectorySeparatorChar, true); Check(jdk.ToUpperInvariant(), true);
  Check(jdk.Replace((char)92, '/'), true);
  Check(null, false); Check("", false); Check(" ", false); Check("OpenJDK", false);
  Check("C:OpenJDK", false); Check("\\OpenJDK", false);
  var copy = Path.Combine(root, "identical-copy"); Directory.CreateDirectory(Path.Combine(copy, "bin"));
  File.Copy(java, Path.Combine(copy, "bin/java.exe")); Check(copy, false);
  Check(Path.Combine(jdk, "nested"), false); Check(jdk + "\0", false);
  expected = new string('0', 64); Check(jdk, false);
  expected = ""; Check(jdk, false); expected = null; Check(jdk, false);
  expected = Hash(new byte[] {1,2,3});
  File.WriteAllBytes(java, new byte[] {4,5,6}); Check(jdk, false);
  File.Delete(java); Check(jdk, false);
  File.WriteAllBytes(java, new byte[] {1,2,3});
  var savedEditor = editor; editor = "Unity.exe"; Check(jdk, false); editor = savedEditor;
  UnityEditor.Android.AndroidExternalToolsSettings.fail = true; Check(jdk, false);
  UnityEditor.Android.AndroidExternalToolsSettings.fail = false; Check(jdk, true);
  if (UnityEditor.Android.AndroidExternalToolsSettings.reads != cases) return 2;
  Console.WriteLine(cases); return 0;
 }
}'''.replace('METHODS', methods)
        file = output / 'probe.cs'
        file.write_bytes(source.encode('utf-8'))
        exe = output / 'probe.exe'
        args = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                '-nologo', '-langversion:9.0', '-target:exe', '-out:' + str(exe)]
        args += ['-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / name)
                 for name in ('mscorlib.dll', 'System.dll', 'System.Core.dll')]
        compiled = subprocess.run([*args, str(file)], capture_output=True)
        (output / 'compile.private.log').write_bytes(compiled.stdout + compiled.stderr)
        self.assertEqual(compiled.returncode, 0, 'Guard probe compilation failed; preserve log')
        executed = subprocess.run([str(exe), str(output / 'synthetic')], capture_output=True)
        (output / 'execute.private.log').write_bytes(executed.stdout + executed.stderr)
        self.assertEqual(executed.returncode, 0, 'Guard probe failed; preserve log')
        self.assertEqual(executed.stdout.decode().strip(), '21')
        (output / 'result.private.json').write_text(json.dumps(dict(
            casesPassed=21, sharedPreferenceWrites=0, unityEditorLaunches=0,
            javaGradleLaunches=0, sdkServiceCalls=0,
            sourceSha256=hashlib.sha256(template_raw).hexdigest(),
            scope='Actual JDK guard/getter route with inert settings; not a live Editor or signed build.'
        ), indent=2), encoding='utf-8')


if __name__ == '__main__':
    unittest.main()
