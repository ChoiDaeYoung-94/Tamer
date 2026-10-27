param(
    [Parameter(Mandatory=$true)][string]$SyntheticFile,
    [Parameter(Mandatory=$true)][string]$LauncherSource
)
$ErrorActionPreference = 'Stop'
# Compile the existing public file helper only. No launcher/UI/Job entry exists
# in this compilation unit and no source/ACL/environment setting is changed.
$source = [IO.File]::ReadAllText($LauncherSource)
$start = $source.IndexOf('public static class LauncherFiles {')
$end = $source.IndexOf('internal sealed class LockedLauncherPin', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Public file helper extraction failed' }
$unit = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
'@ + [Environment]::NewLine + $source.Substring($start, $end - $start)
Add-Type -TypeDefinition $unit
[LauncherFiles]::Regular($SyntheticFile)
$stream = [IO.File]::Open($SyntheticFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    @{
        identity = [LauncherFiles]::Identity($stream)
        sddl = [LauncherFiles]::Acl($SyntheticFile)
        sha256 = [LauncherFiles]::Hash($stream)
    } | ConvertTo-Json -Compress
} finally { $stream.Dispose() }
