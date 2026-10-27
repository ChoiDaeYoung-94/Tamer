param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Python,
      [Parameter(Mandatory=$true)][string]$Java,[Parameter(Mandatory=$true)][string]$Javac)
$ErrorActionPreference='Stop'
$classes=Join-Path $Evidence 'classes'
$null=New-Item -ItemType Directory -Path $classes
& $Javac -encoding UTF-8 -d $classes (Join-Path (Split-Path -Parent $PSScriptRoot) 'OperatingKeyPreflight.java')
if($LASTEXITCODE -ne 0){throw 'Synthetic integration worker compilation failed'}
& (Join-Path $PSScriptRoot 'Compile.ps1') -Evidence $Evidence
Add-Type -Path (Join-Path $Evidence 'SigningLauncherSynthetic.dll')
$result=[LauncherHarness]::BridgeJava($Python,(Join-Path $PSScriptRoot 'bridge_java_child.py'),$Java,$classes,$Evidence)
$result | Set-Content -LiteralPath (Join-Path $Evidence 'bridge-java-results.json') -Encoding utf8NoBOM
$result
