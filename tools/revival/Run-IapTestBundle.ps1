param(
    [string]$TestTitle,
    [string]$ProductionTitle,
    [string]$Catalog = 'iap-test-v1',
    [switch]$PrepareOnly,
    [switch]$CreateSigningForNewTestApp,
    [string]$SigningDirectory,
    [string]$TestVersionCode
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path "$PSScriptRoot/../..").Path
. "$PSScriptRoot/ProjectSettingsSnapshot.ps1"
function Assert-IapTestVersionCode {
    param([bool]$Specified, [string]$Value, [bool]$PrepareOnly)
    if (!$Specified) { return }
    $parsed = 0
    if ($PrepareOnly -or $Value -notmatch '\A[0-9]+\z' -or
        ![int]::TryParse($Value, [ref]$parsed) -or $parsed -le 26 -or $parsed -gt 2100000000) {
        throw 'Explicit test version code requires a bundle build and a value above baseline 26 within Android limits.'
    }
}
$versionCodeSpecified = $PSBoundParameters.ContainsKey('TestVersionCode')
Assert-IapTestVersionCode -Specified $versionCodeSpecified -Value $TestVersionCode -PrepareOnly ([bool]$PrepareOnly)
function Assert-IapExistingSigningDirectory {
    param([Parameter(Mandatory)][string]$Path)
    if (![IO.Path]::IsPathFullyQualified($Path)) { throw 'Signing directory must be an absolute existing path.' }
    $directory = [IO.Path]::GetFullPath($Path)
    $cursor = $directory
    while ($cursor) {
        if (!(Test-Path -LiteralPath $cursor -PathType Container) -or
            ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Signing directory and ancestors must be existing regular directories.'
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    foreach ($name in @('test-upload.jks', 'password.dpapi', 'test-upload.der', 'test-only.json')) {
        $path = Join-Path $directory $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
            ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'The four existing regular IAP signing files are required.'
        }
    }
    return $directory
}
$externalSigning = $PSBoundParameters.ContainsKey('SigningDirectory')
if ($externalSigning -and $CreateSigningForNewTestApp) { throw 'External signing selection cannot create a new key.' }
$private = if ($externalSigning) { Assert-IapExistingSigningDirectory -Path $SigningDirectory } else { Join-Path $project '.revival-local/iap-signing' }
$jdk = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin'
$key = Join-Path $private 'test-upload.jks'
$passwordFile = Join-Path $private 'password.dpapi'
$certificate = Join-Path $private 'test-upload.der'
$marker = Join-Path $private 'test-only.json'
# Decide before creating directories, changing ACLs, decrypting passwords, or launching tools.
function Assert-IapSigningPreparation {
    param([int]$ExistingFiles, [bool]$PrepareOnly, [bool]$CreateSigningForNewTestApp)
    if ($ExistingFiles -gt 0 -and $ExistingFiles -lt 4) {
        throw 'Incomplete IAP signing setup; preserve existing files and recover the original setup.'
    }
    if ($CreateSigningForNewTestApp -and (!$PrepareOnly -or $ExistingFiles -ne 0)) {
        throw 'New signing creation requires PrepareOnly and an empty setup for a newly approved test app.'
    }
    if ($ExistingFiles -eq 0 -and !$CreateSigningForNewTestApp) {
        throw 'Existing IAP signing files are missing. Recover the original four files; no replacement key will be generated.'
    }
}
$existingSigningFiles = @(@($key, $passwordFile, $certificate, $marker) | Where-Object { Test-Path -LiteralPath $_ }).Count
Assert-IapSigningPreparation -ExistingFiles $existingSigningFiles -PrepareOnly ([bool]$PrepareOnly) -CreateSigningForNewTestApp ([bool]$CreateSigningForNewTestApp)
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
$acl.SetAccessRule($rule)
if (!$externalSigning) {
    if (Test-Path -LiteralPath $private) {
        if ((Get-Item -LiteralPath $private).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Signing directory must not be a link.' }
    } else { New-Item -ItemType Directory -Path $private | Out-Null }
    [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($private), $acl)
    foreach ($path in @($key, $passwordFile, $certificate, $marker, (Join-Path $private "keytool.log"), (Join-Path $private "certificate.log"))) {
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Signing files must not be links.' }
        if (Test-Path -LiteralPath $path) {
            $fileAcl = [Security.AccessControl.FileSecurity]::new()
            $fileAcl.SetAccessRuleProtection($true, $false)
            $fileAcl.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'Allow'))
            [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($path), $fileAcl)
        }
    }
}
$ready = (Test-Path -LiteralPath $key) -and (Test-Path -LiteralPath $passwordFile) -and (Test-Path -LiteralPath $certificate) -and (Test-Path -LiteralPath $marker)
if ($externalSigning -and !$ready) { throw 'External signing files became unavailable; preserve and inspect.' }
if (!$ready -and ((Test-Path $key) -or (Test-Path $passwordFile) -or (Test-Path $certificate) -or (Test-Path $marker))) {
    throw 'Partial signing setup exists; preserve it for inspection.'
}
$pointer = [IntPtr]::Zero
$snapshot = $null
$resourceSnapshot = @()
$previousKeyPath = [Environment]::GetEnvironmentVariable('TAMER_IAP_TEST_KEY_PATH', 'Process')
$previousVersionCode = [Environment]::GetEnvironmentVariable('TAMER_IAP_TEST_VERSION_CODE', 'Process')
try {
    # Do not inherit an unrelated process override when the option is omitted.
    if ($versionCodeSpecified) { $env:TAMER_IAP_TEST_VERSION_CODE = $TestVersionCode }
    else { Remove-Item Env:TAMER_IAP_TEST_VERSION_CODE -ErrorAction SilentlyContinue }
    if (!$ready) {
        $random = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($random) } finally { $rng.Dispose() }
        $secret = [Convert]::ToBase64String($random)
        [Array]::Clear($random, 0, $random.Length)
        ConvertTo-SecureString $secret -AsPlainText -Force | ConvertFrom-SecureString | Set-Content -LiteralPath $passwordFile
    } else {
        $metadata = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if ($externalSigning -and ($metadata.keySha256 -notmatch '^[a-fA-F0-9]{64}$' -or $metadata.certificateSha256 -notmatch '^[a-fA-F0-9]{64}$')) { throw 'External signing marker requires key and certificate SHA-256 fields.' }
        if ($metadata.applicationId -ne 'com.AeDeong.MonsterTamer.iaptest' -or $metadata.alias -ne 'tamer-iap-test-upload') { throw 'Not the dedicated IAP test key.' }
        if ((Get-FileHash -LiteralPath $key -Algorithm SHA256).Hash -ne $metadata.keySha256) { throw 'Test key changed; preserve and inspect.' }
        if ((Get-FileHash -LiteralPath $certificate -Algorithm SHA256).Hash -ne $metadata.certificateSha256) { throw 'Test certificate changed; preserve and inspect.' }
        $secure = (Get-Content -LiteralPath $passwordFile -Raw).Trim() | ConvertTo-SecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        $secret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    $env:TAMER_IAP_TEST_KEY_PASSWORD = $secret
    $env:TAMER_IAP_TEST_KEY_PATH = $key
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
        # Capture before Editor startup/import as well as the C# build's own finally.
        # This private backup may contain restored configuration; never print it.
        $resourcePaths = @(
            'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset',
            'Assets/ThirdParty/PlayFabSDK/Shared/Public/Resources/PlayFabSharedSettings.asset.meta'
        )
        foreach ($relative in $resourcePaths) {
            $path = Join-Path $project $relative
            if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
                ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw 'Existing regular PlayFab resource and metadata required; restore approved assets first.'
            }
        }
        $resourceBackup = Join-Path $project ('Logs/revival/iap-resource-snapshots/' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($resourceBackup) | Out-Null
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($resourceBackup), $acl)
        foreach ($relative in $resourcePaths) {
            $path = Join-Path $project $relative
            $backup = Join-Path $resourceBackup ([IO.Path]::GetFileName($path) + '.backup')
            $bytes = [IO.File]::ReadAllBytes($path)
            $stream = [IO.File]::Open($backup, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
            try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
            $resourceSnapshot += [pscustomobject]@{ Path=$path; Backup=$backup; Sha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash }
        }
        $env:TAMER_IAP_TEST_TITLE = $TestTitle
        $env:TAMER_IAP_PRODUCTION_TITLE = $ProductionTitle
        $env:TAMER_IAP_TEST_CATALOG = $Catalog
        & "$project/tools/.local/unity-cli/1.0.0-beta.8/unity.exe" build $project --editor-path 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe' --target Android --execute-method RevivalIapBuild.BuildStoreTestBundle --log-file "$project/Logs/revival/iap-bundle-build.log" --no-tail --non-interactive
        if ($LASTEXITCODE -ne 0) { throw 'IAP test bundle build failed.' }
    }
    Write-Output 'Dedicated IAP test signing preparation succeeded.'
} finally {
    try {
        if ($null -ne $snapshot) { Restore-RevivalProjectSettings -Snapshot $snapshot }
    } finally {
    try {
        if ($resourceSnapshot.Count -gt 0) {
            $editorRunning = [bool](Get-RevivalProjectEditor -ProjectPath $project)
            $resourceChecks = @()
            foreach ($entry in $resourceSnapshot) {
                $exists = Test-Path -LiteralPath $entry.Path -PathType Leaf
                $unsafePath = $false
                $cursor = [IO.Path]::GetDirectoryName($entry.Path)
                while ($cursor) {
                    if (!(Test-Path -LiteralPath $cursor -PathType Container) -or
                        ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                        $unsafePath = $true
                        break
                    }
                    $cursor = [IO.Path]::GetDirectoryName($cursor)
                }
                $item = if ($exists -and !$unsafePath) { Get-Item -LiteralPath $entry.Path } else { $null }
                $link = $null -ne $item -and [bool]($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
                $afterHash = if ($null -ne $item -and !$link) { (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash } else { $null }
                $backupMatches = (Get-FileHash -LiteralPath $entry.Backup -Algorithm SHA256).Hash -eq $entry.Sha256
                $resourceChecks += [pscustomobject]@{
                    Path=$entry.Path; Exists=$exists; UnsafePath=$unsafePath; Link=$link
                    AfterSha256=$afterHash; OriginalSha256=$entry.Sha256; BackupMatches=$backupMatches
                    Bytes=$(if ($null -ne $item) { $item.Length } else { $null })
                    CreationTimeUtc=$(if ($null -ne $item) { $item.CreationTimeUtc.ToString('o') } else { $null })
                    MatchesOriginal=($exists -and !$unsafePath -and !$link -and $backupMatches -and $afterHash -eq $entry.Sha256)
                }
            }
            $manualRecoveryRequired = $editorRunning -or [bool]($resourceChecks | Where-Object { !$_.MatchesOriginal })
            @{ EditorRunning=$editorRunning; ManualRecoveryRequired=$manualRecoveryRequired; Files=$resourceChecks } |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resourceBackup 'after-state.private.json') -Encoding utf8
            # The C# scope restores files. Never recreate or overwrite a changed
            # file here; retain the private originals for separately reviewed recovery.
            if ($manualRecoveryRequired) {
                throw 'PlayFab resource restoration is unverified; private evidence and backups preserved for reviewed recovery without overwrite.'
            }
        }
    } finally {
    [Environment]::SetEnvironmentVariable('TAMER_IAP_TEST_KEY_PATH', $previousKeyPath, 'Process')
    if ([string]::IsNullOrEmpty($previousVersionCode)) { Remove-Item Env:TAMER_IAP_TEST_VERSION_CODE -ErrorAction SilentlyContinue }
    else { $env:TAMER_IAP_TEST_VERSION_CODE = $previousVersionCode }
    Remove-Item Env:TAMER_IAP_TEST_KEY_PASSWORD, Env:TAMER_IAP_TEST_TITLE, Env:TAMER_IAP_PRODUCTION_TITLE, Env:TAMER_IAP_TEST_CATALOG -ErrorAction SilentlyContinue
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $secret = $null
    }
    }
}
