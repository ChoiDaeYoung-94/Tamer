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
    def exercise_processes(self, body):
        with tempfile.TemporaryDirectory() as folder:
            script = Path(folder) / 'processes.ps1'
            script.write_text('''param($Helper)
$ErrorActionPreference = 'Stop'
. $Helper
function Get-CimInstance { param($ClassName, $Filter, $ErrorAction) $script:processes }
$project = 'C:/synthetic/Tamer Project'
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
function Set-Process($ExecutablePath, $CommandLine) {
    $script:processes = @([pscustomobject]@{
        ProcessId = 1; ExecutablePath = $ExecutablePath; CommandLine = $CommandLine
    })
}
''' + body, encoding='utf-8')
            result = subprocess.run([SHELL, '-NoProfile', '-NonInteractive', '-File', str(script),
                                     str(HELPER)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_process_identity_and_exact_project_argument(self):
        self.exercise_processes(r'''
$cases = @(
    @($editor, '"C:\Program Files\Unity\Editor\Unity.exe" -projectPath "C:\synthetic\Tamer Project"', 1),
    @($editor.ToUpper(), 'Unity.exe -PROJECTPATH "c:/SYNTHETIC/tamer project/" -batchmode', 1),
    @($editor, 'Unity.exe -projectPath "C:/synthetic/other/../Tamer Project"', 1),
    @($editor, 'Unity.exe -projectPath "C:\synthetic\Tamer Project\\"', 1),
    @($editor, 'Unity.exe -projectPath "C:/synthetic/Tamer Project-copy"', 0),
    @($editor, 'Unity.exe -projectPath "C:/synthetic/Tamer Project/child"', 0),
    @($editor, 'Unity.exe -projectPath C:/synthetic/Other -logFile "C:/synthetic/Tamer Project"', 0),
    @($editor, 'Unity.exe -logFile "text -projectPath C:/synthetic/Tamer Project" -projectPath C:/synthetic/Other', 0),
    @($editor, 'Unity.exe -logFile "text \" -projectPath C:/synthetic/Tamer Project" -projectPath C:/synthetic/Other', 0),
    @('C:/synthetic/Tamer Project/tools/.local/unity-cli/1.0.0-beta.8/unity.exe', 'unity.exe command -projectPath "C:/synthetic/Tamer Project"', 0),
    @('C:/synthetic/Other/tools/.local/unity-cli/1.0.0-beta.8/UNITY.EXE', 'unity.exe command --project-path "C:/synthetic/Tamer Project"', 0)
)
foreach ($case in $cases) {
    Set-Process $case[0] $case[1]
    $found = @(Get-RevivalProjectEditor -ProjectPath $project)
    if ($found.Count -ne $case[2]) { throw 'Unexpected project process selection' }
    if ($found.Count -and $found[0].ProcessId -ne 1) { throw 'Original process was not returned' }
}
$script:processes = @()
if (@(Get-RevivalProjectEditor -ProjectPath $project).Count) { throw 'Unexpected empty process result' }
''')

    def test_unknown_process_information_refuses_settings_changes(self):
        self.exercise_processes(r'''
$cases = @(
    @($null, 'Unity.exe -projectPath "C:/synthetic/Tamer Project"'),
    @('C:/unknown/Unity.exe', 'Unity.exe -projectPath "C:/synthetic/Tamer Project"'),
    @($editor, $null),
    @($editor, 'Unity.exe -projectPathExtra "C:/synthetic/Tamer Project"'),
    @($editor, 'Unity.exe -projectPath'),
    @($editor, 'Unity.exe -projectPath relative/path'),
    @($editor, 'Unity.exe -projectPath C:/synthetic/Other -projectPath "C:/synthetic/Tamer Project"')
)
foreach ($case in $cases) {
    Set-Process $case[0] $case[1]
    $refused = $false
    try { Get-RevivalProjectEditor -ProjectPath $project | Out-Null } catch { $refused = $true }
    if (!$refused) { throw 'Unknown ownership was accepted' }
}
function Get-CimInstance { throw 'Synthetic process query failure' }
$refused = $false
try { Get-RevivalProjectEditor -ProjectPath $project | Out-Null } catch { $refused = $true }
if (!$refused) { throw 'Process query failure was accepted' }
''')

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
