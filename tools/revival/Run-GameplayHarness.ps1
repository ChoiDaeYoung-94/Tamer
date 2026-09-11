param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$gameplayHarnessEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
}
if ($gameplayHarnessEditors) { throw 'Close this checkout Editor before building the ad harness.' }
# Capture before Editor startup: Unity may clear the serialized signing alias on import.
$gameplayHarnessSnapshots = @{}
$gameplayHarnessBackup = Join-Path $ProjectPath ('.revival-local/gameplay-harness-snapshots/' + [guid]::NewGuid().ToString('N'))
foreach ($gameplayHarnessRelative in @(
    'ProjectSettings/ProjectSettings.asset',
    'ProjectSettings/SceneTemplateSettings.json',
    'ProjectSettings/AndroidResolverDependencies.xml',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Assets/Plugins/Android/AndroidManifest.xml',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml'
)) {
    $gameplayHarnessPath = Join-Path $ProjectPath $gameplayHarnessRelative
    $gameplayHarnessSnapshots[$gameplayHarnessPath] = [IO.File]::ReadAllBytes($gameplayHarnessPath)
    $gameplayHarnessBackupFile = Join-Path $gameplayHarnessBackup $gameplayHarnessRelative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $gameplayHarnessBackupFile) | Out-Null
    [IO.File]::WriteAllBytes($gameplayHarnessBackupFile, $gameplayHarnessSnapshots[$gameplayHarnessPath])
}
Push-Location $ProjectPath
try {
    $gameplayHarnessMethod = 'RevivalGameplayBuild.BuildAndroid'
    $gameplayHarnessCli = Join-Path $ProjectPath 'tools/.local/unity-cli/1.0.0-beta.8/unity.exe'
    $gameplayHarnessLog = Join-Path $ProjectPath "Logs/revival/gameplay-build.log"
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $gameplayHarnessLog) | Out-Null
    & $gameplayHarnessCli build $ProjectPath --editor-version 6000.0.81f1 --target Android --execute-method $gameplayHarnessMethod --log-file $gameplayHarnessLog --no-tail --non-interactive
    if ($LASTEXITCODE -ne 0) { throw "Harness build failed (exit $LASTEXITCODE)." }
    python tools/revival/verify_gameplay_harness.py
    if ($LASTEXITCODE -ne 0) { throw 'Harness APK identity/signature verification failed.' }
}
finally {
    try {
        $gameplayHarnessRemainingEditors = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
            $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
        }
        if ($gameplayHarnessRemainingEditors) {
            throw "Editor remains active; settings were not overwritten. Original snapshots retained at $gameplayHarnessBackup"
        }
        foreach ($gameplayHarnessPath in $gameplayHarnessSnapshots.Keys) {
            [IO.File]::WriteAllBytes($gameplayHarnessPath, $gameplayHarnessSnapshots[$gameplayHarnessPath])
        }
    }
    finally { Pop-Location }
}
