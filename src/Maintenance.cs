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
    internal static Dictionary<string,object> Doctor() {
        ProcessStartInfo start=Start("maintenance.ps1","-Action Check");
        start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
        string output,error;
        using(Process process=Process.Start(start)) {
            output=process.StandardOutput.ReadToEnd();error=process.StandardError.ReadToEnd();process.WaitForExit();
            if(process.ExitCode!=0) throw new IOException("driver checks failed / "+error.Trim());
        }
        Dictionary<string,object> report=Aim.Parse(output);
        try {
            string version=Aim.KernelVersion();
            report["KernelReadable"]=true;report["KernelVersion"]=version;report["KernelError"]=null;
        } catch(Exception e) {report["KernelReadable"]=false;report["KernelVersion"]=null;report["KernelError"]=e.Message;}
        report["EndpointPresent"]=Aim.EndpointState();return report;
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
