param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference='Stop'
$source=Join-Path (Split-Path -Parent $PSScriptRoot) 'Invoke-SigningLauncher.ps1'
$tokens=$null;$errorsFound=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errorsFound)
if($errorsFound.Count){throw 'Bootstrap parser failed'}
# Load only the production function definitions. No main launcher/Prompt/build.
foreach($definition in $ast.FindAll({param($item) $item -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){
    . ([ScriptBlock]::Create($definition.Extent.Text))
}
Initialize-LauncherBootstrap
$private=Join-Path $Evidence 'private'
$null=New-Item -ItemType Directory -Path $private
$acl=[Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true,$false)
$owner=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.SetOwner($owner)
foreach($sid in @($owner,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){
    $rule=[Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $private -AclObject $acl
$reviewedSource=Join-Path $private 'reviewed.cs'
[IO.File]::WriteAllText($reviewedSource,'public static class ReviewedBootstrapFixture { public static int Value=7; }')
$manifest=Join-Path $private 'review.private.json'
$manifestText=@{'reviewed.cs'=(Get-FileHash -LiteralPath $reviewedSource -Algorithm SHA256).Hash.ToLowerInvariant()} | ConvertTo-Json -Compress
[IO.File]::WriteAllText($manifest,$manifestText)
$expected=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
$locks=[Collections.Generic.List[IDisposable]]::new()
try{
    $bytes=Read-LauncherReviewed $manifest $expected $locks
    $manifestBlocked=$false
    try{[IO.File]::WriteAllText($manifest,'{"reviewed.cs":"unreviewed"}')}catch [IO.IOException]{$manifestBlocked=$true}
    $parsed=[Text.Encoding]::UTF8.GetString($bytes)|ConvertFrom-Json -AsHashtable
    $null=Read-LauncherReviewed $reviewedSource $parsed['reviewed.cs'] $locks $false
    $sourceBlocked=$false
    try{[IO.File]::WriteAllText($reviewedSource,'public static class ReviewedBootstrapFixture { public static int Value=99; }')}catch [IO.IOException]{$sourceBlocked=$true}
    Add-Type -Path $reviewedSource
    if(!$manifestBlocked -or !$sourceBlocked -or [ReviewedBootstrapFixture]::Value -ne 7){throw 'Bootstrap snapshot boundary failed'}
    $result=@{passed=$true;manifestWriteBlocked=$manifestBlocked;sourceWriteBlocked=$sourceBlocked;exactReviewedSourceCompiled=$true;promptCalls=0;realBindingCalls=0}
    $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $Evidence 'bootstrap-results.json') -Encoding utf8NoBOM
    $result|ConvertTo-Json -Compress
}finally{foreach($handle in $locks){$handle.Dispose()}}
