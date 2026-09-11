param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [ValidateSet('sample', 'control')][string]$Variant = 'sample'
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$adHarnessEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
}
if ($adHarnessEditors) { throw 'Close this checkout Editor before building the ad harness.' }
# Capture before Editor startup: Unity may clear the serialized signing alias on import.
$adHarnessSnapshots = @{}
$adHarnessBackup = Join-Path $ProjectPath ('.revival-local/ad-harness-snapshots/' + [guid]::NewGuid().ToString('N'))
foreach ($adHarnessRelative in @(
    'ProjectSettings/ProjectSettings.asset',
    'ProjectSettings/SceneTemplateSettings.json',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml'
)) {
    $adHarnessPath = Join-Path $ProjectPath $adHarnessRelative
    $adHarnessSnapshots[$adHarnessPath] = [IO.File]::ReadAllBytes($adHarnessPath)
    $adHarnessBackupFile = Join-Path $adHarnessBackup $adHarnessRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $adHarnessBackupFile) | Out-Null
    [IO.File]::WriteAllBytes($adHarnessBackupFile, $adHarnessSnapshots[$adHarnessPath])
}
Push-Location $ProjectPath
try {
    $adHarnessMethod = if ($Variant -eq 'sample') { 'RevivalAdHarnessBuild.BuildSample' } else { 'RevivalAdHarnessBuild.BuildReleaseControl' }
    $adHarnessCli = Join-Path $ProjectPath 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
    $adHarnessLog = Join-Path $ProjectPath "Logs/revival/ads-$Variant-build.log"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $adHarnessLog) | Out-Null
    & $adHarnessCli build $ProjectPath --editor-version 6000.0.81f1 --target Android --execute-method $adHarnessMethod --log-file $adHarnessLog --no-tail --non-interactive
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed (exit $LASTEXITCODE)." }
    python tools/revival/verify_ad_harness.py --variant $Variant
    if ($LASTEXITCODE -ne 0) { throw 'Harness APK identity/signature verification failed.' }
}
finally {
    try {
        $adHarnessRemainingEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
        }
        if ($adHarnessRemainingEditors) {
            throw "Editor remains active; settings were not overwritten. Original snapshots retained at $adHarnessBackup"
        }
        foreach ($adHarnessPath in $adHarnessSnapshots.Keys) {
            [IO.File]::WriteAllBytes($adHarnessPath, $adHarnessSnapshots[$adHarnessPath])
        }
    }
    finally { Pop-Location }
}
