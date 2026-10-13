// Explicit local integration: reversible native writes and HKCU startup registration.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using Helox;

internal static class FinalLiveProbe {
    private static readonly Assembly App=typeof(Settings).Assembly;
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static,Instance=BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type Type(string name) {return App.GetType("Helox."+name,true);}
    private static object Call(string type,string name,params object[] args) {try {return Type(type).GetMethod(name,Static).Invoke(null,args);}catch(TargetInvocationException e) {throw e.InnerException;}}
    private static bool Same(object a,object b) {return (bool)Call("Aim","SameValue",a,b);}
    private static Dictionary<string,object> Map(object value) {return (Dictionary<string,object>)value;}
    private static void Expect(bool value,string message) {if(!value) throw new IOException(message);}
    private static string Root {get {return (string)Type("Store").GetField("Root",Static).GetValue(null);}}
    private static string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private static Dictionary<string,object> Command(string args,bool failure=false) {
        ProcessStartInfo start=new ProcessStartInfo(App.Location,args+" --json") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(Process p=Process.Start(start)) {
            string output=p.StandardOutput.ReadToEnd(),error=p.StandardError.ReadToEnd();p.WaitForExit();
            Expect(p.ExitCode==(failure ? 1 : 0),args+" / "+output+error);return Json.Deserialize<Dictionary<string,object>>(output);
        }
    }
    private static Dictionary<string,object> Hidden() {
        using(Process p=Process.Start(new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(App.Location),"helox-startup.exe")) {UseShellExecute=false,CreateNoWindow=true})) {Expect(p.WaitForExit(30000) && p.ExitCode==0,"hidden startup helper failed");}
        return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(Root,"startup-last.json")));
    }
    private static void ChildDesktop() {
        Settings before=Settings.Read(),after=Json.Deserialize<Settings>(Json.Serialize(before));after.Speed=before.Speed==10 ? 11 : 10;
        string undo=Path.Combine(Root,"undo.json"),checkpoint=Path.Combine(Root,"windows-recovery.json");
        object snapshot=Call("WindowsRecovery","Snapshot",before,File.Exists(undo) ? File.ReadAllBytes(undo) : null);
        Call("WindowsRecovery","Commit",before,after,new Action<Settings>(delegate(Settings value) {value.Apply();Environment.Exit(23);}),new Action<Settings>(delegate(Settings value) {Call("Store","Save",undo,value);}),
            new Action(delegate {Call("Store","Save",checkpoint,snapshot);}),new Action(delegate {}),new Action(delegate {File.Delete(checkpoint);}));
        throw new IOException("desktop interruption was not reached");
    }
    [STAThread] private static int Main(string[] args) {
        try {if(args.Length==1 && args[0]=="child-desktop") ChildDesktop();else Run();return 0;}catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
    private static void Run() {
        Call("LiveVerify","RequireClosedGames");Call("LiveVerify","RequireNoRecovery");
        string evidencePath=Path.Combine(Root,"final-live-evidence.json");Expect(!File.Exists(evidencePath),"previous final live evidence remains / restore that baseline before another test");
        string[] names={"startup.json","startup-last.json","selected-mouse.json","original.json","undo.json","aim-before.json","aim-presets.json","game-undo.json","game-recovery.json","windows-recovery.json"};
        string[] paths=new string[names.Length];for(int i=0;i<names.Length;i++) paths[i]=Path.Combine(Root,names[i]);
        object files=Type("SavedFiles").GetConstructor(Instance,null,new Type[]{typeof(string[])},null).Invoke(new object[]{paths});
        Dictionary<string,object> original=Map(Call("Aim","Active"));Settings windows=Settings.Read();Exception failure=null;
        object registered=null;RegistryValueKind kind=RegistryValueKind.String;
        using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RunKey)) {if(key!=null) {registered=key.GetValue("HeloxTerminal");if(registered!=null) kind=key.GetValueKind("HeloxTerminal");}}
        Dictionary<string,object> evidenceFiles=new Dictionary<string,object>();
        foreach(string path in paths) {byte[] bytes=(byte[])Type("SavedFiles").GetMethod("Bytes",Instance).Invoke(files,new object[]{path});evidenceFiles[Path.GetFileName(path)]=bytes==null ? null : Convert.ToBase64String(bytes);}
        Call("Store","Save",evidencePath,new Dictionary<string,object>{{"driver",original},{"windows",windows},{"files",evidenceFiles},{"startupRegistry",registered},{"startupRegistryKind",kind.ToString()}});
        Dictionary<string,object> report=new Dictionary<string,object>();int startupCases=0;
        try {
            Expect((string)Command("version")["version"]=="0.20.0","version contract failed");
            Console.WriteLine("final live / forced desktop exit, mutation guards and read-only exact undo recovery");
            string undoPath=Path.Combine(Root,"undo.json"),checkpoint=Path.Combine(Root,"windows-recovery.json");
            if(!File.Exists(undoPath)) File.WriteAllText(undoPath,Json.Serialize(windows));
            byte[] undoBytes=File.ReadAllBytes(undoPath);FileAttributes undoAttributes=File.GetAttributes(undoPath);
            try {
                File.SetAttributes(undoPath,undoAttributes|FileAttributes.ReadOnly);
                using(Process child=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"child-desktop") {UseShellExecute=false,CreateNoWindow=true})) {Expect(child.WaitForExit(30000) && child.ExitCode==23,"desktop child did not stop after mutation");}
                Expect(File.Exists(checkpoint) && !windows.Same(Settings.Read()),"desktop interruption did not retain changed state + checkpoint");
                Expect(((string)Command("set speed 10",true)["error"]).Contains("recover first"),"new desktop change was not guarded");
                Expect(((string)Command("aim bypass on",true)["error"]).Contains("recover first"),"new driver change was not guarded");
                Command("recover");Expect(windows.Same(Settings.Read()) && !File.Exists(checkpoint) && Convert.ToBase64String(File.ReadAllBytes(undoPath))==Convert.ToBase64String(undoBytes),"desktop recovery did not restore exact Windows + read-only undo");
            }finally {File.SetAttributes(undoPath,undoAttributes);}
            report["DesktopForcedExitRecovered"]=true;
            Command("preset apply valorant-steady");Device device=(Device)Call("Program","Selected");string id=(string)Call("Aim","Id",device);
            Console.WriteLine("final live / startup registration, hidden match and cold-driver restoration");
            object recipe=Call("Aim","Active");Settings recipeWindows=Settings.Read();
            Dictionary<string,object> enabled=Command("startup on");Expect((bool)enabled["Enabled"] && (bool)enabled["RegistrationMatches"] && Same(recipe,Call("Aim","Active")),"startup enable changed driver or failed registration");
            string readonlyConfig=Path.Combine(Root,"startup.json");FileAttributes configAttributes=File.GetAttributes(readonlyConfig);byte[] configBefore=File.ReadAllBytes(readonlyConfig);
            try {File.SetAttributes(readonlyConfig,configAttributes|FileAttributes.ReadOnly);Command("startup off",true);Expect((bool)Command("startup status")["RegistrationMatches"] && Convert.ToBase64String(File.ReadAllBytes(readonlyConfig))==Convert.ToBase64String(configBefore) && Same(recipe,Call("Aim","Active")),"failed startup off did not restore registration and exact configuration");startupCases++;}
            finally {File.SetAttributes(readonlyConfig,configAttributes);}
            Expect((string)Hidden()["state"]=="matched" && Same(recipe,Call("Aim","Active")),"hidden startup matched case changed driver");startupCases++;
            Call("Aim","Write",Call("Aim","Defaults"));Expect((string)Hidden()["state"]=="restored" && Same(recipe,Call("Aim","Active")) && recipeWindows.Same(Settings.Read()),"hidden startup did not restore cold driver without desktop changes");startupCases++;
            Console.WriteLine("final live / calibrated device and latest saved controls after cold reset");
            Dictionary<string,object> normalized=Map(Call("Aim","Parse",Json.Serialize(recipe))),entry=Map(Call("Aim","DeviceEntry",normalized,id)),cfg=Map(entry["config"]);
            cfg["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;cfg["Polling rate Hz (keep at 0 for automatic adjustment)"]=1000;cfg["Use constant time interval based on polling rate"]=true;
            Call("Aim","Write",normalized);Dictionary<string,object> prefs=Map(Call("Aim","Saved",Path.Combine(Root,"aim-presets.json")));Map(prefs[id])["speedUnit"]="in/s";Call("Store","Save",Path.Combine(Root,"aim-presets.json"),prefs);
            Command("startup on");object calibrated=Call("Aim","Active");Call("Aim","Write",Call("Aim","Defaults"));
            Expect((string)Hidden()["state"]=="restored" && (bool)Call("GamePresets","SameSelected",Call("Aim","Active"),calibrated,id),"startup lost captured DPI or constant device timing");startupCases++;
            Command("aim snap on 1");object latest=Call("Aim","Active");Call("Aim","Write",Call("Aim","Defaults"));
            Expect((string)Command("startup run")["state"]=="restored" && (bool)Call("GamePresets","SameSelected",Call("Aim","Active"),latest,id),"startup ignored latest saved controls");startupCases++;
            Console.WriteLine("final live / startup preserves bypass, external profiles, pending recovery and disconnects");
            Command("aim bypass on");object bypass=Call("Aim","Active");Expect((string)Hidden()["state"]=="skipped" && Same(bypass,Call("Aim","Active")),"startup overwrote bypass");startupCases++;
            Dictionary<string,object> external=Map(Call("Aim","Defaults"));Map(((object[])external["profiles"])[0])["Output DPI"]=1100.0;Call("Aim","Write",external);object foreign=Call("Aim","Active");
            Expect((string)Hidden()["state"]=="skipped" && Same(foreign,Call("Aim","Active")),"startup overwrote external default profile");startupCases++;
            Call("Store","Save",checkpoint,Call("WindowsRecovery","Snapshot",Settings.Read(),File.ReadAllBytes(undoPath)));
            Expect((string)Hidden()["state"]=="skipped" && Same(foreign,Call("Aim","Active")),"startup bypassed pending recovery");startupCases++;Command("recover");
            string configPath=Path.Combine(Root,"startup.json");byte[] configBytes=File.ReadAllBytes(configPath);Dictionary<string,object> startupConfig=Map(Call("Aim","Parse",File.ReadAllText(configPath)));startupConfig["devicePath"]="disconnected-test-mouse";Call("Store","Save",configPath,startupConfig);
            Expect((string)Hidden()["state"]=="skipped" && Same(foreign,Call("Aim","Active")),"startup silently selected another mouse");startupCases++;File.WriteAllBytes(configPath,configBytes);
            Command("startup off");Expect(!(bool)Command("startup status")["Enabled"] && (string)Hidden()["state"]=="skipped" && Same(foreign,Call("Aim","Active")),"startup off did not disable automatic changes");startupCases++;
            report["StartupCases"]=startupCases;report["ActualWindowsSignInTested"]=false;report["GameInputVerified"]=null;
        }catch(Exception e) {failure=e;}
        finally {
            List<string> restoreErrors=new List<string>();
            bool driverRestored=false,windowsRestored=false;
            try {Call("Aim","Write",original);Expect(Same(original,Call("Aim","Active")),"original driver restore mismatch");driverRestored=true;}catch(Exception e) {restoreErrors.Add("driver: "+e.Message);}
            try {if(!windows.Same(Settings.Read())) windows.Apply();Expect(windows.Same(Settings.Read()),"original Windows restore mismatch");windowsRestored=true;}catch(Exception e) {restoreErrors.Add("windows: "+e.Message);}
            // Never clear interruption evidence unless both native components are verified.
            if(driverRestored && windowsRestored) try {FilesRestore(files);}catch(Exception e) {restoreErrors.Add("saved files: "+e.Message);}
            try {using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RunKey)) {if(registered==null || restoreErrors.Count>0) key.DeleteValue("HeloxTerminal",false);else key.SetValue("HeloxTerminal",registered,kind);}}catch(Exception e) {restoreErrors.Add("startup registry: "+e.Message);}
            if(restoreErrors.Count==0) File.Delete(evidencePath);
            else restoreErrors.Add("baseline and saved bytes retained: "+evidencePath);
            if(restoreErrors.Count>0) failure=new IOException((failure==null ? "" : failure.Message+" / ")+String.Join(" / ",restoreErrors.ToArray()));
        }
        if(failure!=null) throw failure;
        Expect(windows.Same(Settings.Read()) && Same(original,Call("Aim","Active")),"final state differs from baseline");report["FullStateRestored"]=true;
        Console.WriteLine(Json.Serialize(report));
    }
    private static void FilesRestore(object files) {try {Type("SavedFiles").GetMethod("Restore",Instance).Invoke(files,new object[0]);}catch(TargetInvocationException e) {throw e.InnerException;}}
}
