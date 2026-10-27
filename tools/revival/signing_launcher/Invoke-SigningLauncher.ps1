# Explicit root-owned entry. No default prompt or build. All values below are
# nonsecret review metadata; passwords are accepted only by the masked C# UI.
param(
    [Parameter(Mandatory=$true)][string]$Binding,
    [Parameter(Mandatory=$true)][string]$BindingSha256,
    [Parameter(Mandatory=$true)][string]$ReviewedSources,
    [Parameter(Mandatory=$true)][string]$ReviewedSourcesSha256,
    [switch]$AllowReviewedBuild
)
$ErrorActionPreference='Stop'
function Initialize-LauncherBootstrap {
    if('LauncherBootstrapFile' -as [type]){return}
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class LauncherBootstrapFile {
    [StructLayout(LayoutKind.Sequential)] struct Info {
        public uint Attributes,CreationLow,CreationHigh,AccessLow,AccessHigh,WriteLow,WriteHigh;
        public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Info value);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileW(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    public static string Identity(FileStream stream) {
        Info value;if(!GetFileInformationByHandle(stream.SafeFileHandle,out value) || value.Links!=1 || (value.Attributes&0x410)!=0)throw new IOException();
        return value.Volume+":"+value.IndexHigh+":"+value.IndexLow;
    }
    public static SafeFileHandle Directory(string path) {
        var handle=CreateFileW(path,0x80,1,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
        Info value;
        if(handle.IsInvalid || !GetFileInformationByHandle(handle,out value) || (value.Attributes&0x400)!=0 || (value.Attributes&0x10)==0){handle.Dispose();throw new IOException();}
        return handle;
    }
}
'@
}
function Lock-LauncherAncestors([string]$Path,[Collections.Generic.List[IDisposable]]$Locks) {
    if($Path -cnotmatch '\A[A-Za-z]:[\\/]' -or ![IO.Path]::IsPathFullyQualified($Path) -or
       ($Path -split '[\\/]') -contains '..'){throw 'Local regular path required'}
    $parents=[Collections.Generic.List[string]]::new()
    $cursor=[IO.Path]::GetDirectoryName($Path)
    while($cursor){$parents.Insert(0,$cursor);$cursor=[IO.Path]::GetDirectoryName($cursor)}
    foreach($parent in $parents){$Locks.Add([LauncherBootstrapFile]::Directory($parent))}
    if(([IO.File]::GetAttributes($Path) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Reparse input rejected'}
}
function Test-LauncherPrivateAcl([string]$Path) {
    $acl=Get-Acl -LiteralPath $Path
    $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18')
    if(@($acl.Access).Count -ne 2){throw 'Private owner and SYSTEM ACL required'}
    $seen=@()
    foreach($rule in $acl.Access){
        $sid=$rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if($sid -notin $allowed -or $rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
           ($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -ne [Security.AccessControl.FileSystemRights]::FullControl){throw 'Private ACL required'}
        $seen+=$sid
    }
    if(@($seen|Sort-Object -Unique).Count -ne 2){throw 'Distinct owner and SYSTEM ACL required'}
}
function Read-LauncherReviewed([string]$Path,[string]$Sha256,[Collections.Generic.List[IDisposable]]$Locks,[bool]$Private=$true) {
    Lock-LauncherAncestors $Path $Locks
    $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $Locks.Add($stream)
    $identity=[LauncherBootstrapFile]::Identity($stream)
    if($Private){Test-LauncherPrivateAcl $Path;Test-LauncherPrivateAcl ([IO.Path]::GetDirectoryName($Path))}
    if($stream.Length -lt 1 -or $stream.Length -gt 8388608 -or $Sha256 -cnotmatch '\A[0-9a-f]{64}\z'){throw 'Bounded exact metadata required'}
    $bytes=[byte[]]::new([int]$stream.Length)
    $stream.ReadExactly($bytes)
    if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() -cne $Sha256 -or
       [LauncherBootstrapFile]::Identity($stream) -ne $identity){throw 'Exact reviewed bytes required'}
    # Hash and JSON parser consume this one snapshot, with its handle retained.
    return ,$bytes
}
if(!$AllowReviewedBuild){throw 'Explicit reviewed build permission required; no prompt opened'}
if($PSVersionTable.PSVersion.Major -lt 7 -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){
    throw 'PowerShell 7 with -STA required'
}
# Compiler input handles stay non-writable/non-deletable until the run ends.
$locks=[Collections.Generic.List[IDisposable]]::new()
try{
    Initialize-LauncherBootstrap
    $bindingBytes=Read-LauncherReviewed $Binding $BindingSha256 $locks
    $reviewBytes=Read-LauncherReviewed $ReviewedSources $ReviewedSourcesSha256 $locks
    $review=[Text.Encoding]::UTF8.GetString($reviewBytes) | ConvertFrom-Json -AsHashtable
    $expected=@('OwnedChild.cs','SigningLauncher.cs','OperatingKeyPreflight.java','producer_bridge.py','Invoke-SigningLauncher.ps1','EditorOwnership.ps1')
    if($review.Count -ne $expected.Count){throw 'Exact reviewed source scope required'}
    foreach($name in $expected){
        if(!$review.ContainsKey($name)){throw 'Exact reviewed source required'}
        $null=Read-LauncherReviewed (Join-Path $PSScriptRoot $name) $review[$name] $locks $false
    }
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type -Path @((Join-Path $PSScriptRoot 'OwnedChild.cs'),(Join-Path $PSScriptRoot 'SigningLauncher.cs')) -ReferencedAssemblies @(
        'System.Windows.Forms','System.Windows.Forms.Primitives','System.Private.Windows.Core',
        'System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Runtime.InteropServices',
        'System.Threading','System.Threading.Thread','System.Threading.Tasks','System.Diagnostics.Process',
        'System.ComponentModel.Primitives','System.ComponentModel','System.Security.Cryptography','System.Text.RegularExpressions',
        'System.Collections','Microsoft.Win32.Primitives','System.Text.Json','System.Memory',
        'System.Security.AccessControl','System.Security.Principal.Windows','System.IO.FileSystem.AccessControl')
    [SigningLauncher]::Run($Binding,$BindingSha256,$true)
}finally{
    foreach($handle in $locks){$handle.Dispose()}
}
