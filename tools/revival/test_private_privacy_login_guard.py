"""Compile the actual marker and login entry guard against inert side-effect probes."""
import json
from pathlib import Path
import re
import subprocess
import sys
import unittest
import uuid

ROOT = Path(__file__).resolve().parents[2]
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data')


@unittest.skipUnless(sys.platform == 'win32', 'Pinned Windows Unity compiler required')
class PrivacyLoginGuardChecks(unittest.TestCase):
    def test_trial_guard_precedes_device_side_effects_and_preserves_other_contexts(self):
        contract = (ROOT / 'Assets/Scripts/Advertising/PrivatePrivacyReleaseContract.cs').read_text(encoding='utf-8-sig')
        login = (ROOT / 'Assets/Scripts/Login/Login.cs').read_text(encoding='utf-8-sig')
        marker = re.search(r'public static bool IsPrivatePrivacyTrial\s*=>[^;]+;', contract).group()
        declaration = re.search(r'private static readonly Binding ApprovedPrivacy\s*=\s*null;', contract).group()
        start = login.index('private async UniTask<bool> LoginWithDeviceAsync(CancellationToken token)')
        start = login.index('{', start) + 1
        end = login.index('            ShowLoading("LogIn...");', start)
        prefix = login[start:end]
        output = ROOT / 'Logs/revival/privacy-login-guard-checks' / uuid.uuid4().hex
        output.mkdir(parents=True)
        results = []
        for name, defines, bound, blocked in (
            ('public_android', 'UNITY_ANDROID', False, False),
            ('private_android', 'UNITY_ANDROID', True, True),
            ('private_editor', 'UNITY_ANDROID,UNITY_EDITOR', True, False),
            ('private_other_platform', '', True, False),
        ):
            binding = declaration.replace('= null;', '= new Binding();') if bound else declaration
            source = '''using System;
using System.Threading;
using System.Threading.Tasks;
namespace AD.Advertising {
    public static class AgeTreatmentPolicy { public static bool PrivacySdkEnvironmentReviewed => true; }
    public static class PrivatePrivacyReleaseContract {
        private sealed class Binding { }
        BINDING
        MARKER
    }
}
internal sealed class Probe {
    private string _loginFailureMessage;
    internal int PreferenceWrites, IdentityReads, DeviceRequests;
    private bool LoginCancelled(CancellationToken token) => token.IsCancellationRequested;
    internal async Task<bool> Enter(CancellationToken token) {
PREFIX
        // Instrument the first point where the original method can do device work.
        PreferenceWrites++; IdentityReads++; DeviceRequests++;
        return true;
    }
    public static int Main() {
        int cases = 0;
        foreach (bool cancelled in new [] { false, true }) {
                var probe = new Probe();
                bool value = probe.Enter(new CancellationToken(cancelled)).GetAwaiter().GetResult();
                bool rejected = cancelled || BLOCKED;
                if (value == rejected || probe.PreferenceWrites != (rejected ? 0 : 1) ||
                    probe.IdentityReads != (rejected ? 0 : 1) || probe.DeviceRequests != (rejected ? 0 : 1)) return 1;
                if (!cancelled && BLOCKED && string.IsNullOrEmpty(probe._loginFailureMessage)) return 2;
                cases++;
        }
        Console.WriteLine(cases);
        return 0;
    }
}'''.replace('BINDING', binding).replace('MARKER', marker).replace('PREFIX', prefix).replace('BLOCKED', str(blocked).lower())
            file = output / (name + '.cs')
            file.write_bytes(source.encode('utf-8'))
            exe = output / (name + '.exe')
            args = [str(EDITOR / 'netcorerun/netcorerun.exe'), str(EDITOR / 'DotNetSdkRoslyn/csc.dll'),
                    '-nologo', '-langversion:9.0', '-target:exe', '-out:' + str(exe)]
            if defines:
                args.append('-define:' + defines)
            for assembly in ('mscorlib.dll', 'System.dll', 'System.Core.dll'):
                args.append('-r:' + str(EDITOR / 'UnityReferenceAssemblies/unity-4.8-api' / assembly))
            compiled = subprocess.run([*args, str(file)], capture_output=True)
            (output / (name + '.compile.log')).write_bytes(compiled.stdout + compiled.stderr)
            self.assertEqual(compiled.returncode, 0, 'Pure guard compilation failed: ' + name)
            executed = subprocess.run([str(exe)], capture_output=True)
            (output / (name + '.execute.log')).write_bytes(executed.stdout + executed.stderr)
            self.assertEqual(executed.returncode, 0, 'Pure guard side-effect probe failed: ' + name)
            self.assertEqual(executed.stdout.decode().strip(), '2')
            results.append({'context': name, 'casesPassed': 2, 'compiled': True, 'executed': True})
        (output / 'result.json').write_text(json.dumps({'results': results, 'caseCount': 8,
            'meaning': 'Actual source marker and pre-side-effect guard compiled with inert probes. Not full Login compilation, Unity, SDK or account preservation proof.'}), encoding='utf-8')


if __name__ == '__main__':
    unittest.main()
