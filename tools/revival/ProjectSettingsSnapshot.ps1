# Private recovery copy for Unity startup/build serialization side effects.
function Get-RevivalProjectEditor {
    param([Parameter(Mandatory)][string]$ProjectPath)
    $normalized = [IO.Path]::GetFullPath($ProjectPath).Replace('/', '\')
    Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/', '\').IndexOf(
            $normalized, [StringComparison]::OrdinalIgnoreCase) -ge 0
    }
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
