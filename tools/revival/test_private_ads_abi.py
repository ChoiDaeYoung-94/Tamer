"""Execute the actual template's ABI guards with inert types; no Editor or SDK."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import unittest
import uuid

from private_ads_evidence import create_private_directory

ROOT = Path(__file__).resolve().parents[2]


class AbiGuardTests(unittest.TestCase):
    def test_compiled_guard_rejects_missing_wrong_and_changed_abi_contracts(self):
        source = ROOT / 'tools/revival/private_ads_build/PrivateProductionAdsBuild.cs'
        raw = source.read_text(encoding='utf-8')
        guards = raw.split('    private static AndroidArchitecture ReadArchitectures(', 1)[1]
        guards = '    private static AndroidArchitecture ReadArchitectures(' + guards.split(
            '    private static string Hash(', 1)[0]
        output = ROOT / ('Logs/revival/internal-abi-guard-' + uuid.uuid4().hex)
        output.parent.mkdir(parents=True, exist_ok=True)
        create_private_directory(output)
        program = '''using System; using System.Linq;
[Flags] enum AndroidArchitecture { ARM64 = 2, X86_64 = 8 }
class Probe {
    static bool approvedPrivacy;
    class Snapshot { public AndroidArchitecture TargetArchitectures; public string AbiProfile; public string[] ExpectedAbis; }
    static Snapshot snapshot;
    static Exception Rejected() { return new InvalidOperationException("Synthetic ABI contract rejected"); }
''' + guards + '''
    static int count;
    static void Check(bool valid) { if (!valid) throw new Exception("Synthetic ABI assertion"); count++; }
    static void Set(string profile, string abis, string version = "1") {
        Environment.SetEnvironmentVariable("TAMER_PRIVATE_ADS_ABI_PROFILE_VERSION", version);
        Environment.SetEnvironmentVariable("TAMER_PRIVATE_ADS_ABI_PROFILE", profile);
        Environment.SetEnvironmentVariable("TAMER_PRIVATE_ADS_EXPECTED_ABIS", abis);
    }
    static void Reject() {
        try { string p; string[] a; ReadArchitectures(out p, out a); }
        catch (InvalidOperationException) { count++; return; }
        throw new Exception("Synthetic invalid ABI accepted");
    }
    static void Main() {
        string p; string[] a;
        Set("arm64", "arm64-v8a");
        Check(ReadArchitectures(out p, out a) == AndroidArchitecture.ARM64 && p == "arm64" && a.SequenceEqual(new[]{"arm64-v8a"}));
        Set("internal_dual64", "arm64-v8a,x86_64");
        var target = ReadArchitectures(out p, out a);
        Check(target == (AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64));
        snapshot = new Snapshot { TargetArchitectures = target, AbiProfile = p, ExpectedAbis = a };
        Check(AbiEnvironmentMatches());
        snapshot.TargetArchitectures = AndroidArchitecture.ARM64; Check(!AbiEnvironmentMatches());
        snapshot.TargetArchitectures = target;
        snapshot.ExpectedAbis = new[]{"arm64-v8a"}; Check(!AbiEnvironmentMatches());
        snapshot.ExpectedAbis = a;
        Set("arm64", "arm64-v8a"); Check(!AbiEnvironmentMatches());
        Set("internal_dual64", "arm64-v8a,x86_64"); approvedPrivacy = true; Reject();
        Set("arm64", "arm64-v8a"); Check(ReadArchitectures(out p, out a) == AndroidArchitecture.ARM64);
        approvedPrivacy = false;
        Set(null, "arm64-v8a"); Reject();
        Set("arm64", "arm64-v8a", null); Reject();
        Set("arm64", "arm64-v8a", "2"); Reject();
        Set("x86_64", "x86_64"); Reject();
        Set("internal_dual64", "x86_64,arm64-v8a"); Reject();
        Set("internal_dual64", "arm64-v8a"); Reject();
        Set("internal_dual64", "arm64-v8a,x86_64,x86_64"); Reject();
        Set("internal_dual64", null); Reject();
        Console.WriteLine("abiGuardCases=" + count);
    }
}'''
        code = output / 'Probe.cs'
        code.write_text(program, encoding='utf-8')
        exe = output / 'Probe.exe'
        compiler = Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
        self.assertTrue(compiler.is_file(), 'Existing .NET Framework compiler required')
        child_env = {k: v for k, v in os.environ.items() if not k.startswith('TAMER_PRIVATE_ADS_')
                     and k not in ('TAMER_KEYSTORE_PASS', 'TAMER_KEYALIAS_PASS')}
        compiled = subprocess.run([str(compiler), '/nologo', '/target:exe', '/out:' + str(exe), str(code)],
                                  capture_output=True, text=True, timeout=60, env=child_env)
        (output / 'compile.private.log').write_text(compiled.stdout + compiled.stderr, encoding='utf-8')
        self.assertEqual(compiled.returncode, 0, 'Inert ABI guard compile failed; preserve private log')
        executed = subprocess.run([str(exe)], capture_output=True, text=True, timeout=30, env=child_env)
        (output / 'execution.private.log').write_text(executed.stdout + executed.stderr, encoding='utf-8')
        report = dict(templateSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                      extractedGuardSha256=hashlib.sha256(guards.encode()).hexdigest(),
                      compileExit=compiled.returncode, executionExit=executed.returncode,
                      expectedCases=16, editorLaunches=0, sdkCalls=0, keyAccess=0,
                      actualAndroidArchitectureEnumUsed=False, completeTemplateCompileRequired=True)
        (output / 'result.private.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        self.assertEqual(executed.returncode, 0, 'Inert ABI guard execution failed; preserve private log')
        self.assertEqual(executed.stdout.strip(), 'abiGuardCases=16')


if __name__ == '__main__':
    unittest.main()
