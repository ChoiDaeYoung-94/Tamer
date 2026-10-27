using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

public sealed class LauncherPin {
    public string Path {get;set;}
    public string Sha256 {get;set;}
    public string Identity {get;set;}
    public string Sddl {get;set;}
}
public sealed class LauncherBinding {
    public int Schema {get;set;}
    public string Checkout {get;set;}
    public string Head {get;set;}
    public string Branch {get;set;}
    public string Plan {get;set;}
    public string PlanSha256 {get;set;}
    public string SourceReview {get;set;}
    public string SourceReviewSha256 {get;set;}
    public string Certificate {get;set;}
    public string CertificateSha256 {get;set;}
    public string Alias {get;set;}
    public bool CurrentUploadCertificateReviewed {get;set;}
    public bool ExistingKeyOwnershipReviewed {get;set;}
    public string Python {get;set;}
    public string Git {get;set;}
    public string Powershell {get;set;}
    public string Java {get;set;}
    public string ClassDirectory {get;set;}
    public string Bridge {get;set;}
    public DateTimeOffset DeadlineUtc {get;set;}
    public int MinimumStartBudgetSeconds {get;set;}
    public int ProducerTimeoutSeconds {get;set;}
    public string[] RuntimeFiles {get;set;}
    public string[] AbsentRuntimeFiles {get;set;}
    public LauncherPin[] Pins {get;set;}
}
public sealed class LauncherResult {
    public string Status {get;set;}
    public string Phase {get;set;}
    public string ExceptionType {get;set;}
    public bool OwnedChildStarted {get;set;}
    public bool? ProducerInvoked {get;set;}
    public bool? ActualBuildExecuted {get;set;}
    public bool ExternalRecoveryRequired {get;set;}
    public bool BinaryVerified {get {return false;}}
    public bool Distributable {get {return false;}}
}
internal sealed class SanitizedBridgeFailure : Exception {
    internal readonly string RemoteType,RemotePhase;
    internal readonly bool ProducerInvoked;
    internal SanitizedBridgeFailure(string type,string phase,bool producer) {RemoteType=type;RemotePhase=phase;ProducerInvoked=producer;}
}

public static class LauncherFiles {
    [StructLayout(LayoutKind.Sequential)] struct NativeInfo {
        public uint Attributes,CreationLow,CreationHigh,AccessLow,AccessHigh;
        public uint WriteLow,WriteHigh,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle file,out NativeInfo info);
    public static bool IsLocalAbsolutePath(string path) {
        if(String.IsNullOrWhiteSpace(path) || path.IndexOf('\0')>=0)return false;
        string value=path.Replace('\\','/');
        return Regex.IsMatch(value,"\\A[A-Za-z]:/") && Path.IsPathFullyQualified(path) &&
            Array.IndexOf(value.Split('/'),"..")<0;
    }
    public static void Regular(string path,bool directory=false) {
        if(!IsLocalAbsolutePath(path))throw new SecurityException();
        string cursor=path;bool first=true;
        while(!String.IsNullOrEmpty(cursor)) {
            var attributes=File.GetAttributes(cursor);
            if((attributes&FileAttributes.ReparsePoint)!=0 ||
                ((attributes&FileAttributes.Directory)!=0)!=(!first||directory))throw new SecurityException();
            cursor=Path.GetDirectoryName(cursor);first=false;
        }
    }
    public static string Identity(FileStream file) {
        NativeInfo x;
        if(!GetFileInformationByHandle(file.SafeFileHandle,out x))throw new IOException();
        if(x.Links!=1)throw new SecurityException();
        return String.Format("{0:x8}:{1:x8}:{2:x8}:{3:x8}:{4:x8}:{5:x8}:{6:x8}",
            x.Volume,x.IndexHigh,x.IndexLow,x.SizeHigh,x.SizeLow,x.WriteHigh,x.WriteLow);
    }
    public static string Acl(string path) {
        return FileSystemAclExtensions.GetAccessControl(new FileInfo(path),
            AccessControlSections.Owner|AccessControlSections.Group|AccessControlSections.Access)
            .GetSecurityDescriptorSddlForm(AccessControlSections.Owner|AccessControlSections.Group|AccessControlSections.Access);
    }
    public static string Hash(FileStream file) {
        file.Position=0;
        using(var hash=SHA256.Create())return Convert.ToHexString(hash.ComputeHash(file)).ToLowerInvariant();
    }
    public static byte[] Snapshot(FileStream file,int limit) {
        if(file.Length<1 || file.Length>limit)throw new IOException();
        var result=new byte[(int)file.Length];file.Position=0;
        try {file.ReadExactly(result);if(file.ReadByte()!=-1)throw new IOException();return result;}
        catch {Array.Clear(result,0,result.Length);throw;}
    }
}
internal sealed class LockedLauncherPin : IDisposable {
    internal readonly LauncherPin Pin;
    internal readonly FileStream File;
    internal LockedLauncherPin(LauncherPin pin) {
        Pin=pin;
        LauncherFiles.Regular(pin.Path);
        File=new FileStream(pin.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
        try {Verify();}catch{File.Dispose();throw;}
    }
    internal void Verify() {
        LauncherFiles.Regular(Pin.Path);
        if(!Regex.IsMatch(Pin.Sha256??"","\\A[0-9a-f]{64}\\z") ||
            LauncherFiles.Identity(File)!=Pin.Identity || LauncherFiles.Acl(Pin.Path)!=Pin.Sddl ||
            LauncherFiles.Hash(File)!=Pin.Sha256)throw new SecurityException();
    }
    public void Dispose(){File.Dispose();}
}

internal static class LauncherProtocol {
    internal static void Integer(Stream stream,int value) {
        stream.WriteByte((byte)(value>>24));stream.WriteByte((byte)(value>>16));
        stream.WriteByte((byte)(value>>8));stream.WriteByte((byte)value);
    }
    internal static void Field(Stream stream,byte[] value) {Integer(stream,value.Length);stream.Write(value,0,value.Length);}
    internal static void Password(Stream stream,char[] value) {
        Integer(stream,value.Length);
        foreach(char item in value){stream.WriteByte((byte)(item>>8));stream.WriteByte((byte)item);}
    }
    internal static void Validate(char[][] secret) {
        if(secret==null || secret.Length!=2)throw new SecurityException();
        foreach(var chars in secret) {
            if(chars==null || chars.Length<1 || chars.Length>1024)throw new SecurityException();
            for(int i=0;i<chars.Length;i++) {
                char c=chars[i];if(c=='\0')throw new SecurityException();
                if(Char.IsHighSurrogate(c)){if(++i>=chars.Length || !Char.IsLowSurrogate(chars[i]))throw new SecurityException();}
                else if(Char.IsLowSurrogate(c))throw new SecurityException();
            }
        }
    }
    internal static void Clear(char[][] secret){if(secret!=null)foreach(var chars in secret)if(chars!=null)Array.Clear(chars,0,chars.Length);}
}

internal static class LauncherProcess {
    // No credential is installed into this process's environment. All inherited
    // credential/injection variables are removed from each new child block.
    internal static ProcessStartInfo Start(string executable,string directory,params string[] arguments) {
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=directory};
        var remove=new List<string>();
        foreach(var pair in start.Environment) {
            string name=pair.Key.ToUpperInvariant();
            if(name.StartsWith("TAMER_") || name.StartsWith("PYTHON") || name.StartsWith("DOTNET_") ||
                name.StartsWith("COMPLUS_") || name=="JAVA_TOOL_OPTIONS" || name=="JDK_JAVA_OPTIONS" ||
                name=="_JAVA_OPTIONS" || name=="CLASSPATH")remove.Add(pair.Key);
        }
        foreach(string name in remove)start.Environment.Remove(name);
        foreach(string argument in arguments)start.ArgumentList.Add(argument);
        return start;
    }
    static async Task<byte[]> Drain(Stream stream,int limit) {
        using(var output=new MemoryStream()) {
            var buffer=new byte[1024];
            try {
                int count;
                while((count=await stream.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false))>0) {
                    if(output.Length+count>limit)throw new IOException();
                    output.Write(buffer,0,count);
                }
                return output.ToArray();
            }finally{
                Array.Clear(buffer,0,buffer.Length);ArraySegment<byte> raw;
                if(output.TryGetBuffer(out raw))Array.Clear(raw.Array,raw.Offset,raw.Count);
            }
        }
    }
    internal static T Run<T>(ProcessStartInfo start,Action<Stream> send,int timeout,CancellationToken cancel,
        Func<byte[],byte[],int,T> complete,Action started=null) {
        OwnedLauncherChild child=null;Task<byte[]> output=null,error=null;Task write=null;
        var timer=Stopwatch.StartNew();
        try {
            cancel.ThrowIfCancellationRequested();
            child=OwnedLauncherChild.Start(start);
            if(started!=null)started();
            cancel.ThrowIfCancellationRequested();
            output=Drain(child.Output,16384);error=Drain(child.Error,4096);
            if(send==null)child.Input.Dispose();
            else write=Task.Run(()=>{using(var input=child.Input)send(input);});
            while(!child.WaitForExit(25)) {
                cancel.ThrowIfCancellationRequested();
                if(timer.ElapsedMilliseconds>=timeout || output.IsFaulted || error.IsFaulted ||
                    (write!=null && write.IsFaulted))throw new IOException();
            }
            int code=child.ExitCode;
            child.StopOwned(); // Also terminate inherited-pipe/quiet descendants.
            cancel.ThrowIfCancellationRequested();
            Task[] tasks=write==null?new Task[]{output,error}:new Task[]{output,error,write};
            if(!Task.WaitAll(tasks,(int)Math.Max(0,timeout-timer.ElapsedMilliseconds)))throw new IOException();
            return complete(output.Result,error.Result,code);
        }finally{
            try{if(child!=null)child.Dispose();}
            finally{
                if(write!=null)try{write.Wait(5000);}catch{}
                foreach(var task in new[]{output,error})if(task!=null)task.ContinueWith(t=>{
                    if(t.Status==TaskStatus.RanToCompletion)Array.Clear(t.Result,0,t.Result.Length);
                },TaskContinuationOptions.ExecuteSynchronously);
            }
        }
    }
    internal static JsonDocument Json(byte[] output,byte[] error,int code) {
        // Diagnostics are never rendered, persisted, or copied into exceptions.
        if(code!=0 || error.Length!=0)throw new IOException();
        try{return JsonDocument.Parse(output);}catch{throw new IOException();}
    }
    internal static JsonDocument BridgeJson(byte[] output,byte[] error,int code) {
        if(code==1 && error.Length==0) {
            using(var doc=Json(output,error,0)) {
                var r=doc.RootElement;Exact(r,"schema","status","exceptionType","phase","producerInvoked","actualBuildExecuted","binaryVerified","distributable","externalRecoveryRequired");
                string type=r.GetProperty("exceptionType").GetString(),phase=r.GetProperty("phase").GetString();
                bool invoked=r.GetProperty("producerInvoked").GetBoolean();
                var actual=r.GetProperty("actualBuildExecuted");
                if(r.GetProperty("schema").GetInt32()!=1 || r.GetProperty("status").GetString()!="failed" ||
                    !Regex.IsMatch(type??"","\\A[A-Za-z0-9_]{1,100}\\z") ||
                    !new HashSet<string>{"binding","preflight","input","challenge","recheck","receipt","producer"}.Contains(phase) ||
                    (invoked?actual.ValueKind!=JsonValueKind.Null:actual.ValueKind!=JsonValueKind.False) ||
                    r.GetProperty("binaryVerified").GetBoolean() || r.GetProperty("distributable").GetBoolean() ||
                    r.GetProperty("externalRecoveryRequired").GetBoolean()!=invoked)throw new IOException();
                throw new SanitizedBridgeFailure(type,phase,invoked);
            }
        }
        return Json(output,error,code);
    }
    internal static void Exact(JsonElement value,params string[] fields) {
        var names=new HashSet<string>(fields,StringComparer.Ordinal);int count=0;
        foreach(var pair in value.EnumerateObject()){if(!names.Remove(pair.Name))throw new SecurityException();count++;}
        if(names.Count!=0 || count!=fields.Length)throw new SecurityException();
    }
}

// This seam is internal and compiled with the synthetic harness only. The public
// entry point always constructs reviewed file/producer/challenge implementations.
internal static class LauncherSession {
    internal static LauncherResult Run(Action gate,Func<char[][]> prompt,
        Action<char[][]> producer,Action deadline) {
        var result=new LauncherResult{Status="failed",Phase="preflight",ExceptionType="",ProducerInvoked=false,ActualBuildExecuted=false};char[][] secret=null;
        try {
            deadline();gate();deadline();result.Phase="prompt";secret=prompt();
            LauncherProtocol.Validate(secret);deadline();
            result.Phase="recheck";gate();deadline();result.Phase="producer";
            // OwnedChildStarted is marked by the native launch callback in the
            // public entry, not by entering this delegate before startup succeeds.
            result.ProducerInvoked=null;result.ActualBuildExecuted=null;
            producer(secret);result.Status="producer_completed";result.Phase="completed";result.ActualBuildExecuted=true;result.ProducerInvoked=true;
        }catch(Exception failure){
            var remote=failure as SanitizedBridgeFailure;
            string type=failure.GetType().Name;
            result.ExceptionType=remote!=null?remote.RemoteType:Regex.IsMatch(type,"\\A[A-Za-z0-9_]{1,100}\\z")?type:"Exception";
            if(remote!=null){result.Phase=remote.RemotePhase;result.ProducerInvoked=remote.ProducerInvoked;result.ActualBuildExecuted=remote.ProducerInvoked?(bool?)null:false;}
            if(failure is OperationCanceledException)result.Status="cancelled";
        }finally{LauncherProtocol.Clear(secret);}
        return result;
    }
}

public static class SigningLauncher {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessageW(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    static readonly JsonSerializerOptions Options=new JsonSerializerOptions {
        PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    static char[] Read(TextBox box) {
        int length=(int)SendMessageW(box.Handle,0x000e,IntPtr.Zero,IntPtr.Zero);
        if(length<1 || length>1024)throw new SecurityException();
        IntPtr raw=Marshal.AllocHGlobal((length+1)*2);
        try {
            if((int)SendMessageW(box.Handle,0x000d,(IntPtr)(length+1),raw)!=length)throw new IOException();
            var result=new char[length];Marshal.Copy(raw,result,0,length);return result;
        }finally{for(int i=0;i<(length+1)*2;i++)Marshal.WriteByte(raw,i,0);Marshal.FreeHGlobal(raw);box.Clear();}
    }
    static void Budget(LauncherBinding b) {
        if(b.MinimumStartBudgetSeconds<600 || b.ProducerTimeoutSeconds<1 || b.ProducerTimeoutSeconds>86400 ||
            (b.DeadlineUtc-DateTimeOffset.UtcNow).TotalSeconds<b.MinimumStartBudgetSeconds)throw new TimeoutException();
    }
    static char[][] Prompt(LauncherBinding b) {
        using(var form=new Form())using(var store=new TextBox())using(var alias=new TextBox())
        using(var ok=new Button())using(var cancel=new Button())using(var tick=new System.Windows.Forms.Timer()) {
            form.Text="Tamer 검토된 빌드 — 기존 암호 직접 입력";form.ClientSize=new Size(610,235);
            form.StartPosition=FormStartPosition.CenterScreen;form.FormBorderStyle=FormBorderStyle.FixedDialog;
            form.MaximizeBox=false;form.MinimizeBox=false;
            var info=new Label{Text="기존 키를 검사한 뒤 검토된 빌드를 한 번 실행합니다. 두 암호를 직접 입력해 주세요.",Location=new Point(18,15),Size=new Size(575,45)};
            store.SetBounds(220,72,365,27);alias.SetBounds(220,117,365,27);
            foreach(var box in new[]{store,alias}){box.UseSystemPasswordChar=true;box.MaxLength=1024;box.ShortcutsEnabled=false;}
            var first=new Label{Text="기존 저장소 비밀번호",Location=new Point(18,76),AutoSize=true};
            var second=new Label{Text="기존 키(alias) 비밀번호",Location=new Point(18,121),AutoSize=true};
            ok.Text="검사 후 빌드";ok.SetBounds(350,178,115,32);ok.DialogResult=DialogResult.OK;
            cancel.Text="취소";cancel.SetBounds(475,178,110,32);cancel.DialogResult=DialogResult.Cancel;
            form.AcceptButton=ok;form.CancelButton=cancel;form.Controls.AddRange(new Control[]{info,first,second,store,alias,ok,cancel});
            bool timedOut=false;
            tick.Interval=250;tick.Tick+=(sender,e)=>{try{Budget(b);}catch(TimeoutException){timedOut=true;form.DialogResult=DialogResult.Cancel;form.Close();}};tick.Start();
            char[] one=null;
            try {var answer=form.ShowDialog();PromptExit(timedOut,answer==DialogResult.OK);Budget(b);one=Read(store);return new[]{one,Read(alias)};}
            catch{if(one!=null)Array.Clear(one,0,one.Length);throw;}
            finally{tick.Stop();store.Clear();alias.Clear();}
        }
    }
    internal static void PromptExit(bool timedOut,bool accepted) {
        if(timedOut)throw new TimeoutException();
        if(!accepted)throw new OperationCanceledException();
    }
    internal static void CompleteOwnership(LauncherResult result,bool started) {
        result.OwnedChildStarted=started;result.ExternalRecoveryRequired=started&&result.ProducerInvoked!=false;
        if(!started){result.ProducerInvoked=false;result.ActualBuildExecuted=false;}
    }
    static ProcessStartInfo BridgeStart(LauncherBinding b,string action,string path,string hash,string attempt) {
        StartupFiles(b);
        // A pinned regular plan file is a non-directory cache prefix. No .pyc
        // can exist below it, including stdlib caches read during interpreter startup.
        var start=LauncherProcess.Start(b.Python,Path.GetDirectoryName(b.Python),"-I","-S","-B",
            "-X","pycache_prefix="+b.Plan,b.Bridge,action,
            "--binding",path,"--binding-sha256",hash,"--attempt",attempt);
        if(action=="run")start.ArgumentList.Add("--allow-reviewed-build");
        return start;
    }
    static void Gate(LauncherBinding b,string action,string path,string hash,string attempt,CancellationToken cancel) {
        LauncherProcess.Run(BridgeStart(b,action,path,hash,attempt),null,120000,cancel,(output,error,code)=>{
            using(var doc=LauncherProcess.BridgeJson(output,error,code)) {
                var r=doc.RootElement;LauncherProcess.Exact(r,"schema","status","attemptId","sourceHead","planSha256");
                if(r.GetProperty("schema").GetInt32()!=1 || r.GetProperty("status").GetString()!="ready" ||
                    r.GetProperty("attemptId").GetString()!=attempt || r.GetProperty("sourceHead").GetString()!=b.Head ||
                    r.GetProperty("planSha256").GetString()!=b.PlanSha256)throw new SecurityException();
            }return true;
        });
    }
    static LockedLauncherPin Find(List<LockedLauncherPin> pins,string path) {
        foreach(var pin in pins)if(String.Equals(Path.GetFullPath(pin.Pin.Path),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))return pin;
        throw new SecurityException();
    }
    static void Produce(LauncherBinding b,string path,string hash,string attempt,char[][] secret,
        CancellationToken cancel,Action started) {
        int timeout=(int)Math.Min(b.ProducerTimeoutSeconds*1000L,Math.Max(1,(b.DeadlineUtc-DateTimeOffset.UtcNow).TotalMilliseconds));
        LauncherProcess.Run(BridgeStart(b,"run",path,hash,attempt),input=>{
            LauncherProtocol.Integer(input,0x544C5031);LauncherProtocol.Password(input,secret[0]);
            LauncherProtocol.Password(input,secret[1]);input.Flush();
        },timeout,cancel,(output,error,code)=>{
            using(var doc=LauncherProcess.BridgeJson(output,error,code)) {
                var r=doc.RootElement;LauncherProcess.Exact(r,"schema","status","exceptionType","phase","producerInvoked","actualBuildExecuted","binaryVerified","distributable","externalRecoveryRequired");
                if(r.GetProperty("schema").GetInt32()!=1 || r.GetProperty("status").GetString()!="producer_completed" ||
                    r.GetProperty("exceptionType").GetString()!="" || r.GetProperty("phase").GetString()!="completed" ||
                    !r.GetProperty("producerInvoked").GetBoolean() || !r.GetProperty("actualBuildExecuted").GetBoolean() || r.GetProperty("binaryVerified").GetBoolean() ||
                    r.GetProperty("distributable").GetBoolean() || !r.GetProperty("externalRecoveryRequired").GetBoolean())throw new SecurityException();
            }return true;
        },started);
    }
    internal static void StartupFiles(LauncherBinding b) {
        string directory=Path.GetFullPath(Path.GetDirectoryName(b.Python));
        var expected=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
            Path.Combine(directory,Path.GetFileNameWithoutExtension(b.Python)+"._pth"),
            Path.Combine(directory,"pyvenv.cfg"),Path.Combine(Path.GetDirectoryName(directory),"pyvenv.cfg")};
        foreach(string dll in Directory.GetFiles(directory,"python*.dll"))expected.Add(Path.ChangeExtension(dll,"._pth"));
        var absent=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string path in b.AbsentRuntimeFiles??new string[0])absent.Add(Path.GetFullPath(path));
        if(absent.Count!=(b.AbsentRuntimeFiles??new string[0]).Length)throw new SecurityException();
        foreach(string path in expected) {
            bool exists=File.Exists(path)||Directory.Exists(path);
            // Current contract supports a standalone runtime only. A startup
            // redirect requires a new reviewed runtime contract, never an
            // automatic widening to external import paths before validation.
            if(exists || !absent.Remove(path))throw new SecurityException();
        }
        if(absent.Count!=0)throw new SecurityException();
    }
    static void Progress(Action<CancellationToken> work) {
        using(var form=new Form())using(var cancel=new Button())using(var tick=new System.Windows.Forms.Timer())
        using(var cts=new CancellationTokenSource()) {
            form.Text="Tamer 검토된 빌드 실행 중";form.ClientSize=new Size(520,130);form.StartPosition=FormStartPosition.CenterScreen;
            form.FormBorderStyle=FormBorderStyle.FixedDialog;form.MaximizeBox=false;form.MinimizeBox=false;
            form.Controls.Add(new Label{Text="취소하면 소유 실행을 종료하고 복구 근거를 보존합니다.",Location=new Point(18,20),Size=new Size(485,35)});
            cancel.Text="실행 취소";cancel.SetBounds(360,75,135,32);cancel.Click+=(sender,e)=>{cts.Cancel();cancel.Enabled=false;};form.Controls.Add(cancel);
            Task task=null;bool finished=false;form.Shown+=(sender,e)=>{task=Task.Run(()=>work(cts.Token));tick.Start();};
            form.FormClosing+=(sender,e)=>{if(!finished){e.Cancel=true;cts.Cancel();cancel.Enabled=false;}};
            tick.Interval=100;tick.Tick+=(sender,e)=>{if(task!=null && task.IsCompleted){finished=true;tick.Stop();form.Close();}};
            form.ShowDialog();if(task==null)throw new IOException();task.GetAwaiter().GetResult();
        }
    }
    public static string Run(string bindingPath,string bindingSha256,bool allowReviewedBuild) {
        var pins=new List<LockedLauncherPin>();FileStream bound=null;bool producerStarted=false;
        LauncherResult result=null;
        try {
            if(!allowReviewedBuild || Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)throw new SecurityException();
            LauncherFiles.Regular(bindingPath);bound=new FileStream(bindingPath,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(!Regex.IsMatch(bindingSha256??"","\\A[0-9a-f]{64}\\z") || LauncherFiles.Hash(bound)!=bindingSha256)throw new SecurityException();
            byte[] raw=LauncherFiles.Snapshot(bound,8*1024*1024);LauncherBinding b;
            try{b=JsonSerializer.Deserialize<LauncherBinding>(raw,Options);}finally{Array.Clear(raw,0,raw.Length);}
            if(b==null || b.Schema!=1 || !b.CurrentUploadCertificateReviewed || !b.ExistingKeyOwnershipReviewed ||
                b.Pins==null || b.Pins.Length<1 || b.RuntimeFiles==null || b.RuntimeFiles.Length<1 || b.AbsentRuntimeFiles==null)throw new SecurityException();
            Budget(b);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var pin in b.Pins){if(!paths.Add(Path.GetFullPath(pin.Path)))throw new SecurityException();pins.Add(new LockedLauncherPin(pin));}
            // No bridge/Java execution before executable + source files have been pinned.
            foreach(string path in new[]{b.Python,b.Git,b.Powershell,b.Java,b.Bridge,
                Path.Combine(b.ClassDirectory,"OperatingKeyPreflight.class"),b.Plan,b.SourceReview,b.Certificate,
                Path.Combine(b.Checkout,"src","AeDeong.keystore")})Find(pins,path);
            foreach(string path in b.RuntimeFiles)Find(pins,path);
            string launcherRoot=Path.Combine(b.Checkout,"tools","revival","signing_launcher");
            if(!String.Equals(Path.GetFullPath(b.Bridge),Path.Combine(launcherRoot,"producer_bridge.py"),StringComparison.OrdinalIgnoreCase))throw new SecurityException();
            foreach(string name in new[]{"OwnedChild.cs","SigningLauncher.cs","OperatingKeyPreflight.java","producer_bridge.py","Invoke-SigningLauncher.ps1","EditorOwnership.ps1"})
                Find(pins,Path.Combine(launcherRoot,name));
            foreach(string name in new[]{"private_ads_producer.py","private_ads_build.py","private_ads_approved.py",
                "private_ads_evidence.py","validate_private_ads_preparation.py","windows_owned_files.py"})
                Find(pins,Path.Combine(b.Checkout,"tools","revival",name));
            string attempt=Guid.NewGuid().ToString("N");bool claimed=false;
            result=LauncherSession.Run(()=>{
                foreach(var pin in pins)pin.Verify();
                if(LauncherFiles.Hash(bound)!=bindingSha256)throw new SecurityException();
                Gate(b,claimed?"inspect":"claim",bindingPath,bindingSha256,attempt,CancellationToken.None);claimed=true;
            },()=>Prompt(b),secret=>
                Progress(cancel=>Produce(b,bindingPath,bindingSha256,attempt,secret,cancel,()=>producerStarted=true)),()=>Budget(b));
        }catch(Exception failure){result=new LauncherResult{Status=failure is OperationCanceledException?"cancelled":"failed",Phase="preflight",ExceptionType=failure.GetType().Name,ProducerInvoked=false,ActualBuildExecuted=false};}
        finally{for(int i=pins.Count-1;i>=0;i--)pins[i].Dispose();if(bound!=null)bound.Dispose();}
        CompleteOwnership(result,producerStarted);
        return JsonSerializer.Serialize(result,Options);
    }
}
