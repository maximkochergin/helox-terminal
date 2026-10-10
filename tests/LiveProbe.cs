using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Helox;

internal static class LiveProbe {
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    private static readonly Assembly App=typeof(Settings).Assembly;
    private static readonly Type Aim=App.GetType("Helox.Aim",true),Files=App.GetType("Helox.SavedFiles",true);
    private const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static,Instance=BindingFlags.NonPublic|BindingFlags.Instance;
    private static object Call(Type type,string name,params object[] args) {
        try {return type.GetMethod(name,Static).Invoke(null,args);}catch(TargetInvocationException e) {throw e.InnerException;}
    }
    private static bool Same(object a,object b) {return (bool)Call(Aim,"SameValue",a,b);}
    private static Dictionary<string,object> Map(object value) {return (Dictionary<string,object>)value;}
    private static void Expect(bool result,string message) {if(!result) throw new IOException(message);}
    private static Dictionary<string,object> Command(string args,bool failure=false) {
        ProcessStartInfo info=new ProcessStartInfo(App.Location,args+" --json") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(Process p=Process.Start(info)) {
            string output=p.StandardOutput.ReadToEnd(),error=p.StandardError.ReadToEnd();p.WaitForExit();
            Dictionary<string,object> result=Json.Deserialize<Dictionary<string,object>>(output);
            Expect(p.ExitCode==(failure ? 1 : 0),args+" failed / "+output+error);return result;
        }
    }
    [STAThread] private static int Main() {
        try {Run();return 0;}catch(Exception e) {Console.Error.WriteLine(e.Message);return 1;}
    }
    private static void Run() {
        Call(App.GetType("Helox.LiveVerify",true),"RequireClosedGames");
        string root=(string)App.GetType("Helox.Store",true).GetField("Root",Static).GetValue(null);
        Expect(!File.Exists(Path.Combine(root,"verify-recovery.json")) && !File.Exists(Path.Combine(root,"game-recovery.json")),"restore unfinished recovery first");
        string[] paths={"aim-presets.json","game-undo.json","undo.json","original.json","aim-before.json","verify-recovery.json","game-recovery.json","game-tuning.json"};
        for(int i=0;i<paths.Length;i++) paths[i]=Path.Combine(root,paths[i]);
        object files=Files.GetConstructor(Instance,null,new Type[]{typeof(string[])},null).Invoke(new object[]{paths});
        object before=Call(Aim,"Active");Settings windows=Settings.Read();Exception failure=null;
        Dictionary<string,object> summary=null;
        try {
            Console.WriteLine("live / temporary kernel cases + restoration");
            Dictionary<string,object> proof=Command("aim verify");System.Collections.IList steps=(System.Collections.IList)proof["Steps"];
            Expect(steps.Count==13 && (bool)proof["OriginalDriverStateRestored"] && (bool)proof["RawInputSinkRegistered"] && proof["RawInputError"]==null && proof["GameInputVerified"]==null,"real kernel verification failed");
            foreach(object step in steps) Expect((bool)Map(step)["KernelReadbackMatched"],"kernel readback mismatch");
            int undos=0;
            List<string> names=new List<string>();
            foreach(string game in new string[]{"valorant","cs2","kovaaks-valorant","kovaaks-cs2","kovaaks-tracking"}) foreach(string style in new string[]{"","-steady","-linear"}) names.Add(game+style);
            Dictionary<string,object> status=Command("status");string mousePath=(string)Map(status["receiver"])["Path"];
            File.WriteAllText(Path.Combine(root,"game-tuning.json"),Json.Serialize(new Dictionary<string,object>{{mousePath,new MovementTuning {DevicePath=mousePath,MeasuredUtc=DateTime.UtcNow.ToString("o"),SlowP90=1,FastP75=10,IntervalMs=8,SlowReports=250,FastReports=250,Source="synthetic test fixture / not measured movement"}}}));
            names.Add("valorant-personal");names.Add("valorant-steady-personal");names.Add("kovaaks-valorant-personal");
            foreach(string name in names) {
                Console.WriteLine("live / apply, repeat and undo "+name);
                Dictionary<string,object> applied=Command("preset apply "+name),read=Map(Map(applied["Response"])["Readback"]);
                Expect((bool)applied["Applied"] && !(bool)applied["GameSettingsApplied"] && (string)read["Mode"]=="lut" && Convert.ToDouble(read["InputHalfLifeMs"])==0 && (int)Map(applied["Windows"])["Acceleration"]==0,"live preset apply failed");
                Dictionary<string,object> match=Command("preset status");
                Expect((bool)match["WindowsMatch"] && (name.EndsWith("-personal") ? (bool)match["LastAppliedMatches"] : ((System.Collections.IList)match["DriverMatches"]).Contains(name)) && !(bool)match["GameSettingsVerified"],"live preset status missed active recipe");
                if(name=="valorant") {
                    object recipeConfig=Call(Aim,"Active");
                    Command("aim smooth on 2");Command("aim smooth off");
                    Expect(Same(recipeConfig,Call(Aim,"Active")),"smoothing roundtrip changed the live recipe curve");
                    string prefsPath=Path.Combine(root,"aim-presets.json");byte[] recipePrefs=File.ReadAllBytes(prefsPath);
                    Command("aim curve natural");
                    Dictionary<string,object> imported=Map(Call(Aim,"Parse",Json.Serialize(Call(Aim,"Active"))));
                    foreach(object value in (System.Collections.IList)imported["profiles"]) if((string)Map(value)["name"]==(string)read["Profile"]) Map(value)["Input Speed Cap"]=10.0;
                    Call(Aim,"Write",imported);
                    string prefsBytes=Convert.ToBase64String(File.ReadAllBytes(prefsPath));
                    Command("aim smooth on 2");Command("aim smooth off");
                    Expect(Same(imported,Call(Aim,"Active")) && prefsBytes==Convert.ToBase64String(File.ReadAllBytes(prefsPath)),"independent smoothing changed imported cap, curve, timing or saved metadata");
                    Call(Aim,"Write",recipeConfig);
                    File.WriteAllBytes(prefsPath,recipePrefs);
                    Console.WriteLine("live / legacy LUT tail repair with missing metadata + stale-preview guards");
                    Dictionary<string,object> legacy=Map(Call(Aim,"Parse",Json.Serialize(recipeConfig)));
                    foreach(object value in (System.Collections.IList)legacy["profiles"]) if((string)Map(value)["name"]==(string)read["Profile"]) {
                        Dictionary<string,object> timing=Map(Map(value)["Input speed calculation parameters"]);
                        timing["Time in ms after which an input is weighted at half its original value."]=4.0;
                        timing["Time in ms after which an output is weighted at half its original value."]=8.0;
                        timing["Time in ms after which scale is weighted at half its original value."]=4.0;
                    }
                    Call(Aim,"Write",legacy);File.Delete(prefsPath);Settings tailWindows=Settings.Read();
                    Device target=(Device)Call(App.GetType("Helox.Program",true),"Selected");
                    foreach(string guard in new string[]{"RepairTail","RecoverControls"}) {
                        bool rejected=false;try {Call(Aim,guard,target,true,recipeConfig);}catch(InvalidOperationException e) {rejected=e.Message.Contains("mouse setup changed");}
                        Expect(rejected && Same(legacy,Call(Aim,"Active")) && !File.Exists(prefsPath),"stale filter/recovery preview changed state");
                    }
                    Dictionary<string,object> tailPreview=Command("aim flick-tail preview");
                    Expect(!(bool)tailPreview["Applied"] && (bool)tailPreview["Changed"] && Same(legacy,Call(Aim,"Active")) && !File.Exists(prefsPath),"tail preview changed live driver or metadata");
                    Dictionary<string,object> tailApplied=Command("aim flick-tail apply"),tailRead=Map(tailApplied["Readback"]);
                    Expect((bool)tailApplied["Applied"] && Convert.ToDouble(tailRead["InputHalfLifeMs"])==0 && Convert.ToDouble(tailRead["OutputHalfLifeMs"])==0 && Convert.ToDouble(tailRead["ScaleHalfLifeMs"])==4 && tailWindows.Same(Settings.Read()) && !File.Exists(prefsPath),"tail repair lost stability, modified windows or fabricated metadata");
                    Expect(Convert.ToInt64(Map(tailApplied["Response"])["AfterFlickPeakCounts"])<Convert.ToInt64(Map(tailApplied["BeforeResponse"])["AfterFlickPeakCounts"]) && Convert.ToInt32(Map(tailApplied["Response"])["ReversalWrongWayReports"])==0,"tail repair did not improve the modeled flick recovery");
                    Expect(Same(Call(Aim,"Canonical",Call(Aim,"ConfigureSmoothing",legacy,(string)read["DeviceId"],false,4.0,true)),Call(Aim,"Active")),"tail repair changed an unrelated profile field");
                    Expect(!(bool)Command("aim flick-tail apply")["Changed"],"clean tail repair did not remain a no-op");
                    Call(Aim,"Write",recipeConfig);File.WriteAllBytes(prefsPath,recipePrefs);
                    // The kernel already matches; changing only saved choices still needs undo.
                    string gameUndo=Path.Combine(root,"game-undo.json");byte[] recipeUndo=File.ReadAllBytes(gameUndo);
                    Settings recipeWindows=Settings.Read();string id=(string)read["DeviceId"];
                    foreach(bool missing in new bool[]{false,true}) {
                        Dictionary<string,object> changedPrefs=Map(Call(Aim,"Parse",System.Text.Encoding.UTF8.GetString(recipePrefs).TrimStart('\uFEFF')));
                        if(missing) {changedPrefs.Remove(id);changedPrefs["HID\\METADATA-PEER"]=new Dictionary<string,object>{{"precision",false},{"smooth",false}};}else Map(changedPrefs[id])["gainLimit"]=1.6;
                        byte[] changedBytes=System.Text.Encoding.UTF8.GetBytes(Json.Serialize(changedPrefs));File.WriteAllBytes(prefsPath,changedBytes);
                        if(missing) {
                            Expect(((string)Command("aim status")["Note"]).Contains("saved curve controls missing or changed"),"missing saved curve controls hidden from status");
                            Dictionary<string,object> controlsPreview=Command("aim controls preview");
                            Expect((bool)controlsPreview["Available"] && !(bool)controlsPreview["Applied"] && Convert.ToBase64String(changedBytes)==Convert.ToBase64String(File.ReadAllBytes(prefsPath)),"controls recovery preview changed preferences");
                            Dictionary<string,object> controlsRecovered=Command("aim controls recover"),recoveredPrefs=Map(Call(Aim,"Saved",prefsPath));
                            Expect((bool)controlsRecovered["Applied"] && !(bool)controlsRecovered["DriverChanged"] && Same(recipeConfig,Call(Aim,"Active")) && recipeWindows.Same(Settings.Read()) && Same(changedPrefs["HID\\METADATA-PEER"],recoveredPrefs["HID\\METADATA-PEER"]),"controls recovery changed native state, windows or peer choices");
                            Expect(!((string)Command("aim status")["Note"]).Contains("saved curve controls missing or changed"),"recovered controls remain unavailable");
                            File.WriteAllBytes(prefsPath,changedBytes);
                        }
                        Command("preset apply valorant");string preferenceUndo=Convert.ToBase64String(File.ReadAllBytes(gameUndo));
                        Expect(Same(recipeConfig,Call(Aim,"Active")) && recipeWindows.Same(Settings.Read()) && preferenceUndo!=Convert.ToBase64String(recipeUndo),"preference-only apply lost undo or changed native settings");
                        Command("preset apply valorant");Expect(preferenceUndo==Convert.ToBase64String(File.ReadAllBytes(gameUndo)),"preference-only no-op replaced undo");
                        Command("preset undo");Expect(Convert.ToBase64String(changedBytes)==Convert.ToBase64String(File.ReadAllBytes(prefsPath)) && Same(recipeConfig,Call(Aim,"Active")) && recipeWindows.Same(Settings.Read()),"preference-only undo failed exact restoration");
                        File.WriteAllBytes(prefsPath,recipePrefs);File.WriteAllBytes(gameUndo,recipeUndo);
                    }
                }
                string undoPath=Path.Combine(root,"game-undo.json"),undoBytes=Convert.ToBase64String(File.ReadAllBytes(undoPath));
                Dictionary<string,object> repeat=Command("preset apply "+name);
                Expect((bool)repeat["Applied"] && undoBytes==Convert.ToBase64String(File.ReadAllBytes(undoPath)),"identical reapply replaced useful undo");
                Expect((bool)Command("preset undo")["restored"],"live preset undo failed");
                Expect(Same(before,Call(Aim,"Active")) && windows.Same(Settings.Read()),"combined undo changed original native state");undos++;
            }
            Console.WriteLine("live / forced preference write failure + independent rollback");
            string prefs=Path.Combine(root,"aim-presets.json");if(!File.Exists(prefs)) File.WriteAllText(prefs,"{}");FileAttributes attrs=File.GetAttributes(prefs);
            try {
                File.SetAttributes(prefs,attrs|FileAttributes.ReadOnly);
                Dictionary<string,object> failedSmooth=Command("aim smooth on 2",true);
                Expect(((string)failedSmooth["error"]).Contains("previous settings and saved files restored") && Same(before,Call(Aim,"Active")) && windows.Same(Settings.Read()),"independent smoothing persistence failure did not roll back");
                Dictionary<string,object> failed=Command("preset apply valorant",true);
                Expect(((string)failed["error"]).Contains("previous settings and saved files restored"),"persistence failure did not report verified recovery");
            }finally {File.SetAttributes(prefs,attrs);}
            Expect(Same(before,Call(Aim,"Active")) && windows.Same(Settings.Read()),"failed persistence changed native state");
            Console.WriteLine("live / interrupted verification guard + snapshot restoration");
            string recoveryPath=Path.Combine(root,"verify-recovery.json");File.WriteAllText(recoveryPath,Json.Serialize(before));
            Expect(((string)Command("aim precision on",true)["error"]).Contains("unfinished live verification"),"unfinished verification allowed an aim write");
            Expect(((string)Command("preset apply valorant",true)["error"]).Contains("unfinished live verification"),"unfinished verification allowed a recipe write");
            foreach(string mutation in new string[]{"aim controls recover","aim flick-tail apply","aim smooth off"}) Expect(((string)Command(mutation,true)["error"]).Contains("unfinished live verification"),"pending recovery allowed independent filter/control writes");
            string tuningPath=Path.Combine(root,"game-tuning.json");byte[] tuningBytes=File.ReadAllBytes(tuningPath);File.Delete(tuningPath);
            try {Expect(((string)Command("preset apply valorant personal",true)["error"]).Contains("unfinished live verification"),"missing calibration hid a pending recovery");}
            finally {File.WriteAllBytes(tuningPath,tuningBytes);}
            Expect(Same(before,Call(Aim,"Active")),"recovery guard changed the driver");
            Expect(((string)Command("aim status")["Note"]).Contains("unfinished verification"),"recovery status missing");
            Expect((bool)Command("aim verify restore")["restored"] && !File.Exists(recoveryPath) && Same(before,Call(Aim,"Active")),"snapshot restoration failed");
            CrashAndActivation(root,before);
            summary=new Dictionary<string,object>{{"DriverCases",steps.Count},{"PresetsAppliedAndUndone",undos},{"IndependentSmoothing",true},{"LegacyTailRepair",true},{"ExactControlsRecovery",true},{"StaleAdjustmentPreviewBlocked",true},{"IdempotentReapply",true},{"PreferenceOnlyUndo",true},{"PersistenceFailureRecovered",true},{"RecoveryGuardAndRestore",true},{"ForcedExitRecovered",true},{"BlockingActivationVerified",true},{"CorruptUndoBlocked",true},{"RawReports",proof["CapturedMotionReports"]},{"GameInputVerified",null}};
        }catch(Exception e) {failure=e;}
        // Attempt independent native recovery. Preserve recovery evidence if the driver cannot be restored.
        List<string> recovery=new List<string>();bool driverRestored=false,windowsRestored=false;
        try {if(File.Exists(Path.Combine(root,"game-recovery.json")) || !Same(before,Call(Aim,"Active"))) Call(Aim,"Write",before);driverRestored=Same(before,Call(Aim,"Active"));Expect(driverRestored,"native restore mismatch");}catch(Exception e) {recovery.Add("driver: "+e.Message);}
        try {if(!windows.Same(Settings.Read())) windows.Apply();windowsRestored=windows.Same(Settings.Read());Expect(windowsRestored,"windows restore mismatch");}catch(Exception e) {recovery.Add("windows: "+e.Message);}
        if(driverRestored && windowsRestored) try {Files.GetMethod("Restore",Instance).Invoke(files,new object[0]);}catch(Exception e) {recovery.Add("files: "+e.Message);}
        if(failure!=null || recovery.Count>0) throw new IOException((failure==null ? "live verification recovery failed" : failure.Message)+(recovery.Count==0 ? "" : "; "+String.Join(" / ",recovery.ToArray())));
        Console.WriteLine(Json.Serialize(summary));
    }
    private static void CrashAndActivation(string root,object before) {
        string checkpoint=Path.Combine(root,"game-recovery.json"),undoPath=Path.Combine(root,"undo.json");
        object saved=Files.GetConstructor(Instance,null,new Type[]{typeof(string[])},null).Invoke(new object[]{new string[]{Path.Combine(root,"aim-presets.json"),Path.Combine(root,"game-undo.json"),undoPath}});
        Console.WriteLine("live / corrupt previous desktop undo rejected before activation");
        File.WriteAllText(undoPath,"{\"Speed\":\"10\"}");Settings initial=Settings.Read();
        Expect(Command("preset apply valorant",true).ContainsKey("error") && Same(before,Call(Aim,"Active")) && initial.Same(Settings.Read()) && !File.Exists(checkpoint),"bad undo allowed a partial apply");
        Files.GetMethod("Restore",Instance).Invoke(saved,new object[0]);
        Console.WriteLine("live / kill preset process after windows write + durable combined recovery");
        Settings staged=Json.Deserialize<Settings>(Json.Serialize(initial));staged.Speed=11;staged.Acceleration=1;staged.Apply();
        KillAfterWindows("preset apply valorant",checkpoint,0);
        Expect(((string)Command("aim precision on",true)["error"]).Contains("unfinished game preset"),"pending game recovery allowed an aim write");
        Expect(((string)Command("set speed 12",true)["error"]).Contains("unfinished game preset"),"pending game recovery allowed a windows write");
        Expect(((string)Command("aim verify",true)["error"]).Contains("unfinished game preset"),"pending game recovery allowed live verification");
        Expect((bool)Command("preset recover")["restored"] && !File.Exists(checkpoint) && staged.Same(Settings.Read()) && Same(before,Call(Aim,"Active")),"hard-exit recovery failed");
        Console.WriteLine("live / kill combined undo + recover its previous applied state");
        Command("preset apply valorant");object applied=Call(Aim,"Active");Settings appliedWindows=Settings.Read();string appliedUndo=Convert.ToBase64String(File.ReadAllBytes(undoPath));
        KillAfterWindows("preset undo",checkpoint,1);
        Expect((bool)Command("preset recover")["restored"] && !File.Exists(checkpoint) && appliedWindows.Same(Settings.Read()) && Same(applied,Call(Aim,"Active")) && appliedUndo==Convert.ToBase64String(File.ReadAllBytes(undoPath)),"interrupted undo did not recover its prior applied state");
        Expect((bool)Command("preset undo")["restored"] && staged.Same(Settings.Read()) && Same(before,Call(Aim,"Active")),"recovered undo could not be retried");
        Files.GetMethod("Restore",Instance).Invoke(saved,new object[0]);initial.Apply();
        Console.WriteLine("live / kill independent filter process + durable recovery");
        string filterStrength=Convert.ToDouble(Command("aim status")["OutputHalfLifeMs"])==2 ? "8" : "2";
        KillAtCheckpoint("aim smooth on "+filterStrength,checkpoint);
        foreach(string mutation in new string[]{"aim smooth off","aim flick-tail apply","aim controls recover"}) Expect(((string)Command(mutation,true)["error"]).Contains("unfinished game preset"),"pending filter checkpoint allowed another change");
        Expect((bool)Command("preset recover")["restored"] && !File.Exists(checkpoint) && initial.Same(Settings.Read()) && Same(before,Call(Aim,"Active")),"hard-exit filter recovery failed");
        Files.GetMethod("Restore",Instance).Invoke(saved,new object[0]);
        Console.WriteLine("live / synchronous activation delay + immediate readback + restoration");
        Type game=App.GetType("Helox.GamePresets",true),recovery=App.GetType("Helox.GameRecovery",true);
        string id=(string)Command("aim status")["DeviceId"];
        object after=Call(game,"Configure",before,Call(Aim,"Defaults"),id,Call(game,"Get","valorant"));
        if(Same(before,Call(Aim,"Canonical",after))) after=Call(game,"Configure",before,Call(Aim,"Defaults"),id,Call(game,"Get","cs2"));
        object native=Call(Aim,"Validate",after);Call(recovery,"Begin",initial,before,saved);
        Stopwatch activation=Stopwatch.StartNew();native.GetType().GetMethod("Activate").Invoke(native,new object[0]);activation.Stop();
        Expect(activation.ElapsedMilliseconds>=900 && Same(Call(Aim,"Canonical",after),Call(Aim,"Active")),"synchronous activation did not finish before returning");
        Console.WriteLine("live / native Activate blocked "+activation.ElapsedMilliseconds+" ms / immediate readback matched");
        Call(recovery,"Restore");
        Expect(!File.Exists(checkpoint) && Same(before,Call(Aim,"Active")) && initial.Same(Settings.Read()),"activation checkpoint recovery failed");
    }
    private static void KillAtCheckpoint(string args,string checkpoint) {
        ProcessStartInfo info=new ProcessStartInfo(App.Location,args+" --json") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(Process p=Process.Start(info)) {
            Stopwatch timeout=Stopwatch.StartNew();
            while(!p.HasExited && timeout.ElapsedMilliseconds<15000 && !File.Exists(checkpoint)) System.Threading.Thread.Sleep(5);
            if(!p.HasExited && File.Exists(checkpoint)) System.Threading.Thread.Sleep(50);
            bool intercepted=!p.HasExited && File.Exists(checkpoint);
            if(!p.HasExited) {p.Kill();p.WaitForExit();}
            Expect(intercepted,"could not interrupt "+args+" at its filter checkpoint");
        }
    }
    private static void KillAfterWindows(string args,string checkpoint,int acceleration) {
        ProcessStartInfo info=new ProcessStartInfo(App.Location,args+" --json") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(Process p=Process.Start(info)) {
            Stopwatch timeout=Stopwatch.StartNew();
            while(!p.HasExited && timeout.ElapsedMilliseconds<15000 && !(File.Exists(checkpoint) && Settings.Read().Acceleration==acceleration)) System.Threading.Thread.Sleep(5);
            bool intercepted=!p.HasExited && File.Exists(checkpoint) && Settings.Read().Acceleration==acceleration;
            if(!p.HasExited) {p.Kill();p.WaitForExit();}
            Expect(intercepted,"could not interrupt "+args+" after its windows write");
        }
    }
}
