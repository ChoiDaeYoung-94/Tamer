"""Exercise byte recovery and refusal paths without launching a Unity Editor."""
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

SHELL = shutil.which('pwsh') or shutil.which('powershell')
HELPER = Path(__file__).with_name('ProjectSettingsSnapshot.ps1')


@unittest.skipUnless(SHELL, 'PowerShell is required for settings recovery tests')
class SettingsSnapshotTests(unittest.TestCase):
    def exercise(self, body):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'ProjectSettings').mkdir()
            path = root / 'ProjectSettings/ProjectSettings.asset'
            original = b'PlayerSettings:\r\n  AndroidKeyaliasName: synthetic-alias\r\n'
            path.write_bytes(original)
            script = root / 'exercise.ps1'
            script.write_text('''param($Helper, $Root)
$ErrorActionPreference = 'Stop'
. $Helper
function Get-RevivalProjectEditor { param($ProjectPath) if ($script:resident) { 'synthetic editor' } }
$snapshot = Save-RevivalProjectSettings -ProjectPath $Root
[IO.File]::WriteAllText($snapshot.Path, 'Unity startup changed this file')
''' + body, encoding='utf-8')
            result = subprocess.run([SHELL, '-NoProfile', '-NonInteractive', '-File', str(script),
                                     str(HELPER), str(root)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            return path.read_bytes(), original

    def test_failed_run_finally_restores_original_bytes(self):
        actual, original = self.exercise('''
try { try { throw 'synthetic CLI failure' } finally { Restore-RevivalProjectSettings $snapshot } }
catch { if ($_.Exception.Message -ne 'synthetic CLI failure') { throw } }
if (!(Test-Path -LiteralPath $snapshot.Backup)) { throw 'Recovery copy disappeared' }
''')
        self.assertEqual(actual, original)

    def test_resident_editor_prevents_restore_and_keeps_recovery_copy(self):
        actual, _ = self.exercise('''
$script:resident = $true
$refused = $false
try { Restore-RevivalProjectSettings $snapshot } catch { $refused = $true }
if (!$refused -or !(Test-Path -LiteralPath $snapshot.Backup)) { throw 'Expected preserved recovery copy' }
''')
        self.assertEqual(actual, b'Unity startup changed this file')

    def test_changed_recovery_copy_does_not_overwrite_project(self):
        actual, _ = self.exercise('''
[IO.File]::WriteAllText($snapshot.Backup, 'changed backup')
$refused = $false
try { Restore-RevivalProjectSettings $snapshot } catch { $refused = $true }
if (!$refused) { throw 'Changed recovery copy was accepted' }
''')
        self.assertEqual(actual, b'Unity startup changed this file')


if __name__ == '__main__':
    unittest.main()
