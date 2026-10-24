"""Verify real baseline argument forwarding/refusal before any Editor or recovery write."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SHELL = shutil.which('pwsh') or shutil.which('powershell')
SCRIPT = Path(__file__).with_name('Run-Baseline.ps1')


@unittest.skipUnless(SHELL, 'PowerShell is required')
class BaselineAssetInputTests(unittest.TestCase):
    def exercise(self, source=False, approval=False, sdk=False):
        with tempfile.TemporaryDirectory(prefix='tamer baseline inputs ') as folder:
            root = Path(folder)
            settings = root / 'ProjectSettings/ProjectSettings.asset'
            settings.parent.mkdir()
            original = b'PlayerSettings:\n  AndroidKeyaliasName: synthetic-preserve\n'
            settings.write_bytes(original)
            editor = root / 'Never execute Editor.exe'
            editor.write_bytes(b'not an executable')
            capture = root / 'verifier-arguments.json'
            verifier = root / 'fake verifier.ps1'
            verifier.write_text('''$ErrorActionPreference = 'Stop'
$captured = ConvertTo-Json -InputObject @($args) -Compress
[IO.File]::WriteAllText($env:TAMER_BASELINE_ARGUMENT_CAPTURE, $captured)
if ($args[0] -eq '-m') { $global:LASTEXITCODE = 0 }
else { $global:LASTEXITCODE = 73 }
''', encoding='utf-8')
            asset_source = root / 'source with spaces & [literal]'
            asset_source.mkdir()
            ledger = root / 'approval with spaces.json'
            ledger.write_text('{"synthetic":true}', encoding='utf-8')
            script = SCRIPT.with_name('Run-SdkValidation.ps1') if sdk else SCRIPT
            command = [SHELL, '-NoProfile', '-NonInteractive', '-File', str(script),
                       '-ProjectPath', str(root), '-EditorPath', str(editor), '-PythonPath', str(verifier)]
            if source:
                command += ['-AssetSource', str(asset_source)]
            if approval:
                command += ['-PrivateMetaApproval', str(ledger)]
            result = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', errors='replace',
                                    env=dict(os.environ, TAMER_BASELINE_ARGUMENT_CAPTURE=str(capture)), timeout=30)
            self.assertNotEqual(result.returncode, 0, 'The synthetic verifier always refuses.')
            self.assertEqual(settings.read_bytes(), original)
            self.assertFalse((root / 'Logs').exists(), 'Refusal must happen before snapshot/test/build writes.')
            if approval and not source:
                self.assertFalse(capture.exists(), 'An incomplete approval input must not reach the verifier.')
                self.assertIn('PrivateMetaApproval requires AssetSource', result.stderr)
            else:
                expected = ['tools/revival/restore_assets.py', '--verify']
                if source:
                    expected += ['--source', str(asset_source)]
                if approval:
                    expected += ['--private-meta-approval', str(ledger)]
                self.assertEqual(json.loads(capture.read_text(encoding='utf-8-sig')), expected)
                self.assertIn('Asset verification failed', result.stderr)

    def test_default_stays_strict_without_source_or_approval(self):
        self.exercise()

    def test_explicit_source_preserves_literal_path(self):
        self.exercise(source=True)

    def test_approval_and_source_are_forwarded_without_evaluation(self):
        self.exercise(source=True, approval=True)

    def test_approval_without_source_refuses_before_execution(self):
        self.exercise(approval=True)

    def test_sdk_runner_forwards_inputs_to_baseline(self):
        self.exercise(source=True, approval=True, sdk=True)

    def test_sdk_runner_refuses_incomplete_input_before_python_tests(self):
        self.exercise(approval=True, sdk=True)


if __name__ == '__main__':
    unittest.main()
