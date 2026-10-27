param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Python,[switch]$Delta)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Compile.ps1') -Evidence $Evidence
$assembly=Join-Path $Evidence 'SigningLauncherSynthetic.dll'
Add-Type -Path $assembly
$result=[LauncherHarness]::Run($Python,(Join-Path $PSScriptRoot 'synthetic_child.py'),(Get-Process -Id $PID).Path,$assembly,$Evidence,[bool]$Delta)
$result | Set-Content -LiteralPath (Join-Path $Evidence 'native-results.json') -Encoding utf8NoBOM
$result | ConvertFrom-Json | Format-Table -AutoSize
'Synthetic native session/process checks passed; actual prompt, key, Editor and build calls: 0.'
