"""Compile the actual formatter and SDK exception declarations without SDK calls."""
from pathlib import Path
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows compiler required')
class LoginOutcomeDiagnosticsChecks(unittest.TestCase):
    def test_outcomes_include_only_defined_error_codes_and_fixed_labels(self):
        login = (ROOT / 'Assets/Scripts/Login/Login.cs').read_text(encoding='utf-8-sig')
        start = login.index('        private static string DescribeGoogleLoginOutcome(')
        end = login.index('        private static string DescribeRequestException(', start)
        formatter = login[start:end]
        self.assertIn('DescribeGoogleLoginOutcome(login.IsTimeout, login.Error,', login)
        sdk = (ROOT / 'Assets/ThirdParty/PlayFabSDK/Shared/Internal/PlayFabErrors.cs').read_text()
        start = sdk.index('    public enum PlayFabErrorCode')
        end = sdk.index('    }', start) + 5
        declarations = sdk[start:end] + '\npublic class PlayFabError { public PlayFabErrorCode Error; public string ErrorMessage; }'
        output = ROOT / 'Logs/revival/login-safe-outcome-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        source = '''using System;
using PlayFab;
namespace PlayFab { DECLARATIONS }
class Probe {
FORMATTER
 public static int Main() {
  var error = new PlayFabError { Error = PlayFabErrorCode.AccountNotFound, ErrorMessage = "private-canary" };
  if (DescribeGoogleLoginOutcome(false, error, false, false) != "ServerError: AccountNotFound") return 1;
  error.Error = (PlayFabErrorCode)int.MaxValue;
  if (DescribeGoogleLoginOutcome(false, error, false, false) != "ServerError: UnknownCode") return 2;
  if (DescribeGoogleLoginOutcome(true, error, false, false) != "TimeoutOrRequestException") return 3;
  if (DescribeGoogleLoginOutcome(false, null, false, false) != "NullResult") return 4;
  if (DescribeGoogleLoginOutcome(false, null, true, true) != "NewAccountRejected") return 5;
  if (DescribeGoogleLoginOutcome(false, null, true, false) != "ExistingAccountResponse") return 6;
  Console.WriteLine(6); return 0;
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
        self.assertEqual(executed.stdout.decode().strip(), '6')


if __name__ == '__main__':
    unittest.main()
