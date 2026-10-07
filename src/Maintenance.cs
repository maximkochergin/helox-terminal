using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Helox {
internal static class Maintenance {
    internal static void CheckStartup() {
        using(Mutex gate=new Mutex(false,"Local\\helox-maintenance")) {
            bool held=false;
            try {
                try {held=gate.WaitOne(0);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("cleanup running / wait until it finishes");
            }finally {if(held) gate.ReleaseMutex();}
        }
    }
    private static string Script(string name) {
        string path=Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..")),name);
        if(!File.Exists(path)) throw new InvalidOperationException(name+" missing / extract the complete release archive");
        return path;
    }
    private static ProcessStartInfo Start(string script,string arguments) {
        return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe"),
            "-noprofile -executionpolicy bypass -file \""+Script(script)+"\" "+arguments);
    }
    private static Dictionary<string,object> FileReport() {
        ProcessStartInfo start=Start("maintenance.ps1","-Action Check");
        start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        string output,error;
        using(Process process=Process.Start(start)) {
            output=process.StandardOutput.ReadToEnd();error=process.StandardError.ReadToEnd();process.WaitForExit();
            if(process.ExitCode!=0) throw new IOException("driver checks failed / "+error.Trim());
        }
        return Aim.Parse(output);
    }
    internal static void RequireVerifiedBackend() {
        Dictionary<string,object> report=FileReport();
        if(!BackendVerified(report)) throw new InvalidOperationException("aim backend unverified or incomplete / run aim prepare and restart helox");
    }
    private static bool BackendVerified(Dictionary<string,object> report) {
        foreach(string key in new string[]{"BackendPrepared","PackageVerified","BackendVerified"}) {
            object value;if(!report.TryGetValue(key,out value) || !Object.Equals(value,true)) return false;
        }
        return true;
    }
    internal static Dictionary<string,object> Doctor() {
        Dictionary<string,object> report=FileReport();
        AddKernelCheck(report,Aim.KernelVersion);
        report["EndpointPresent"]=Aim.EndpointState();return report;
    }
    internal static void AddKernelCheck(Dictionary<string,object> report,Func<string> readKernel) {
        // A diagnostic integrity failure must not be followed by loading that DLL.
        if(!BackendVerified(report)) {
            report["KernelReadable"]=null;report["KernelVersion"]=null;
            report["KernelError"]="backend unverified or incomplete / run aim prepare before kernel readback";return;
        }
        try {
            string version=readKernel();
            report["KernelReadable"]=true;report["KernelVersion"]=version;report["KernelError"]=null;
        } catch(Exception e) {report["KernelReadable"]=false;report["KernelVersion"]=null;report["KernelError"]=e.Message;}
    }
    internal static void ValidateResetBackups(string root,bool? endpoint,Action<Dictionary<string,object>> validateAim) {
        string original=Path.Combine(root,"original.json");
        if(File.Exists(original)) Store.Load<Settings>(original);
        string aim=Path.Combine(root,"aim-before.json");
        if(File.Exists(aim) && endpoint!=false) {
            if(endpoint==null) throw new InvalidOperationException("driver endpoint unreadable / data kept");
            validateAim(Aim.Parse(File.ReadAllText(aim)));
        }
    }
    internal static void ValidateReset() {
        bool? endpoint=Aim.EndpointState();
        ValidateResetBackups(Store.Root,endpoint,delegate(Dictionary<string,object> cfg) {
            Dictionary<string,object> report=Doctor();
            if(!Object.Equals(report["BackendVerified"],true)) throw new InvalidOperationException("aim backend unverified / data kept");
            Aim.Validate(cfg);
        });
    }
    internal static void Uninstall() {
        ProcessStartInfo start=Start("install-aim.ps1","-Uninstall");start.UseShellExecute=false;
        using(Process process=Process.Start(start)) {process.WaitForExit();if(process.ExitCode!=0) throw new IOException("driver removal failed / see message above / data kept");}
    }
    internal static void Reset() {
        // Release the loaded bridge before a fresh process deletes its files.
        ProcessStartInfo start=Start("maintenance.ps1","-Action Reset -ParentId "+Process.GetCurrentProcess().Id+" -Interactive");
        start.UseShellExecute=true;start.WindowStyle=ProcessWindowStyle.Normal;
        using(Process child=Process.Start(start)) {if(child==null) throw new IOException("could not start cleanup / nothing removed");}
        Console.WriteLine("  cleanup opened / this session is closing");Environment.Exit(0);
    }
}
}
