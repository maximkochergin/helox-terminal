using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Helox {
public sealed class StartupReport {
    public bool Enabled {get;set;}
    public bool RegistrationMatches {get;set;}
    public string DevicePath {get;set;}
    public string Helper {get;set;}
    public string Note {get;set;}
    public Dictionary<string,object> LastRun {get;set;}
}
internal static class Startup {
    internal const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run",RunName="HeloxTerminal";
    internal static readonly string PathName=Path.Combine(Store.Root,"startup.json"),LogPath=Path.Combine(Store.Root,"startup-last.json");
    internal static string HelperPath {get {return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"helox-startup.exe");}}
    internal static string Command(string helper) {return "\""+helper+"\"";}
    internal static Dictionary<string,object> Read(Dictionary<string,object> value) {
        if(value==null || value.Count!=5 || !value.ContainsKey("version") || !(value["version"] is int) || (int)value["version"]!=1) throw new ArgumentException("invalid startup settings / enable startup again");
        foreach(string key in new string[]{"devicePath","deviceId","helper"}) {
            object field;if(!value.TryGetValue(key,out field) || !(field is string) || String.IsNullOrWhiteSpace((string)field) || ((string)field).Length>4096 || ((string)field).IndexOfAny(new char[]{'\0','\r','\n','\"'})>=0) throw new ArgumentException("invalid startup settings / enable startup again");
        }
        string helper=(string)value["helper"];
        if(!Path.IsPathRooted(helper) || !String.Equals(Path.GetFileName(helper),"helox-startup.exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("invalid startup executable");
        if(!value.ContainsKey("seed")) throw new ArgumentException("missing startup calibration");
        Dictionary<string,object> seed=Aim.Map(value["seed"]);AimConfigGuard.Check(seed);
        List<object> devices=Aim.Items(seed["devices"]);
        if(devices.Count!=1 || Aim.Items(seed["profiles"]).Count!=1 || (string)Aim.Map(devices[0])["id"]!=(string)value["deviceId"] || (string)Aim.Map(devices[0])["profile"]!="") throw new ArgumentException("invalid startup mouse scope");
        return value;
    }
    private static string Registered() {using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey)) {return key==null ? null : key.GetValue(RunName) as string;}}
    private static void Register(string command) {
        using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RunKey)) {if(command==null) key.DeleteValue(RunName,false);else key.SetValue(RunName,command,RegistryValueKind.String);}
    }
    private static void RollbackRegistration(string previous,SavedFiles files,Exception error) {
        List<string> failures=new List<string>();
        try {Register(previous);}catch(Exception e) {failures.Add("registration: "+e.Message);}
        try {files.Restore();}catch(Exception e) {failures.Add("saved settings: "+e.Message);}
        throw new IOException("startup change failed: "+error.Message+(failures.Count==0 ? "; previous registration and settings restored" : "; rollback incomplete: "+String.Join(" / ",failures.ToArray())));
    }
    internal static StartupReport Status() {
        StartupReport report=new StartupReport {Note="disabled / startup on enables one hidden check at Windows sign-in"};
        try {
            string registered=Registered();report.Enabled=registered!=null;
            if(File.Exists(PathName)) {
                Dictionary<string,object> cfg=Read(Aim.Parse(File.ReadAllText(PathName)));report.DevicePath=(string)cfg["devicePath"];report.Helper=(string)cfg["helper"];
                report.RegistrationMatches=registered==Command(report.Helper) && File.Exists(report.Helper);
                report.Note=!report.Enabled ? "disabled / enable startup from the current extracted release" : report.RegistrationMatches ? "one hidden check at sign-in / cold driver only / no resident process" : "startup registration or executable changed / enable startup again";
            }else if(report.Enabled) report.Note="startup settings missing / disable or enable startup again";
        }catch(Exception e) {report.Note="startup unavailable: "+e.Message;}
        try {if(File.Exists(LogPath)) report.LastRun=Aim.Parse(File.ReadAllText(LogPath));}catch {report.Note+=" / last startup report unreadable";}
        return report;
    }
    internal static void SetOff() {
        Store.Locked(delegate {
            string before=Registered();SavedFiles files=new SavedFiles(new string[]{PathName});
            try {Register(null);File.Delete(PathName);}
            catch(Exception e) {RollbackRegistration(before,files,e);}
        });
    }
    internal static Dictionary<string,object> Seed(Dictionary<string,object> live,Dictionary<string,object> defaults,string id) {
        Dictionary<string,object> seed=Aim.Parse(Store.Json.Serialize(defaults));
        Dictionary<string,object> entry=Aim.DeviceEntry(live,id);
        if(entry==null) throw new InvalidOperationException("apply saved aim settings before enabling startup");
        entry=Aim.Parse(Store.Json.Serialize(entry));entry["profile"]="";seed["devices"]=new object[]{entry};return seed;
    }
    internal static Dictionary<string,object> Target(Dictionary<string,object> defaults,Dictionary<string,object> config,Dictionary<string,object> saved) {
        config=Read(config);CheckSeed(defaults,config);string id=(string)config["deviceId"];object controls;
        if(!saved.TryGetValue(id,out controls)) throw new InvalidOperationException("no saved aim controls for startup mouse / apply a curve or recipe first");
        AimPreset preset=Aim.ReadPreset(Aim.Map(controls));Aim.CheckUnit(Aim.Describe(Aim.Map(config["seed"]),id),preset.SpeedUnit);
        return Aim.Canonical(Aim.ConfigurePreset(Aim.Map(config["seed"]),defaults,id,preset,true));
    }
    internal static void CheckSeed(Dictionary<string,object> defaults,Dictionary<string,object> config) {
        Dictionary<string,object> seed=Aim.Map(config["seed"]);
        foreach(string key in new string[]{"defaultDeviceConfig","profiles"}) if(!Aim.SameValue(seed[key],defaults[key])) throw new InvalidOperationException("startup baseline changed / enable startup again");
    }
    internal static string Policy(Dictionary<string,object> live,Dictionary<string,object> defaults,Dictionary<string,object> target,string id) {
        if(GamePresets.SameSelected(live,target,id)) return "already matched";
        foreach(string key in new string[]{"defaultDeviceConfig","profiles","devices"}) if(!Aim.SameValue(live[key],defaults[key])) return "external settings or bypass retained";
        return "restore cold driver";
    }
    internal static void SetOn(Device device) {
        Store.Locked(delegate {Aim.Locked(delegate {
            LiveVerify.RequireNoRecovery();if(!File.Exists(HelperPath)) throw new InvalidOperationException("startup helper missing / rebuild or extract complete 0.20.0 release");
            Dictionary<string,object> live=Aim.Active(),defaults=Aim.Defaults();string id=Aim.Id(device);
            Dictionary<string,object> config=new Dictionary<string,object>{{"version",1},{"devicePath",device.Path},{"deviceId",id},{"helper",HelperPath},{"seed",Seed(live,defaults,id)}};
            Dictionary<string,object> target=Target(defaults,config,Aim.Saved(Path.Combine(Store.Root,"aim-presets.json")));
            if(!GamePresets.SameSelected(live,target,id) || Aim.Describe(live,id).Enabled!=true) throw new InvalidOperationException("live aim differs from saved controls / resume saved or apply a recipe before enabling startup");
            GamePresets.RequireStartedStack(DeviceStack.Read(device));
            string previous=Registered();SavedFiles files=new SavedFiles(new string[]{PathName});
            try {Store.Save(PathName,config);Register(Command(HelperPath));}
            catch(Exception e) {RollbackRegistration(previous,files,e);}
            return true;
        });});
    }
    internal static Dictionary<string,object> Run() {
        Dictionary<string,object> result=new Dictionary<string,object>{{"checkedUtc",DateTime.UtcNow.ToString("o")},{"applied",false},{"state","skipped"},{"note",null}};
        Store.Locked(delegate {try {
            Aim.Locked(delegate {
                LiveVerify.RequireNoRecovery();Dictionary<string,object> config=Read(Aim.Parse(File.ReadAllText(PathName)));
                if(Registered()!=Command((string)config["helper"]) || !String.Equals((string)config["helper"],HelperPath,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("startup disabled or release path changed / enable startup again");
                Device device=Device.List().Find(delegate(Device d){return String.Equals(d.Path,(string)config["devicePath"],StringComparison.OrdinalIgnoreCase);});
                if(device==null || Aim.Id(device)!=(string)config["deviceId"]) throw new InvalidOperationException("startup mouse disconnected / reconnect and resume saved");
                GameSessionReport session=GameSession.Read();if(!session.QuerySucceeded || session.RunningGames.Length>0) throw new InvalidOperationException("game running or game query unavailable / resume saved with games closed");
                Dictionary<string,object> defaults=Aim.Defaults(),before=Aim.Active(),target=Target(defaults,config,Aim.Saved(Path.Combine(Store.Root,"aim-presets.json")));
                string policy=Policy(before,defaults,target,(string)config["deviceId"]);result["note"]=policy;
                if(policy=="already matched") result["state"]="matched";
                else if(policy=="restore cold driver") {
                    GamePresets.RequireStartedStack(DeviceStack.Read(device));LiveVerify.RequireClosedGames();
                    // Durable driver transaction, with no desktop or saved-control writes.
                    Aim.CommitDurable(before,target,delegate {});
                    result["applied"]=true;result["state"]="restored";
                }
                return true;
            });
        }catch(Exception e) {result["note"]=e.Message;}
        Store.Save(LogPath,result);});return result;
    }
}
internal static class StartupEntry {
    [STAThread] private static int Main() {try {Maintenance.CheckStartup();Startup.Run();return 0;}catch {return 1;}}
}
}
