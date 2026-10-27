using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

// JOB_LIST makes Job membership atomic with process creation, closing the
// create-suspended/assign parent-crash window. HANDLE_LIST restricts inheritance.
internal sealed class OwnedLauncherChild : IDisposable {
    const uint Suspended=0x4, NoWindow=0x08000000, UnicodeEnvironment=0x400, ExtendedStartup=0x80000;
    const uint KillOnClose=0x2000, WaitObject=0, WaitTimeout=258;
    [StructLayout(LayoutKind.Sequential)] struct SecurityAttributes { public int Size; public IntPtr Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)] struct StartupInfo {
        public int Size; public IntPtr Reserved,Desktop,Title;
        public uint X,Y,XSize,YSize,XChars,YChars,Fill,Flags;
        public ushort Show,ReservedSize; public IntPtr ReservedBytes,Input,Output,Error;
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupInfoEx { public StartupInfo Startup; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInformation { public IntPtr Process,Thread; public uint ProcessId,ThreadId; }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long ProcessTime,JobTime; public uint Flags;
        public UIntPtr MinimumWorkingSet,MaximumWorkingSet; public uint ActiveProcessLimit;
        public UIntPtr Affinity; public uint Priority,Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {
        public BasicLimits Basic; public IoCounters Io;
        public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] struct Accounting {
        public long UserTime,KernelTime,PeriodUserTime,PeriodKernelTime;
        public uint PageFaults,TotalProcesses,ActiveProcesses,TerminatedProcesses;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateJobObjectW(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref ExtendedLimits value,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool QueryInformationJobObject(SafeFileHandle job,int kind,out Accounting value,uint size,IntPtr returned);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool IsProcessInJob(SafeFileHandle process,SafeFileHandle job,out bool present);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateJobObject(SafeFileHandle job,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CreatePipe(out SafeFileHandle read,out SafeFileHandle write,ref SecurityAttributes attributes,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetHandleInformation(SafeFileHandle handle,uint mask,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,uint flags,ref IntPtr size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
    [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateProcessW(string application,StringBuilder command,
        IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,
        ref StartupInfoEx startup,out ProcessInformation information);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint ResumeThread(SafeFileHandle thread);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool TerminateProcess(SafeFileHandle process,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(SafeFileHandle handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetExitCodeProcess(SafeFileHandle process,out uint code);
    SafeFileHandle job,process;
    public FileStream Input {get; private set;}
    public FileStream Output {get; private set;}
    public FileStream Error {get; private set;}
    bool stopped,disposed;
    static Win32Exception NativeFailure() { return new Win32Exception(Marshal.GetLastWin32Error()); }
    static string Quote(string value) {
        if(value==null || value.IndexOf('\0')>=0) throw new SecurityException();
        var result=new StringBuilder("\""); int slashes=0;
        foreach(char c in value) {
            if(c=='\\') {slashes++;continue;}
            if(c=='\"') {result.Append('\\',slashes*2+1);result.Append(c);slashes=0;continue;}
            result.Append('\\',slashes);slashes=0;result.Append(c);
        }
        result.Append('\\',slashes*2);return result.Append('"').ToString();
    }
    static void Pipe(out SafeFileHandle read,out SafeFileHandle write,bool parentReads) {
        var attributes=new SecurityAttributes {Size=Marshal.SizeOf<SecurityAttributes>(),Inherit=1};
        if(!CreatePipe(out read,out write,ref attributes,0)) throw NativeFailure();
        if(!SetHandleInformation(parentReads?read:write,1,0)) {
            var failure=NativeFailure();read.Dispose();write.Dispose();throw failure;
        }
    }
    internal static OwnedLauncherChild Start(ProcessStartInfo start) {
        var ownedJob=CreateJobObjectW(IntPtr.Zero,null);
        if(ownedJob.IsInvalid) {var failure=NativeFailure();ownedJob.Dispose();throw failure;}
        try {
            var limits=new ExtendedLimits();limits.Basic.Flags=KillOnClose;
            if(!SetInformationJobObject(ownedJob,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>())) throw NativeFailure();
            uint ignored;
            return StartInJob(start,ownedJob,out ignored);
        } catch {ownedJob.Dispose();throw;}
    }
    // Shared launch path; invalid Job fixtures must fail before process creation.
    // Successful return transfers ownership of the supplied private job to the child lifetime object.
    internal static OwnedLauncherChild StartInJob(ProcessStartInfo start,SafeFileHandle ownedJob,out uint processId,
        Action<uint> beforeResume=null) {
        processId=0;
        if(start==null || start.UseShellExecute || !start.RedirectStandardOutput || !start.RedirectStandardError ||
            !LauncherFiles.IsLocalAbsolutePath(start.FileName) || !String.IsNullOrEmpty(start.Arguments) ||
            !String.IsNullOrEmpty(start.UserName)) throw new SecurityException();
        SafeFileHandle inRead=null,inWrite=null,outRead=null,outWrite=null,errRead=null,errWrite=null,thread=null;
        var child=new OwnedLauncherChild();bool assigned=false,attributesReady=false;
        IntPtr attributes=IntPtr.Zero,handles=IntPtr.Zero,jobs=IntPtr.Zero,environment=IntPtr.Zero;
        char[] environmentChars=null;
        try {
            Pipe(out inRead,out inWrite,false);Pipe(out outRead,out outWrite,true);Pipe(out errRead,out errWrite,true);
            IntPtr size=IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero,2,0,ref size);
            if(size==IntPtr.Zero) throw NativeFailure();
            attributes=Marshal.AllocHGlobal(size);
            if(!InitializeProcThreadAttributeList(attributes,2,0,ref size)) throw NativeFailure();
            attributesReady=true;handles=Marshal.AllocHGlobal(IntPtr.Size*3);
            Marshal.WriteIntPtr(handles,0,inRead.DangerousGetHandle());
            Marshal.WriteIntPtr(handles,IntPtr.Size,outWrite.DangerousGetHandle());
            Marshal.WriteIntPtr(handles,IntPtr.Size*2,errWrite.DangerousGetHandle());
            // Only these three pipe ends may be inherited; job/process/parent ends are non-inheritable.
            if(!UpdateProcThreadAttribute(attributes,0,(IntPtr)0x20002,handles,(IntPtr)(IntPtr.Size*3),IntPtr.Zero,IntPtr.Zero)) throw NativeFailure();
            jobs=Marshal.AllocHGlobal(IntPtr.Size);Marshal.WriteIntPtr(jobs,ownedJob.DangerousGetHandle());
            if(!UpdateProcThreadAttribute(attributes,0,(IntPtr)0x2000D,jobs,(IntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero))throw NativeFailure();
            var entries=new List<string>();
            foreach(var pair in start.Environment) {
                if(pair.Key.IndexOf('\0')>=0 || pair.Value==null || pair.Value.IndexOf('\0')>=0) throw new SecurityException();
                entries.Add(pair.Key+"="+pair.Value);
            }
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            environmentChars=(String.Join("\0",entries)+"\0\0").ToCharArray();
            environment=Marshal.AllocHGlobal(environmentChars.Length*2);
            Marshal.Copy(environmentChars,0,environment,environmentChars.Length);
            var command=new StringBuilder(Quote(start.FileName));
            foreach(var argument in start.ArgumentList) command.Append(' ').Append(Quote(argument));
            var startup=new StartupInfoEx();startup.Startup.Size=Marshal.SizeOf<StartupInfoEx>();
            startup.Startup.Flags=0x100;startup.Startup.Input=inRead.DangerousGetHandle();
            startup.Startup.Output=outWrite.DangerousGetHandle();startup.Startup.Error=errWrite.DangerousGetHandle();startup.Attributes=attributes;
            ProcessInformation information;
            if(!CreateProcessW(start.FileName,command,IntPtr.Zero,IntPtr.Zero,true,
                Suspended|NoWindow|UnicodeEnvironment|ExtendedStartup,environment,
                String.IsNullOrEmpty(start.WorkingDirectory)?null:start.WorkingDirectory,ref startup,out information)) throw NativeFailure();
            child.process=new SafeFileHandle(information.Process,true);thread=new SafeFileHandle(information.Thread,true);
            processId=information.ProcessId;
            assigned=true;child.job=ownedJob;
            bool present;
            if(!IsProcessInJob(child.process,ownedJob,out present))throw NativeFailure();
            if(!present)throw new SecurityException();
            if(beforeResume!=null)beforeResume(processId); // Synthetic abrupt-owner hook only.
            // Streams and ownership are ready before any user code, stdin writer, or descendant can run.
            // Disable FileStream's extra managed write buffer for the secret-bearing stdin pipe.
            child.Input=new FileStream(inWrite,FileAccess.Write,1,false);inWrite=null;
            child.Output=new FileStream(outRead,FileAccess.Read,4096,false);outRead=null;
            child.Error=new FileStream(errRead,FileAccess.Read,4096,false);errRead=null;
            uint previous=ResumeThread(thread);
            if(previous==UInt32.MaxValue) throw NativeFailure();
            if(previous!=1) throw new SecurityException();
            return child;
        } catch {
            // Before assignment only this still-suspended direct child is terminated by its retained handle.
            // After assignment job termination also owns every descendant, including an already exited parent's.
            try {
                if(assigned) child.StopOwned();
                else if(child.process!=null) {
                    if(!TerminateProcess(child.process,1)) throw NativeFailure();
                    if(WaitForSingleObject(child.process,5000)!=WaitObject) throw new IOException();
                }
            } finally {child.CloseHandles();}
            throw;
        } finally {
            foreach(var handle in new[]{inRead,inWrite,outRead,outWrite,errRead,errWrite,thread}) if(handle!=null)handle.Dispose();
            if(attributesReady)DeleteProcThreadAttributeList(attributes);
            if(attributes!=IntPtr.Zero)Marshal.FreeHGlobal(attributes);
            if(handles!=IntPtr.Zero)Marshal.FreeHGlobal(handles);
            if(jobs!=IntPtr.Zero)Marshal.FreeHGlobal(jobs);
            if(environment!=IntPtr.Zero) {
                for(int i=0;i<environmentChars.Length*2;i++)Marshal.WriteByte(environment,i,0);
                Marshal.FreeHGlobal(environment);
            }
            if(environmentChars!=null)Array.Clear(environmentChars,0,environmentChars.Length);
        }
    }
    public bool WaitForExit(int milliseconds) {
        uint result=WaitForSingleObject(process,(uint)milliseconds);
        if(result==WaitObject)return true;
        if(result==WaitTimeout)return false;
        throw NativeFailure();
    }
    public int ExitCode { get {uint value;if(!GetExitCodeProcess(process,out value))throw NativeFailure();return unchecked((int)value);} }
    internal void StopOwned() {
        if(stopped)return;
        if(!TerminateJobObject(job,1))throw NativeFailure();
        var timer=Stopwatch.StartNew();
        for(;;) {
            Accounting state;
            if(!QueryInformationJobObject(job,1,out state,(uint)Marshal.SizeOf<Accounting>(),IntPtr.Zero))throw NativeFailure();
            if(state.ActiveProcesses==0)break;
            if(timer.ElapsedMilliseconds>=5000)throw new IOException();
            Thread.Sleep(10);
        }
        // Job accounting can reach zero just before the process handle becomes signaled.
        // Confirm both observations within the same cleanup deadline rather than a zero-time race.
        if(!WaitForExit((int)Math.Max(0,5000-timer.ElapsedMilliseconds)))throw new IOException();
        stopped=true;
    }
    void CloseHandles() {
        // Closing the final job handle is a kill-on-close fallback if explicit cleanup failed.
        try {if(job!=null)job.Dispose();}
        finally {try {if(Input!=null)Input.Dispose();}
            finally {try {if(Output!=null)Output.Dispose();}
                finally {try {if(Error!=null)Error.Dispose();} finally {if(process!=null)process.Dispose();}}}}
    }
    public void Dispose() {
        if(disposed)return;disposed=true;
        try {StopOwned();} finally {CloseHandles();}
    }
}
