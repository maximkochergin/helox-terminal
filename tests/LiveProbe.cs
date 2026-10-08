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
        string[] paths={"aim-presets.json","game-undo.json","undo.json","original.json","aim-before.json","verify-recovery.json","game-recovery.json"};
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
            foreach(string name in new string[]{"valorant","cs2","kovaaks-valorant","kovaaks-cs2","kovaaks-tracking"}) {
                Console.WriteLine("live / apply, repeat and undo "+name);
                Dictionary<string,object> applied=Command("preset apply "+name),read=Map(Map(applied["Response"])["Readback"]);
                Expect((bool)applied["Applied"] && !(bool)applied["GameSettingsApplied"] && (string)read["Mode"]=="lut" && Convert.ToDouble(read["InputHalfLifeMs"])==0 && (int)Map(applied["Windows"])["Acceleration"]==0,"live preset apply failed");
                Dictionary<string,object> match=Command("preset status");
                Expect((bool)match["WindowsMatch"] && ((System.Collections.IList)match["DriverMatches"]).Contains(name) && !(bool)match["GameSettingsVerified"],"live preset status missed active recipe");
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
                    Expect(((string)Command("aim smooth off",true)["error"]).Contains("smoothing would replace custom curve settings"),"imported natural profile did not trigger smoothing preservation guard");
                    Expect(Same(imported,Call(Aim,"Active")) && prefsBytes==Convert.ToBase64String(File.ReadAllBytes(prefsPath)),"blocked smoothing changed native or saved settings");
                    Call(Aim,"Write",recipeConfig);
                    File.WriteAllBytes(prefsPath,recipePrefs);
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
                Dictionary<string,object> failed=Command("preset apply valorant",true);
                Expect(((string)failed["error"]).Contains("previous settings and saved files restored"),"persistence failure did not report verified recovery");
            }finally {File.SetAttributes(prefs,attrs);}
            Expect(Same(before,Call(Aim,"Active")) && windows.Same(Settings.Read()),"failed persistence changed native state");
            Console.WriteLine("live / interrupted verification guard + snapshot restoration");
            string recoveryPath=Path.Combine(root,"verify-recovery.json");File.WriteAllText(recoveryPath,Json.Serialize(before));
            Expect(((string)Command("aim precision on",true)["error"]).Contains("unfinished live verification"),"unfinished verification allowed an aim write");
            Expect(((string)Command("preset apply valorant",true)["error"]).Contains("unfinished live verification"),"unfinished verification allowed a recipe write");
            Expect(Same(before,Call(Aim,"Active")),"recovery guard changed the driver");
            Expect(((string)Command("aim status")["Note"]).Contains("unfinished verification"),"recovery status missing");
            Expect((bool)Command("aim verify restore")["restored"] && !File.Exists(recoveryPath) && Same(before,Call(Aim,"Active")),"snapshot restoration failed");
            CrashAndActivation(root,before);
            summary=new Dictionary<string,object>{{"DriverCases",steps.Count},{"PresetsAppliedAndUndone",undos},{"IdempotentReapply",true},{"PersistenceFailureRecovered",true},{"RecoveryGuardAndRestore",true},{"ForcedExitRecovered",true},{"BlockingActivationVerified",true},{"CorruptUndoBlocked",true},{"RawReports",proof["CapturedMotionReports"]},{"GameInputVerified",null}};
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
