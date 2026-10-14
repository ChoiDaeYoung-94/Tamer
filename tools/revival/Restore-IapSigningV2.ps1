# Private pipe worker for restore_iap_signing_v2.py. Never invoke from a terminal.
param(
    [ValidateSet('Decrypt', 'Protect')][string]$Mode,
    [switch]$ExecuteApproved,
    [string]$Gpg, [string]$GpgConf, [string]$Pinentry, [string]$GpgHome,
    [string]$CipherSha256
)
$ErrorActionPreference = 'Stop'
function Clear-Bytes([byte[]]$Bytes) {
    if ($null -ne $Bytes) { [Array]::Clear($Bytes, 0, $Bytes.Length) }
}
function Clear-Stream([IO.MemoryStream]$Stream) {
    if ($null -ne $Stream) {
        $segment = [ArraySegment[byte]]::new([byte[]]::new(0))
        if ($Stream.TryGetBuffer([ref]$segment)) { [Array]::Clear($segment.Array, $segment.Offset, $segment.Count) }
        $Stream.Dispose()
    }
}
function Read-PrivatePipe([int]$Limit) {
    $inputStream = [Console]::OpenStandardInput()
    $memory = [IO.MemoryStream]::new()
    $buffer = [byte[]]::new(8192)
    try {
        while (($count = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            if ($memory.Length + $count -gt $Limit) { throw 'Input limit' }
            $memory.Write($buffer, 0, $count)
        }
        return ,$memory.ToArray()
    } finally { Clear-Bytes $buffer; Clear-Stream $memory }
}
function Write-PrivatePipe([byte[]]$Bytes) {
    $stream = [Console]::OpenStandardOutput()
    $stream.Write($Bytes, 0, $Bytes.Length)
    $stream.Flush()
}
function Assert-LocalRegular([string]$Path, [bool]$Directory = $false) {
    if (![IO.Path]::IsPathFullyQualified($Path) -or $Path.StartsWith('\\') -or $Path.Replace('\', '/').Split('/') -contains '..') { throw 'Local path required' }
    $cursor = $Path
    $first = $true
    while ($cursor) {
        $item = Get-Item -LiteralPath $cursor
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            (($first -and !$Directory) -and $item.PSIsContainer) -or
            ((!$first -or $Directory) -and !$item.PSIsContainer)) { throw 'Regular path required' }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
        $first = $false
    }
}

$inputBytes = $plain = $response = $der = $pemDer = $null
$secure = $restored = $null
$originalPointer = $roundtripPointer = [IntPtr]::Zero
$agentHomeReady = $false
$success = $false
try {
    if (!$ExecuteApproved -or $PSVersionTable.PSVersion.Major -lt 7 -or
        ![Console]::IsInputRedirected -or ![Console]::IsOutputRedirected) { throw 'Reviewed private pipes required' }
    if ($Mode -notin @('Decrypt', 'Protect')) { throw 'Mode required' }
    if ($Mode -eq 'Protect') {
        $inputBytes = Read-PrivatePipe 32768
        $request = [Text.Encoding]::UTF8.GetString($inputBytes) | ConvertFrom-Json -AsHashtable
        if (@($request.Keys).Count -ne 4 -or $request.Keys -notcontains 'secret' -or
            $request.Keys -notcontains 'der' -or $request.Keys -notcontains 'pem' -or
            $request.Keys -notcontains 'certificateSha256') { throw 'Protect schema' }
        $secret = $request.secret
        if (@($secret.Keys).Count -ne 2 -or $secret.storePassword -isnot [string] -or
            $secret.storePassword.Length -lt 32 -or $secret.storePassword.Length -gt 1024 -or
            $secret.storePassword -cne $secret.aliasPassword) { throw 'Secret schema' }
        $der = [Convert]::FromBase64String($request.der)
        $match = [regex]::Match($request.pem, '\A\s*-----BEGIN CERTIFICATE-----\s*([A-Za-z0-9+/=\s]+)\s*-----END CERTIFICATE-----\s*\z')
        if (!$match.Success) { throw 'PEM schema' }
        $pemDer = [Convert]::FromBase64String(($match.Groups[1].Value -replace '\s', ''))
        if ($der.Length -ne $pemDer.Length -or ![Linq.Enumerable]::SequenceEqual[byte]($der, $pemDer)) { throw 'DER PEM mismatch' }
        $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($der)
        try {
            $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
            if ($request.certificateSha256 -notmatch '\A[0-9a-f]{64}\z' -or $actual -cne $request.certificateSha256) { throw 'Certificate binding' }
        } finally { $certificate.Dispose() }
        $secure = ConvertTo-SecureString $secret.storePassword -AsPlainText -Force
        # No -Key/-SecureKey: CurrentUser DPAPI, matching the existing IAP wrapper.
        $dpapi = ConvertFrom-SecureString $secure
        $restored = ConvertTo-SecureString $dpapi
        $originalPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        $roundtripPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($restored)
        $length = [Runtime.InteropServices.Marshal]::ReadInt32($originalPointer, -4)
        if ($length -ne [Runtime.InteropServices.Marshal]::ReadInt32($roundtripPointer, -4)) { throw 'DPAPI roundtrip' }
        for ($offset = 0; $offset -lt $length; $offset++) {
            if ([Runtime.InteropServices.Marshal]::ReadByte($originalPointer, $offset) -ne
                [Runtime.InteropServices.Marshal]::ReadByte($roundtripPointer, $offset)) { throw 'DPAPI roundtrip' }
        }
        $response = [Text.Encoding]::UTF8.GetBytes((@{ dpapi = $dpapi; certificateMatched = $true; dpapiRoundtrip = $true } | ConvertTo-Json -Compress))
    } else {
        foreach ($path in @($Gpg, $GpgConf, $Pinentry)) { Assert-LocalRegular $path }
        Assert-LocalRegular $GpgHome $true
        if ($CipherSha256 -notmatch '\A[0-9a-f]{64}\z') { throw 'Cipher hash required' }
        $inputBytes = Read-PrivatePipe (64 * 1024 * 1024)
        if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputBytes)).ToLowerInvariant() -cne $CipherSha256) { throw 'Cipher changed' }
        $configPath = Join-Path $GpgHome 'gpg-agent.conf'
        # A fresh owner-only home is supplied by the locked Python parent.
        $config = [Text.Encoding]::UTF8.GetBytes(('pinentry-program ' + $Pinentry.Replace('\', '/') + "`nno-allow-external-cache`n"))
        $configStream = [IO.File]::Open($configPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $configStream.Write($config, 0, $config.Length) } finally { $configStream.Dispose() }
        $agentHomeReady = $true
        Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
public static class IapV2PrivateChild {
 static async Task<byte[]> Drain(Stream stream, int limit) {
  using (var memory = new MemoryStream()) {
   byte[] buffer = new byte[8192];
   try {
    int size;
    while ((size = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
     if (memory.Length + size > limit) throw new IOException("Pipe limit");
     memory.Write(buffer, 0, size);
    }
    return memory.ToArray();
   } finally { Array.Clear(buffer, 0, buffer.Length); if(memory.TryGetBuffer(out var segment)) Array.Clear(segment.Array,segment.Offset,segment.Count); }
  }
 }
 static void Discard(Task<byte[]> task) {
  if(task == null) return;
  task.ContinueWith(done => { if(done.Status == TaskStatus.RanToCompletion) Array.Clear(done.Result,0,done.Result.Length); }, TaskContinuationOptions.ExecuteSynchronously);
 }
 public static byte[] Run(string executable, string[] args, byte[] input, int seconds, int limit) {
  var start = new ProcessStartInfo(executable) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true };
  foreach(var arg in args) start.ArgumentList.Add(arg);
  using(var child = new Process { StartInfo=start }) {
   Task<byte[]> output=null, error=null; Task write=null; bool transferred=false, started=false;
   try {
    if(!child.Start()) throw new IOException("Child start"); started=true;
    output=Drain(child.StandardOutput.BaseStream,limit); error=Drain(child.StandardError.BaseStream,65536);
    write=Task.Run(async()=>{try {await child.StandardInput.BaseStream.WriteAsync(input,0,input.Length);}finally{child.StandardInput.Close();}});
    var deadline=DateTime.UtcNow.AddSeconds(seconds);
    while(!child.WaitForExit(100)) if(DateTime.UtcNow>deadline || output.IsFaulted || error.IsFaulted || write.IsFaulted) throw new IOException("Child incomplete");
    if(!Task.WaitAll(new Task[]{output,error,write},5000) || child.ExitCode!=0) throw new IOException("Child failed");
    transferred=true; return output.Result;
   } finally {
    try { if(started && !child.HasExited){child.Kill(true);child.WaitForExit(5000);} }
    finally {if(!transferred)Discard(output);Discard(error);}
   }
  }
 }
}
'@
        $gpgHomeArg = $GpgHome
        # Existing Git/MSYS GPG uses Unix sockets; native GPG keeps Windows paths.
        if ($Gpg.Replace('\', '/') -match '/Git/usr/bin/') {
            $gpgHomeArg = '/' + $GpgHome.Substring(0, 1).ToLowerInvariant() + $GpgHome.Substring(2).Replace('\', '/')
            if ([Text.Encoding]::UTF8.GetByteCount($gpgHomeArg + '/S.gpg-agent.browser') -gt 100) { throw 'Agent socket path limit' }
        }
        $plain = [IapV2PrivateChild]::Run($Gpg, [string[]]@('--no-options', '--homedir', $gpgHomeArg, '--no-symkey-cache', '--pinentry-mode', 'ask', '--decrypt'), $inputBytes, 900, 64*1024*1024)
        # Cleanup success is required before decrypted bytes leave this worker.
        $cleanup = [IapV2PrivateChild]::Run($GpgConf, [string[]]@('--homedir', $gpgHomeArg, '--kill', 'gpg-agent'), [byte[]]::new(0), 10, 65536)
        Clear-Bytes $cleanup
        $agentHomeReady = $false
        $response = $plain
    }
    $success = $true
} catch {
    # No exception message/stack/native streams on stdout or stderr.
    $success = $false
} finally {
    if ($agentHomeReady -and ('IapV2PrivateChild' -as [type])) {
        try {
            $cleanup = [IapV2PrivateChild]::Run($GpgConf, [string[]]@('--homedir', $gpgHomeArg, '--kill', 'gpg-agent'), [byte[]]::new(0), 10, 65536)
            Clear-Bytes $cleanup
        } catch { $success = $false }
    }
    if ($originalPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($originalPointer) }
    if ($roundtripPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($roundtripPointer) }
    if ($null -ne $secure) { $secure.Dispose() }
    if ($null -ne $restored) { $restored.Dispose() }
    $secret = $request = $null
    Clear-Bytes $inputBytes; Clear-Bytes $der; Clear-Bytes $pemDer
}
try {
    if (!$success) { exit 1 }
    Write-PrivatePipe $response
} finally {
    Clear-Bytes $response
    if ($null -ne $plain -and ![object]::ReferenceEquals($plain, $response)) { Clear-Bytes $plain }
}
