param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Python,
      [Parameter(Mandatory=$true)][string]$Java,[Parameter(Mandatory=$true)][string]$Javac,
      [Parameter(Mandatory=$true)][string]$Keytool)
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $Evidence -PathType Container) -or @(Get-ChildItem -LiteralPath $Evidence).Count){throw 'Fresh private synthetic directory required'}
$source=Split-Path -Parent $PSScriptRoot
$null=New-Item -ItemType Directory -Path (Join-Path $Evidence 'classes'),(Join-Path $Evidence 'worker-fixture')
& (Join-Path $PSScriptRoot 'Compile.ps1') -Evidence $Evidence
Add-Type -Path (Join-Path $Evidence 'SigningLauncherSynthetic.dll')
$native=[ProfileHarness]::Run($Evidence)
$native | Set-Content -LiteralPath (Join-Path $Evidence 'profile-native-results.private.json') -Encoding utf8NoBOM
$result=[ProfileHarness]::Worker($Python,(Join-Path $PSScriptRoot 'profile_protocol_child.py'),$Java,$Javac,$Keytool,
    (Join-Path $Evidence 'classes'),(Join-Path $Evidence 'worker-fixture'))
$result | Set-Content -LiteralPath (Join-Path $Evidence 'profile-worker-results.private.json') -Encoding utf8NoBOM
$result
