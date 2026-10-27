param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath $Evidence -PathType Container)){throw 'Existing private synthetic evidence directory required'}
$source=Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$paths=@((Join-Path $source 'OwnedChild.cs'),(Join-Path $source 'SigningLauncher.cs'))
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'LauncherHarness.cs')){$paths+=Join-Path $PSScriptRoot 'LauncherHarness.cs'}
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'AtomicOwnerFixture.cs')){$paths+=Join-Path $PSScriptRoot 'AtomicOwnerFixture.cs'}
$assembly=Join-Path $Evidence 'SigningLauncherSynthetic.dll'
Add-Type -Path $paths -OutputAssembly $assembly -ReferencedAssemblies @(
    'System.Windows.Forms','System.Windows.Forms.Primitives','System.Private.Windows.Core',
    'System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Runtime.InteropServices',
    'System.Threading','System.Threading.Thread','System.Threading.Tasks','System.Diagnostics.Process',
    'System.ComponentModel.Primitives','System.ComponentModel','System.Security.Cryptography','System.Text.RegularExpressions',
    'System.Collections','Microsoft.Win32.Primitives','System.Text.Json','System.Memory',
    'System.Security.AccessControl','System.Security.Principal.Windows','System.IO.FileSystem.AccessControl')
foreach($file in @((Join-Path $source 'Invoke-SigningLauncher.ps1'),$PSCommandPath)){
    $tokens=$null;$problems=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($file,[ref]$tokens,[ref]$problems)
    if($problems.Count){throw 'PowerShell parse failed'}
}
'C# compiled and PowerShell parsed; no UI, real binding, Editor, key or build invoked.'
