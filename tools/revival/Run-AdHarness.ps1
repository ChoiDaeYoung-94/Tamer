param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [ValidateSet('sample', 'control', 'ump-sample', 'ump-publisher')][string]$Variant = 'sample',
    [switch]$PrivatePublisherOptIn
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$adHarnessEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
}
if ($adHarnessEditors) { throw 'Close this checkout Editor before building the ad harness.' }
if (($Variant -eq 'ump-publisher') -ne $PrivatePublisherOptIn.IsPresent) {
    throw 'Publisher UMP requires -Variant ump-publisher -PrivatePublisherOptIn; other variants cannot opt in.'
}
$adPublisherOptInBefore = [Environment]::GetEnvironmentVariable('TAMER_UMP_PUBLISHER_BUILD_OPT_IN', 'Process')
# Capture before Editor startup: Unity may clear the serialized signing alias on import.
$adHarnessSnapshots = @{}
$adHarnessBackup = Join-Path $ProjectPath ('.revival-local/ad-harness-snapshots/' + [guid]::NewGuid().ToString('N'))
foreach ($adHarnessRelative in @(
    'ProjectSettings/ProjectSettings.asset',
    'ProjectSettings/SceneTemplateSettings.json',
    'ProjectSettings/AndroidResolverDependencies.xml',
    'Assets/Tests/Scenes/RevivalAdHarness.unity',
    'Assets/Tests/Scenes/RevivalAdHarness.unity.meta',
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
    $adHarnessMethod = switch ($Variant) {
        'sample' { 'RevivalAdHarnessBuild.BuildSample' }
        'control' { 'RevivalAdHarnessBuild.BuildReleaseControl' }
        'ump-sample' { 'RevivalAdHarnessBuild.BuildUmpSample' }
        'ump-publisher' { 'RevivalAdHarnessBuild.BuildUmpPublisher' }
    }
    if ($PrivatePublisherOptIn) { $env:TAMER_UMP_PUBLISHER_BUILD_OPT_IN = '1' }
    else { [Environment]::SetEnvironmentVariable('TAMER_UMP_PUBLISHER_BUILD_OPT_IN', $null, 'Process') }
    $adHarnessCli = Join-Path $ProjectPath 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
    $adHarnessLog = Join-Path $ProjectPath "Logs/revival/ads-$Variant-build.log"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $adHarnessLog) | Out-Null
    & $adHarnessCli build $ProjectPath --editor-version 6000.3.25f1 --target Android --execute-method $adHarnessMethod --log-file $adHarnessLog --no-tail --non-interactive
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed (exit $LASTEXITCODE)." }
    python tools/revival/verify_ad_harness.py --variant $Variant
    if ($LASTEXITCODE -ne 0) { throw 'Harness APK identity/signature verification failed.' }
}
finally {
    [Environment]::SetEnvironmentVariable('TAMER_UMP_PUBLISHER_BUILD_OPT_IN', $adPublisherOptInBefore, 'Process')
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
