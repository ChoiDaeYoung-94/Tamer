param(
    [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe',
    [switch]$TestsOnly
)
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if (!(Test-Path -LiteralPath $EditorPath)) { throw "Editor missing: $EditorPath" }
# Do not compete with a resident editor. Never terminate another project's editor.
$running = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and ($_.CommandLine.Replace('/', '\').Contains($ProjectPath))
}
if ($running) { throw 'Close this project Editor before batch validation.' }
Push-Location $ProjectPath
try {
    python tools/revival/restore_assets.py --verify
    if ($LASTEXITCODE -ne 0) { throw 'Asset verification failed.' }
    $logDir = Join-Path $ProjectPath 'Logs\revival'
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    $cli = Join-Path $ProjectPath 'tools\.local\unity-cli\1.0.0-beta.8\unity.exe'
    if (!(Test-Path -LiteralPath $cli)) { throw 'Run python tools/revival/install_cli.py first.' }
    & $cli test $ProjectPath --editor-version 6000.0.81f1 --mode EditMode --filter Revival --timeout 1200 --output "$logDir\editmode.xml" -- -buildTarget Android -logFile "$logDir\editmode.log"
    if ($LASTEXITCODE -ne 0) { throw "Unity tests failed or produced no verdict (exit $LASTEXITCODE)." }
    if (!$TestsOnly) {
        & $cli build $ProjectPath --editor-path $EditorPath --target Android --execute-method RevivalBuild.BuildAndroidDevelopment --log-file "$logDir\android-build.log" --no-tail --non-interactive
        if ($LASTEXITCODE -ne 0) { throw "Android build failed (exit $LASTEXITCODE)." }
        if (!(Test-Path -LiteralPath 'Build/revival/Tamer-development.apk')) { throw 'Build returned no APK.' }
        $androidPlayer = Join-Path (Split-Path -Parent $EditorPath) 'Data\PlaybackEngines\AndroidPlayer'
        python tools/revival/verify_apk.py --android-player $androidPlayer
        if ($LASTEXITCODE -ne 0) { throw 'APK metadata or signature verification failed.' }
    }
} finally { Pop-Location }
