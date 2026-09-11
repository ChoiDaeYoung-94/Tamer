param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
Push-Location $ProjectPath
try {
    python -m unittest discover -s tools/revival -p 'test_*.py'
    if ($LASTEXITCODE -ne 0) { throw 'Revival Python tests failed.' }
    & "$PSScriptRoot\Run-Baseline.ps1" -ProjectPath $ProjectPath -EditorPath $EditorPath
    python tools/revival/audit_guids.py
    if ($LASTEXITCODE -ne 0) { throw 'GUID verification failed.' }
    python tools/revival/verify_native_alignment.py
    if ($LASTEXITCODE -ne 0) { throw 'APK native alignment verification failed.' }
} finally { Pop-Location }
