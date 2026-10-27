param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Python,
      [Parameter(Mandatory=$true)][string]$Java,[Parameter(Mandatory=$true)][string]$Javac,[Parameter(Mandatory=$true)][string]$Keytool)
$ErrorActionPreference='Stop'
$classes=Join-Path $Evidence 'classes'
$null=New-Item -ItemType Directory -Path $classes
& $Javac -encoding UTF-8 -d $classes (Join-Path (Split-Path -Parent $PSScriptRoot) 'OperatingKeyPreflight.java')
if($LASTEXITCODE -ne 0){throw 'Synthetic success worker compilation failed'}
& (Join-Path $PSScriptRoot 'Compile.ps1') -Evidence $Evidence
Add-Type -Path (Join-Path $Evidence 'SigningLauncherSynthetic.dll')
$result=[LauncherHarness]::BridgeJavaSuccess($Python,(Join-Path $PSScriptRoot 'bridge_java_success_child.py'),$Java,$Keytool,$classes,$Evidence)
$result | Set-Content -LiteralPath (Join-Path $Evidence 'bridge-java-success-results.json') -Encoding utf8NoBOM
$result
