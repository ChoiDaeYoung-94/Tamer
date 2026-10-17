# Private recovery copy for Unity startup/build serialization side effects.
function Get-RevivalProjectEditor {
    param([Parameter(Mandatory)][string]$ProjectPath)
    $normalized = [IO.Path]::GetFullPath($ProjectPath).Replace('/', '\').TrimEnd('\')
    $editors = @(foreach ($process in (Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" -ErrorAction Stop)) {
        $executable = ([string]$process.ExecutablePath).Replace('/', '\')
        # The pinned CLI shares Unity.exe's process name, but is not an Editor.
        if ($executable -match '(?i)\\tools\\\.local\\unity-cli\\[^\\]+\\unity\.exe$') { continue }
        if ($executable -notmatch '(?i)\\Editor\\Unity\.exe$' -or !$process.CommandLine) {
            throw 'Cannot determine Unity process ownership; settings were not changed.'
        }
        if (-not ('Revival.WindowsCommandLine' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace Revival {
    public static class WindowsCommandLine {
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
        public static string[] Split(string commandLine) {
            int count;
            IntPtr memory = CommandLineToArgvW(commandLine, out count);
            if (memory == IntPtr.Zero) throw new InvalidOperationException("Cannot read Unity arguments.");
            try {
                string[] arguments = new string[count];
                for (int i = 0; i < count; i++)
                    arguments[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size));
                return arguments;
            } finally { LocalFree(memory); }
        }
    }
}
'@
        }
        # Parse Windows arguments so a path in another option or a prefix cannot match.
        $arguments = [Revival.WindowsCommandLine]::Split($process.CommandLine)
        $projectArguments = @(for ($i = 1; $i -lt $arguments.Count; $i++) {
            if ($arguments[$i] -ieq '-projectPath') { $i }
        })
        if ($projectArguments.Count -ne 1 -or $projectArguments[0] + 1 -ge $arguments.Count) {
            throw 'Cannot determine Unity project argument; settings were not changed.'
        }
        $project = $arguments[$projectArguments[0] + 1].Replace('/', '\')
        if ($project -notmatch '^(?:[A-Za-z]:\\|\\\\[^\\]+\\[^\\]+(?:\\|$))') {
            throw 'Cannot determine absolute Unity project path; settings were not changed.'
        }
        $project = [IO.Path]::GetFullPath($project).TrimEnd('\')
        if ($project.Equals($normalized, [StringComparison]::OrdinalIgnoreCase)) { $process }
    })
    $editors
}

function Save-RevivalProjectSettings {
    param([Parameter(Mandatory)][string]$ProjectPath)
    $root = (Resolve-Path -LiteralPath $ProjectPath).Path
    if (Get-RevivalProjectEditor -ProjectPath $root) { throw 'Close this project Editor before taking a settings snapshot.' }
    $path = Join-Path $root 'ProjectSettings\ProjectSettings.asset'
    $directory = Join-Path $root 'Logs\revival\settings-snapshots'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $backup = Join-Path $directory (([Guid]::NewGuid().ToString('N')) + '.asset')
    $bytes = [IO.File]::ReadAllBytes($path)
    $stream = [IO.File]::Open($backup, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    [PSCustomObject]@{
        ProjectPath = $root
        Path = $path
        Backup = $backup
        Sha256 = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
    }
}

function Restore-RevivalProjectSettings {
    param([Parameter(Mandatory)]$Snapshot)
    if (Get-RevivalProjectEditor -ProjectPath $Snapshot.ProjectPath) {
        throw "Project Editor is still running; settings were not overwritten. Private recovery copy: $($Snapshot.Backup)"
    }
    if ((Get-FileHash -LiteralPath $Snapshot.Backup -Algorithm SHA256).Hash -ne $Snapshot.Sha256) {
        throw 'Settings recovery copy changed; current project settings were not overwritten.'
    }
    [IO.File]::WriteAllBytes($Snapshot.Path, [IO.File]::ReadAllBytes($Snapshot.Backup))
    if ((Get-FileHash -LiteralPath $Snapshot.Path -Algorithm SHA256).Hash -ne $Snapshot.Sha256) {
        throw "Settings restoration verification failed. Private recovery copy: $($Snapshot.Backup)"
    }
}
