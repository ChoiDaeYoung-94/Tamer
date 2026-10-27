using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class AtomicOwnerFixture {
    [StructLayout(LayoutKind.Sequential)] struct Basic {
        public long ProcessTime,JobTime;public uint Flags;public UIntPtr Minimum,Maximum;
        public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]struct Io {public ulong A,B,C,D,E,F;}
    [StructLayout(LayoutKind.Sequential)]struct Limits {public Basic Basic;public Io Io;public UIntPtr A,B,C,D;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateJobObjectW(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref Limits value,uint size);
    internal static void DieBeforeResume(ProcessStartInfo start,string marker) {
        var job=CreateJobObjectW(IntPtr.Zero,null);if(job.IsInvalid)throw new IOException();
        var limits=new Limits();limits.Basic.Flags=0x2000;
        if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<Limits>()))throw new IOException();
        uint pid;
        OwnedLauncherChild.StartInJob(start,job,out pid,child=>{
            File.WriteAllText(marker+".pid",child.ToString());Environment.Exit(32);
        });
        throw new IOException();
    }
}
