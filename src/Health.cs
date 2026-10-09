using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Helox {
public sealed class ServiceReading {
    public string Name {get;set;}
    public bool? Installed {get;set;}
    public string State {get;set;}
    public int? ErrorCode {get;set;}
}
internal static class ServiceProbe {
    [StructLayout(LayoutKind.Sequential)] private struct Status {
        internal uint Type,State,Controls,Win32Exit,SpecificExit,Checkpoint,WaitHint;
    }
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr OpenSCManager(string machine,string database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr OpenService(IntPtr manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool QueryServiceStatus(IntPtr service,out Status status);
    [DllImport("advapi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseServiceHandle(IntPtr handle);
    internal static string State(uint state) {
        string[] names={"unknown","stopped","start pending","stop pending","running","continue pending","pause pending","paused"};
        return state<names.Length ? names[state] : "unknown";
    }
    internal static ServiceReading Failure(string name,int error,bool opened) {
        return new ServiceReading {Name=name,Installed=opened ? (bool?)true : error==1060 ? (bool?)false : null,State=error==1060 && !opened ? "not installed" : "unknown",ErrorCode=error==1060 && !opened ? null : (int?)error};
    }
    internal static ServiceReading Read(string name) {
        IntPtr manager=IntPtr.Zero,service=IntPtr.Zero;
        try {
            manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero) return Failure(name,Marshal.GetLastWin32Error(),false);
            service=OpenService(manager,name,4);if(service==IntPtr.Zero) return Failure(name,Marshal.GetLastWin32Error(),false);
            Status status;if(!QueryServiceStatus(service,out status)) return Failure(name,Marshal.GetLastWin32Error(),true);
            return new ServiceReading {Name=name,Installed=true,State=State(status.State)};
        }finally {if(service!=IntPtr.Zero) CloseServiceHandle(service);if(manager!=IntPtr.Zero) CloseServiceHandle(manager);}
    }
}
public sealed class HealthReport {
    public string Version {get;set;}
    public string CheckedUtc {get;set;}
    public Device Mouse {get;set;}
    public Settings Windows {get;set;}
    public AimStatus Aim {get;set;}
    public Dictionary<string,object> Driver {get;set;}
    public DeviceStackReport Stack {get;set;}
    public GamePresetStatus Presets {get;set;}
    public ServiceReading[] Vanguard {get;set;}
    public string[] Notes {get;set;}
    public string[] Errors {get;set;}
    public bool? GameInputVerified {get;set;}
    public string Source {get;set;}
}
internal static class Health {
    internal static HealthReport Read(Device device,string selectionNote) {
        List<string> notes=new List<string>(),errors=new List<string>();
        HealthReport report=new HealthReport {Version=Program.Version,CheckedUtc=DateTime.UtcNow.ToString("o"),Mouse=device,
            Vanguard=new ServiceReading[]{ServiceProbe.Read("vgk"),ServiceProbe.Read("vgc")},
            Source="read-only configuration + device stack + service control manager / no game launch or input injection"};
        if(selectionNote!=null) notes.Add(selectionNote);
        try {report.Windows=Settings.Read();}catch(Exception e) {errors.Add("windows: "+e.Message);}
        try {report.Driver=Maintenance.Doctor();}catch(Exception e) {errors.Add("driver: "+e.Message);}
        report.Stack=DeviceStack.Read(device);
        if(report.Stack.Error!=null) notes.Add("mouse stack: "+report.Stack.Error);
        if(report.Driver!=null) {
            object failure;
            if(report.Driver.TryGetValue("KernelError",out failure) && failure!=null) notes.Add("driver: "+failure);
            if(report.Driver.TryGetValue("Errors",out failure) && failure is System.Collections.IEnumerable && !(failure is string))
                foreach(object error in (System.Collections.IEnumerable)failure) if(error!=null) errors.Add("driver: "+error);
        }
        report.Aim=report.Driver!=null && Maintenance.BackendVerified(report.Driver) ? Aim.Read(device) :
            new AimStatus {State="unavailable",Note="backend verification not confirmed / aim doctor"};
        if(report.Aim.State=="ready") {
            try {report.Presets=GamePresets.Status(device);}catch(Exception e) {errors.Add("preset check: "+e.Message);}
            if(report.Aim.Enabled!=true) notes.Add("aim effects bypassed / apply a game recipe or tune mouse > driver > bypass");
            if(report.Aim.OutputHalfLifeMs>0) notes.Add("output averaging active / tune mouse > motion filters > remove flick tail");
            if(report.Aim.LookupInputSmoothingRisk==true) notes.Add("legacy curve speed smoothing / reapply curve or preset");
            if(report.Stack.RawAccelPresent!=true || report.Stack.Started!=true || report.Stack.ProblemCode!=0) notes.Add("selected mouse's started raw accel stack is not confirmed / aim doctor");
        }else notes.Add(report.Aim.Note);
        if(GameRecovery.Pending) notes.Add("game recovery pending / close games, then preset recover");
        if(File.Exists(LiveVerify.RecoveryPath) || Directory.Exists(LiveVerify.RecoveryPath)) notes.Add("live verification recovery pending / aim verify restore");
        notes.Add("game input and vanguard acceptance remain untested / services are not started by this check");
        report.Notes=notes.ToArray();report.Errors=errors.ToArray();return report;
    }
}
}
