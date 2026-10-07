"""Compile actual server-code control flow against inert callback probes."""
from pathlib import Path
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class LoginServerCodeDiagnosticsChecks(unittest.TestCase):
    def test_actual_server_code_method_records_fixed_terminal_stages(self):
        login = (ROOT / 'Assets/Scripts/Login/Login.cs').read_text(encoding='utf-8-sig')
        start = login.index('        private async UniTask<string> RequestGoogleServerCodeAsync(')
        end = login.index('#endif', start)
        formatter = login[start:end].replace('UniTask<string>', 'System.Threading.Tasks.Task<string>')
        declarations = ''
        output = ROOT / 'Logs/revival/login-server-code-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = """using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
class LoginCallbackGate { public bool IsCompleted; public bool TryComplete(bool cancelled) { if (cancelled || IsCompleted) return false; IsCompleted=true; return true; } public void Expire() {} }
class PlayGamesPlatform { public static PlayGamesPlatform Instance = new PlayGamesPlatform(); public void RequestServerSideAccess(bool force, Action<string> callback) { if (Probe.Case==4) throw new Exception("private-canary"); if (Probe.Case==2 || Probe.Case==3) return; callback(Probe.Case==1 ? " " : "private-auth-code-canary"); } }
class Probe {
 public static int Case;
 private string _loginFailureMessage;
 private TimeSpan GpgsTimeout = TimeSpan.FromSeconds(20);
 private static List<string> Logs = new List<string>();
 private bool LoginCancelled(CancellationToken token) { return Case==3 && Logs.Contains("ServerCode: Requested"); }
 private Task<bool> WaitUntilAsync(Func<bool> ready, TimeSpan timeout, CancellationToken token) { return Task.FromResult(ready()); }
 private static void LogStep(string text) { Logs.Add(text); }
METHOD
 public static int Main() {
  string[] expected = { "ServerCode: CallbackReceived", "ServerCode: CallbackEmpty", "ServerCode: Timeout", "ServerCode: Cancelled", "ServerCode: RequestException" };
  for (Case=0;Case<5;Case++) {
   Logs.Clear(); var result = new Probe().RequestGoogleServerCodeAsync(CancellationToken.None).GetAwaiter().GetResult();
   if (!Logs.Contains("ServerCode: Requested") || !Logs.Contains(expected[Case])) return 1;
   foreach(var line in Logs) if (line.Contains("canary")) return 2;
   if (Case==0 && result!="private-auth-code-canary") return 3;
   if (Case>1 && result!=null) return 4;
  }
  Console.WriteLine(5); return 0;
 }
}""".replace('METHOD', formatter)
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
        self.assertEqual(executed.stdout.decode().strip(), '5')


if __name__ == '__main__':
    unittest.main()
