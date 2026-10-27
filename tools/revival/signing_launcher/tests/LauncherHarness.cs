using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Collections.Generic;
using System.Security;
using Microsoft.Win32.SafeHandles;

public sealed class LauncherTestRecord {
    public string Case {get;set;}
    public bool Passed {get;set;}
    public string ExceptionType {get;set;}
    public long ElapsedMs {get;set;}
    public bool SentinelAlive {get;set;}
}
public static class LauncherHarness {
    static void Require(bool value){if(!value)throw new InvalidOperationException("Synthetic assertion failed");}
    static void WaitMarker(string path) {
        var clock=Stopwatch.StartNew();while(!File.Exists(path)){if(clock.ElapsedMilliseconds>5000)throw new IOException();Thread.Sleep(10);}
    }
    static void Exited(string path,bool expectJob) {
        WaitMarker(path);
        using(var data=JsonDocument.Parse(File.ReadAllBytes(path))) {
            Require(data.RootElement.GetProperty("job").GetBoolean()==expectJob);
            int pid=data.RootElement.GetProperty("pid").GetInt32();
            try{using(var found=Process.GetProcessById(pid))Require(found.WaitForExit(1000));}catch(ArgumentException){}
        }
    }
    static void TreeExited(string path) {Exited(path,true);Exited(path+".middle",true);Exited(path+".middle.leaf",true);}
    static ProcessStartInfo Fixture(string python,string script,string mode,string marker) {
        return LauncherProcess.Start(python,Path.GetDirectoryName(python),"-I","-S","-B",script,mode,marker);
    }
    static char[][] Secrets(){return new[]{"합성-store\n🔑".ToCharArray(),"합성-key\r\né".ToCharArray()};}
    static void Send(Stream input,char[][] secret){LauncherProtocol.Integer(input,0x544C5031);foreach(var item in secret)LauncherProtocol.Password(input,item);input.Flush();}
    static LauncherPin Pin(string path) {
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            return new LauncherPin{Path=path,Identity=LauncherFiles.Identity(file),Sha256=LauncherFiles.Hash(file),Sddl=LauncherFiles.Acl(path)};
    }
    static void Persist(string evidence,List<LauncherTestRecord> rows) {
        File.WriteAllText(Path.Combine(evidence,"native-results.json"),JsonSerializer.Serialize(rows));
    }
    public static void AbruptOwner(string python,string script,string marker) {
        var child=OwnedLauncherChild.Start(Fixture(python,script,"tree",marker));
        WaitMarker(marker+".middle.leaf");
        // Deliberately no Dispose/finally: OS close of this process's noninherited
        // private Job handle must kill its three generation fixture tree.
        GC.KeepAlive(child);Environment.Exit(31);
    }
    public static void AbruptBeforeResume(string python,string script,string marker) {
        // Supply our own configured Job through Start's normal setup by calling
        // the test hook on a private job created with the same native API.
        AtomicOwnerFixture.DieBeforeResume(Fixture(python,script,"tree",marker),marker);
    }
    public static string BridgeJava(string python,string script,string java,string classes,string evidence) {
        var secret=Secrets();
        try {
            return LauncherProcess.Run(LauncherProcess.Start(python,Path.GetDirectoryName(python),"-I","-S","-B","-X","pycache_prefix="+script,
                script,java,classes,evidence),input=>Send(input,secret),10000,CancellationToken.None,(output,error,code)=>{
                    using(var doc=LauncherProcess.Json(output,error,code)) {
                        var r=doc.RootElement;LauncherProcess.Exact(r,"passed","phase","exceptionType","producerCalls","receiptCalls","parentAndWorkerJobRequired");
                        Require(r.GetProperty("passed").GetBoolean()&&r.GetProperty("phase").GetString()=="challenge"&&
                            r.GetProperty("producerCalls").GetInt32()==0&&r.GetProperty("receiptCalls").GetInt32()==0&&r.GetProperty("parentAndWorkerJobRequired").GetBoolean());
                        return r.GetRawText();
                    }
                });
        }finally{LauncherProtocol.Clear(secret);}
    }
    public static string BridgeJavaSuccess(string python,string script,string java,string keytool,string classes,string evidence) {
        var before=new List<string>();
        foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
            foreach(var name in new[]{"TAMER_KEYSTORE_PASS","TAMER_KEYALIAS_PASS","FIXTURE_STORE_PASS","FIXTURE_ALIAS_PASS"})before.Add(Environment.GetEnvironmentVariable(name,target));
        var secret=Secrets();string result;
        try {
            result=LauncherProcess.Run(LauncherProcess.Start(python,Path.GetDirectoryName(python),"-I","-S","-B","-X","pycache_prefix="+script,
                script,java,keytool,classes,evidence),input=>Send(input,secret),45000,CancellationToken.None,(output,error,code)=>{
                    using(var doc=LauncherProcess.Json(output,error,code)) {
                        var r=doc.RootElement;LauncherProcess.Exact(r,"passed","actualChallengeVerified","exactReceiptVerified","fakeProducerCalls","actualUnityBuildCalls","realApprovedGateCalls","ownedProducerEnvironmentCleared","binaryVerified","distributable");
                        Require(r.GetProperty("passed").GetBoolean()&&r.GetProperty("actualChallengeVerified").GetBoolean()&&
                            r.GetProperty("exactReceiptVerified").GetBoolean()&&r.GetProperty("fakeProducerCalls").GetInt32()==1&&
                            r.GetProperty("actualUnityBuildCalls").GetInt32()==0&&r.GetProperty("realApprovedGateCalls").GetInt32()==0&&
                            r.GetProperty("ownedProducerEnvironmentCleared").GetBoolean()&&!r.GetProperty("binaryVerified").GetBoolean()&&!r.GetProperty("distributable").GetBoolean());
                        return r.GetRawText();
                    }
                });
        }finally{LauncherProtocol.Clear(secret);}
        int index=0;
        foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
            foreach(var name in new[]{"TAMER_KEYSTORE_PASS","TAMER_KEYALIAS_PASS","FIXTURE_STORE_PASS","FIXTURE_ALIAS_PASS"})Require(before[index++]==Environment.GetEnvironmentVariable(name,target));
        return result;
    }
    public static string Run(string python,string fixture,string pwsh,string assembly,string evidence,bool delta=false) {
        var rows=new List<LauncherTestRecord>();
        string sentinelMarker=Path.Combine(evidence,"unrelated-sentinel.json");
        using(var sentinel=Process.Start(Fixture(python,fixture,"sentinel",sentinelMarker))) {
            try {
                WaitMarker(sentinelMarker);
                Action<string,Action> test=(name,work)=>{
                    if(delta && name!="sanitized-challenge-type-and-phase-survive-without-raw-diagnostics" &&
                        name!="runtime-startup-absence-changed-before-prompt-is-rejected" &&
                        name!="prompt-deadline-is-distinct-from-cancel-and-failed-start-is-false")return;
                    var timer=Stopwatch.StartNew();work();Require(!sentinel.HasExited);
                    rows.Add(new LauncherTestRecord{Case=name,Passed=true,ElapsedMs=timer.ElapsedMilliseconds,SentinelAlive=true});Persist(evidence,rows);
                };
                test("preflight-rejection-never-prompts-or-launches",()=>{
                    int prompt=0,producer=0;
                    var result=LauncherSession.Run(()=>{throw new SecurityException("synthetic-sensitive-value");},
                        ()=>{prompt++;return Secrets();},secret=>{producer++;},()=>{});
                    Require(prompt==0&&producer==0&&result.Status=="failed"&&result.ExceptionType=="SecurityException");
                });
                test("cancel-after-prompt-never-launches",()=>{
                    int producer=0;var result=LauncherSession.Run(()=>{},()=>{throw new OperationCanceledException();},secret=>{producer++;},()=>{});
                    Require(producer==0&&result.Status=="cancelled");
                });
                test("context-change-after-prompt-never-launches-and-clears",()=>{
                    int gate=0,producer=0;char[][] secret=Secrets();
                    var result=LauncherSession.Run(()=>{if(++gate==2)throw new SecurityException();},()=>secret,item=>{producer++;},()=>{});
                    Require(producer==0&&result.Status=="failed");foreach(var chars in secret)foreach(var c in chars)Require(c=='\0');
                });
                test("deadline-after-input-never-launches",()=>{
                    int calls=0,producer=0;var result=LauncherSession.Run(()=>{},()=>Secrets(),item=>{producer++;},()=>{if(++calls==3)throw new TimeoutException();});
                    Require(producer==0&&result.ExceptionType=="TimeoutException");
                });
                test("NUL-and-unpaired-surrogate-rejected",()=>{
                    foreach(var bad in new[]{"bad\0value","bad\ud800"}) {
                        int producer=0;var result=LauncherSession.Run(()=>{},()=>new[]{bad.ToCharArray(),"key".ToCharArray()},item=>{producer++;},()=>{});
                        Require(producer==0&&result.ExceptionType=="SecurityException");
                    }
                });
                test("hash-identity-acl-and-traversal-refuse-before-prompt",()=>{
                    string file=Path.Combine(evidence,"pin-fixture.txt");File.WriteAllText(file,"nonsecret synthetic bytes");
                    using(var held=new LockedLauncherPin(Pin(file)))held.Verify();
                    for(int kind=0;kind<4;kind++) {
                        var pin=Pin(file);
                        if(kind==0)pin.Sha256=new string('0',64);else if(kind==1)pin.Identity="stale";
                        else if(kind==2)pin.Sddl="stale";else pin.Path=Path.Combine(evidence,"..",Path.GetFileName(evidence),"pin-fixture.txt");
                        bool rejected=false;try{using(var held=new LockedLauncherPin(pin)){} }catch(SecurityException){rejected=true;}Require(rejected);
                    }
                });
                test("unicode-framing-child-only-environment-parent-unchanged",()=>{
                    var before=new List<string>();
                    foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
                        foreach(var name in new[]{"TAMER_KEYSTORE_PASS","TAMER_KEYALIAS_PASS"})before.Add(Environment.GetEnvironmentVariable(name,target));
                    var secret=Secrets();string marker=Path.Combine(evidence,"unicode.json");
                    try {
                        bool matched=LauncherProcess.Run(Fixture(python,fixture,"read",marker),input=>Send(input,secret),5000,CancellationToken.None,(output,error,code)=>{
                            using(var json=LauncherProcess.Json(output,error,code)){LauncherProcess.Exact(json.RootElement,"matched");return json.RootElement.GetProperty("matched").GetBoolean();}});
                        Require(matched&&File.ReadAllText(marker+".received")=="matched");Exited(marker,true);
                    }finally{LauncherProtocol.Clear(secret);}
                    int index=0;foreach(var target in new[]{EnvironmentVariableTarget.Process,EnvironmentVariableTarget.User,EnvironmentVariableTarget.Machine})
                        foreach(var name in new[]{"TAMER_KEYSTORE_PASS","TAMER_KEYALIAS_PASS"})Require(before[index++]==Environment.GetEnvironmentVariable(name,target));
                });
                foreach(string mode in new[]{"tree","tree-exit","overflow","partial","leak"}) {
                    string label=mode;
                    test("owned-"+label,()=>{
                        string marker=Path.Combine(evidence,label+".json");Exception failure=null;
                        try{LauncherProcess.Run(Fixture(python,fixture,label,marker),null,label=="tree"?1200:5000,CancellationToken.None,(output,error,code)=>{
                            using(var json=LauncherProcess.Json(output,error,code))return true;});}
                        catch(Exception problem){failure=problem;}
                        if(label=="tree-exit")Require(failure==null);else Require(failure!=null&&!failure.ToString().Contains("synthetic-sensitive-value"));
                        if(label.StartsWith("tree",StringComparison.Ordinal))TreeExited(marker);else Exited(marker,true);
                    });
                }
                test("cancel-three-generation-job-and-preserve-sentinel",()=>{
                    string marker=Path.Combine(evidence,"cancel.json");bool cancelled=false;
                    using(var cts=new CancellationTokenSource()) {
                        cts.CancelAfter(1200);
                        try{LauncherProcess.Run(Fixture(python,fixture,"tree",marker),null,5000,cts.Token,(a,b,c)=>true);}
                        catch(OperationCanceledException){cancelled=true;}
                    }
                    Require(cancelled);TreeExited(marker);
                });
                test("atomic-job-rejection-before-create-no-execution-no-secret-transfer",()=>{
                    string marker=Path.Combine(evidence,"assignment.json");uint pid=0;bool sent=false;Exception failure=null;
                    using(var invalid=new SafeFileHandle(new IntPtr(-1),false)) {
                        try{using(var child=OwnedLauncherChild.StartInJob(Fixture(python,fixture,"tree",marker),invalid,out pid)){sent=true;child.Input.WriteByte(1);}}
                        catch(Exception problem){failure=problem;}
                    }
                    Require(failure is System.ComponentModel.Win32Exception && pid==0 && !sent && !File.Exists(marker));
                });
                test("abrupt-owner-exit-kills-three-generation-job",()=>{
                    string marker=Path.Combine(evidence,"abrupt.json");
                    var start=LauncherProcess.Start(pwsh,Path.GetDirectoryName(pwsh),"-NoProfile","-NonInteractive","-File",
                        Path.Combine(Path.GetDirectoryName(fixture),"EarlyOwner.ps1"),"-Assembly",assembly,"-Python",python,"-Fixture",fixture,"-Marker",marker);
                    using(var owner=Process.Start(start)){Require(owner.WaitForExit(10000)&&owner.ExitCode==31);}
                    TreeExited(marker);
                });
                test("abrupt-owner-before-resume-kills-atomic-job-child",()=>{
                    string marker=Path.Combine(evidence,"before-resume.json");
                    var start=LauncherProcess.Start(pwsh,Path.GetDirectoryName(pwsh),"-NoProfile","-NonInteractive","-File",
                        Path.Combine(Path.GetDirectoryName(fixture),"EarlyOwner.ps1"),"-Assembly",assembly,"-Python",python,"-Fixture",fixture,"-Marker",marker,"-BeforeResume");
                    using(var owner=Process.Start(start)){Require(owner.WaitForExit(10000)&&owner.ExitCode==32);}
                    Require(!File.Exists(marker));WaitMarker(marker+".pid");int pid=Int32.Parse(File.ReadAllText(marker+".pid"));
                    try{using(var child=Process.GetProcessById(pid))Require(child.WaitForExit(1000));}catch(ArgumentException){}
                });
                test("result-exception-message-and-output-never-persisted",()=>{
                    var result=LauncherSession.Run(()=>{},()=>Secrets(),secret=>{throw new InvalidOperationException("synthetic-sensitive-value");},()=>{});
                    string encoded=JsonSerializer.Serialize(result);Require(!encoded.Contains("synthetic-sensitive-value")&&result.ExceptionType=="InvalidOperationException");
                });
                test("sanitized-challenge-type-and-phase-survive-without-raw-diagnostics",()=>{
                    byte[] raw=System.Text.Encoding.UTF8.GetBytes("{\"schema\":1,\"status\":\"failed\",\"exceptionType\":\"UnrecoverableKeyException\",\"phase\":\"challenge\",\"producerInvoked\":false,\"actualBuildExecuted\":false,\"binaryVerified\":false,\"distributable\":false,\"externalRecoveryRequired\":false}");
                    var result=LauncherSession.Run(()=>{},()=>Secrets(),secret=>{using(var json=LauncherProcess.BridgeJson(raw,new byte[0],1)){}},()=>{});
                    Require(result.Phase=="challenge"&&result.ExceptionType=="UnrecoverableKeyException"&&result.ProducerInvoked==false&&result.ActualBuildExecuted==false);
                    SigningLauncher.CompleteOwnership(result,true);Require(!result.ExternalRecoveryRequired);
                    bool rejected=false;try{using(var json=LauncherProcess.BridgeJson(raw,System.Text.Encoding.UTF8.GetBytes("synthetic-sensitive-value"),1)){} }catch(IOException){rejected=true;}Require(rejected);
                });
                test("runtime-startup-absence-changed-before-prompt-is-rejected",()=>{
                    string folder=Path.Combine(evidence,"runtime");Directory.CreateDirectory(folder);string executable=Path.Combine(folder,"python.exe");
                    var b=new LauncherBinding{Python=executable,RuntimeFiles=new string[0],AbsentRuntimeFiles=new[]{Path.Combine(folder,"python._pth"),Path.Combine(folder,"pyvenv.cfg"),Path.Combine(evidence,"pyvenv.cfg")}};
                    SigningLauncher.StartupFiles(b);File.WriteAllText(Path.Combine(folder,"python._pth"),"nonsecret unexpected startup redirect");
                    int prompt=0;var result=LauncherSession.Run(()=>SigningLauncher.StartupFiles(b),()=>{prompt++;return Secrets();},secret=>{},()=>{});
                    Require(prompt==0&&result.ExceptionType=="SecurityException");
                });
                test("prompt-deadline-is-distinct-from-cancel-and-failed-start-is-false",()=>{
                    foreach(bool expired in new[]{true,false}) {
                        Exception failure=null;try{SigningLauncher.PromptExit(expired,false);}catch(Exception caught){failure=caught;}
                        Require(expired?failure is TimeoutException:failure is OperationCanceledException);
                    }
                    bool started=false;
                    var result=LauncherSession.Run(()=>{},()=>Secrets(),secret=>{
                        LauncherProcess.Run(Fixture(Path.Combine(evidence,"not-an-executable.exe"),fixture,"unused","unused"),null,5000,CancellationToken.None,(a,b,c)=>true,()=>started=true);
                    },()=>{});
                    SigningLauncher.CompleteOwnership(result,started);
                    Require(!started&&result.ProducerInvoked==false&&result.ActualBuildExecuted==false&&!result.OwnedChildStarted);
                });
                return JsonSerializer.Serialize(rows);
            }finally{
                if(!sentinel.HasExited){sentinel.Kill(false);Require(sentinel.WaitForExit(5000));}
            }
        }
    }
}
