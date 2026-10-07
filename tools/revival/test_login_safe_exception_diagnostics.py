"""Compile the actual formatter and SDK exception declarations without SDK calls."""
from pathlib import Path
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class LoginExceptionDiagnosticsChecks(unittest.TestCase):
    def test_fixed_sdk_codes_exclude_exception_messages_and_unknown_types(self):
        login = (ROOT / 'Assets/Scripts/Login/Login.cs').read_text(encoding='utf-8-sig')
        start = login.index('        private static string DescribeRequestException(')
        end = login.index('        /// <summary>', start)
        formatter = login[start:end]
        self.assertIn('request exception: {DescribeRequestException(e)}', login)
        sdk = (ROOT / 'Assets/ThirdParty/PlayFabSDK/Shared/Internal/PlayFabErrors.cs').read_text()
        start = sdk.index('    public class PlayFabException : Exception')
        declarations = sdk[start:sdk.rfind('}')]
        output = ROOT / 'Logs/revival/login-safe-exception-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = '''using System;
using PlayFab;
namespace PlayFab { DECLARATIONS }
class Probe {
FORMATTER
 public static int Main() {
  string[] expected = { "AuthContextRequired", "BuildError", "DeveloperKeyNotSet",
   "EntityTokenNotSet", "NotLoggedIn", "TitleNotSet" };
  for (int i = 0; i < expected.Length; i++) {
   var error = new PlayFabException((PlayFabExceptionCode)i, "private-message-canary");
   if (DescribeRequestException(error) != "PlayFabException: " + expected[i]) return 1;
  }
  if (DescribeRequestException(new PlayFabException((PlayFabExceptionCode)999, "private-message-canary")) != "PlayFabException: UnknownCode") return 2;
  if (DescribeRequestException(new Exception("private-message-canary")) != "NonPlayFabException") return 3;
  if (DescribeRequestException(null) != "NonPlayFabException") return 4;
  Console.WriteLine(9); return 0;
 }
}'''.replace('DECLARATIONS', declarations).replace('FORMATTER', formatter)
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
        self.assertEqual(executed.stdout.decode().strip(), '9')


if __name__ == '__main__':
    unittest.main()
