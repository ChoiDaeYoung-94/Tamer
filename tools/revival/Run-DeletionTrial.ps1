param([string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
function Get-ReceiptEditors {
    Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/', '\').Contains($ProjectPath)
    }
}
if (Get-ReceiptEditors) { throw 'Close this checkout Editor before building the receipt harness.' }
. "$PSScriptRoot/ProjectSettingsSnapshot.ps1"
$receiptSettings = Save-RevivalProjectSettings -ProjectPath $ProjectPath
$receiptAssets = @{}
foreach ($relative in @('Assets/Plugins/Android/AndroidManifest.xml',
    'Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset',
    'Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml',
    'Assets/Settings/Settings/UniversalRP-HighQuality.asset', 'Assets/Settings/Settings/UniversalRP-LowQuality.asset',
    'Assets/Settings/Settings/UniversalRP-MediumQuality.asset', 'Assets/Tests/Scenes/RevivalSmoke.unity',
    'Assets/UniversalRenderPipelineGlobalSettings.asset', 'ProjectSettings/AndroidResolverDependencies.xml',
    'ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/SceneTemplateSettings.json')) {
    $path = Join-Path $ProjectPath $relative
    $receiptAssets[$path] = [IO.File]::ReadAllBytes($path)
}
$receiptEvidence = Join-Path $ProjectPath 'Logs/revival'
New-Item -ItemType Directory -Force -Path $receiptEvidence | Out-Null
$receiptSettings | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $receiptEvidence 'deletion-trial-build-settings.json')
Push-Location $ProjectPath
try {
    & tools/.local/unity-cli/1.0.0-beta.8/unity.exe build $ProjectPath --editor-version 6000.0.81f1 --target Android `
        --execute-method RevivalDeletionTrialBuild.BuildAndroid --log-file (Join-Path $receiptEvidence 'deletion-trial-build.log') --no-tail --non-interactive
    if ($LASTEXITCODE -ne 0) { throw "Deletion trial harness build failed: $LASTEXITCODE" }
    python tools/revival/verify_deletion_trial_apk.py
    if ($LASTEXITCODE -ne 0) { throw 'Deletion trial APK verification failed.' }
}
finally {
    try {
        if (Get-ReceiptEditors) { throw 'Editor remains active; preserve snapshots and wait before restoration.' }
        Restore-RevivalProjectSettings -Snapshot $receiptSettings
        foreach ($path in $receiptAssets.Keys) { [IO.File]::WriteAllBytes($path, $receiptAssets[$path]) }
    }
    finally { Pop-Location }
}
