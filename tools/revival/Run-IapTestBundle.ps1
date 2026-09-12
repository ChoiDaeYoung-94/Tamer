param(
    [string]$TestTitle,
    [string]$ProductionTitle,
    [string]$Catalog = 'iap-test-v1',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path "$PSScriptRoot/../..").Path
. "$PSScriptRoot/ProjectSettingsSnapshot.ps1"
$private = Join-Path $project '.revival-local/iap-signing'
$jdk = 'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin'
$key = Join-Path $private 'test-upload.jks'
$passwordFile = Join-Path $private 'password.dpapi'
$certificate = Join-Path $private 'test-upload.der'
$marker = Join-Path $private 'test-only.json'
if (Test-Path $private) {
    if ((Get-Item -LiteralPath $private).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Signing directory must not be a link.' }
} else { New-Item -ItemType Directory -Path $private | Out-Null }
$acl = Get-Acl -LiteralPath $private
$acl.SetAccessRuleProtection($true, $false)
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.SetAccessRule($rule)
Set-Acl -LiteralPath $private -AclObject $acl
foreach ($path in @($key, $passwordFile, $certificate, $marker)) {
    if ((Test-Path $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Signing files must not be links.' }
}
$ready = (Test-Path $key) -and (Test-Path $passwordFile) -and (Test-Path $certificate) -and (Test-Path $marker)
if (!$ready -and ((Test-Path $key) -or (Test-Path $passwordFile) -or (Test-Path $certificate) -or (Test-Path $marker))) {
    throw 'Partial signing setup exists; preserve it for inspection.'
}
$pointer = [IntPtr]::Zero
$snapshot = $null
try {
    if (!$ready) {
        $random = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($random) } finally { $rng.Dispose() }
        $secret = [Convert]::ToBase64String($random)
        [Array]::Clear($random, 0, $random.Length)
        ConvertTo-SecureString $secret -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -LiteralPath $passwordFile
    } else {
        $metadata = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if ($metadata.applicationId -ne 'com.AeDeong.MonsterTamer.iaptest' -or $metadata.alias -ne 'tamer-iap-test-upload') { throw 'Not the dedicated IAP test key.' }
        if ((Get-FileHash $key -Algorithm SHA256).Hash -ne $metadata.keySha256) { throw 'Test key changed; preserve and inspect.' }
        $secure = Get-Content -LiteralPath $passwordFile -Raw | ConvertTo-SecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        $secret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    $env:TAMER_IAP_TEST_KEY_PASSWORD = $secret
    if (!$ready) {
        & "$jdk/keytool.exe" -genkeypair -keystore $key -storetype JKS -storepass:env TAMER_IAP_TEST_KEY_PASSWORD -keypass:env TAMER_IAP_TEST_KEY_PASSWORD -alias tamer-iap-test-upload -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Tamer IAP Test Upload, OU=Isolated Testing, O=Tamer Test, C=KR' *> (Join-Path $private 'keytool.log')
        if ($LASTEXITCODE -ne 0) { throw 'Test key generation failed; inspect private log.' }
        & "$jdk/keytool.exe" -exportcert -keystore $key -storepass:env TAMER_IAP_TEST_KEY_PASSWORD -alias tamer-iap-test-upload -file $certificate *> (Join-Path $private 'certificate.log')
        if ($LASTEXITCODE -ne 0) { throw 'Test certificate export failed.' }
        @{ applicationId='com.AeDeong.MonsterTamer.iaptest'; alias='tamer-iap-test-upload'; keySha256=(Get-FileHash $key -Algorithm SHA256).Hash; certificateSha256=(Get-FileHash $certificate -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content -LiteralPath $marker
    }
    if (!$PrepareOnly) {
        if ($TestTitle -notmatch '^[a-fA-F0-9]{3,32}$' -or $ProductionTitle -notmatch '^[a-fA-F0-9]{3,32}$' -or $TestTitle -eq $ProductionTitle -or [string]::IsNullOrWhiteSpace($Catalog)) { throw 'Explicit separate test title and catalog required.' }
        $snapshot = Save-RevivalProjectSettings -ProjectPath $project
        $env:TAMER_IAP_TEST_TITLE = $TestTitle
        $env:TAMER_IAP_PRODUCTION_TITLE = $ProductionTitle
        $env:TAMER_IAP_TEST_CATALOG = $Catalog
        & "$project/tools/.local/unity-cli/1.0.0-beta.8/unity.exe" build $project --editor-path 'C:/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Unity.exe' --target Android --execute-method RevivalIapBuild.BuildStoreTestBundle --log-file "$project/Logs/revival/iap-bundle-build.log" --no-tail --non-interactive
        if ($LASTEXITCODE -ne 0) { throw 'IAP test bundle build failed.' }
    }
    Write-Output 'Dedicated IAP test signing preparation succeeded.'
} finally {
    try {
        if ($null -ne $snapshot) { Restore-RevivalProjectSettings -Snapshot $snapshot }
    } finally {
    Remove-Item Env:TAMER_IAP_TEST_KEY_PASSWORD, Env:TAMER_IAP_TEST_TITLE, Env:TAMER_IAP_PRODUCTION_TITLE, Env:TAMER_IAP_TEST_CATALOG -ErrorAction SilentlyContinue
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $secret = $null
    }
}
