param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe',
    [ValidateNotNullOrEmpty()]
    [string]$PythonPath = 'python',
    [ValidateNotNullOrEmpty()]
    [string]$AssetSource,
    [ValidateNotNullOrEmpty()]
    [string]$PrivateMetaApproval
)
$ErrorActionPreference = 'Stop'
if ($PSBoundParameters.ContainsKey('PrivateMetaApproval') -and
    !$PSBoundParameters.ContainsKey('AssetSource')) {
    throw 'PrivateMetaApproval requires AssetSource; the existing checkout/source approval must be validated.'
}
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
Push-Location $ProjectPath
try {
    & $PythonPath -m unittest discover -s tools/revival -p 'test_*.py'
    if ($LASTEXITCODE -ne 0) { throw 'Revival Python tests failed.' }
    $baselineArgs = @{ ProjectPath = $ProjectPath; EditorPath = $EditorPath; PythonPath = $PythonPath }
    if ($PSBoundParameters.ContainsKey('AssetSource')) { $baselineArgs.AssetSource = $AssetSource }
    if ($PSBoundParameters.ContainsKey('PrivateMetaApproval')) { $baselineArgs.PrivateMetaApproval = $PrivateMetaApproval }
    & "$PSScriptRoot\Run-Baseline.ps1" @baselineArgs
    & $PythonPath tools/revival/audit_guids.py
    if ($LASTEXITCODE -ne 0) { throw 'GUID verification failed.' }
    & $PythonPath tools/revival/verify_native_alignment.py
    if ($LASTEXITCODE -ne 0) { throw 'APK native alignment verification failed.' }
} finally { Pop-Location }
