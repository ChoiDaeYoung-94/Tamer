param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'EditorOwnership.ps1')
$rows=@()
function Record-Case([string]$name,[object[]]$records,[bool]$reject){
    $failed=$false
    try{$null=Get-LauncherEditorState -Processes $records -Wanted 'C:/synthetic/owner'}catch{$failed=$true}
    if($failed -ne $reject){throw 'Synthetic Editor ownership expectation failed'}
    return @{case=$name;passed=$true;rejected=$failed}
}
$rows+=Record-Case 'known-empty-process-list-allowed' @() $false
$rows+=Record-Case 'null-process-list-blocked' $null $true
$rows+=Record-Case 'beta8-and-beta14-known-CLI' @(
    @{description='unity';version='1.0.0-beta.8';command=''},@{description='unity';version='1.0.0-beta.14';command=''}) $false
$rows+=Record-Case 'known-CLI-Windows-PE-version' @(@{description='unity';version='1.0.0';command=''}) $false
$rows+=Record-Case 'stable-version-alone-cannot-exclude-unknown' @(@{description='other';version='1.0.0';command=''}) $true
$rows+=Record-Case 'unrecognized-CLI-version-blocked' @(@{description='unity';version='1.0.1';command=''}) $true
$rows+=Record-Case 'incomplete-CLI-beta-version-blocked' @(@{description='unity';version='1.0.0-beta';command=''}) $true
$rows+=Record-Case 'version-alone-cannot-exclude-unknown' @(@{description='other';version='1.0.0-beta.14';command=''}) $true
$rows+=Record-Case 'quoted-project-flag-owner-blocked' @(@{description='Unity Editor';version='6000.3.25f1';command='"Unity.exe" "-projectPath" "C:/synthetic/owner"'}) $true
$rows+=Record-Case 'quoted-other-project-preserved' @(@{description='Unity Editor';version='6000.3.25f1';command='"Unity.exe" "-projectPath" "C:/synthetic/other project"'}) $false
$rows+=Record-Case 'unknown-missing-command-blocked' @(@{description='Unity Editor';version='6000.3.25f1';command=''}) $true
$rows+=Record-Case 'duplicate-project-argument-blocked' @(@{description='Unity Editor';version='6000.3.25f1';command='Unity.exe -projectPath C:/synthetic/other -projectPath C:/synthetic/owner'}) $true
$rows+=Record-Case 'unknown-editor-version-blocked' @(@{description='Unity Editor';version='unknown';command='Unity.exe -projectPath C:/synthetic/other'}) $true
$rows | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $Evidence 'editor-ownership-results.json') -Encoding utf8NoBOM
$rows | Format-Table -AutoSize
'Synthetic metadata only; CIM/real Editor/process inspection calls: 0.'
