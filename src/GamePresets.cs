using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
public sealed class GameRecipe {
    public string Id {get;set;}
    public string Game {get;set;}
    public string Engine {get;set;}
    public string InputPath {get;set;}
    public string Purpose {get;set;}
    public string Style {get;set;}
    public string[] Styles {get {return new string[]{"balanced","steady","linear"};}}
    public bool Personal {get;set;}
    public MovementTuning Movement {get;set;}
    public string[] Decisions {get;set;}
    public string[] GameSteps {get;set;}
    public string[] Sources {get;set;}
    public string Scope {get {return "applies windows + selected raw accel device / in-game steps are manual / curve values are helox starting points";}}
    public AimCurve Curve {get;set;}
    public bool Stability {get;set;}
    public bool MicroDamping {get;set;}
    public Dictionary<string,object> DriverControls {get {return Controls().ToMap();}}
    internal AimPreset Controls() {
        return new AimPreset {Precision=true,Smooth=false,SmoothMs=2,GainLimit=Math.Max(1.1,Curve.Limit),Stability=Stability,StabilityMs=8,
            SnapDegrees=0,SnapStrength=1,Directions=new AimDirections(),Damping=new AimDamping {Enabled=MicroDamping,LowScale=.9,RecoverySpeed=.75},Curve=Curve.Copy(),SpeedUnit="counts/ms"};
    }
}
public sealed class GamePresetPreview {
    public GameRecipe Recipe {get;set;}
    public Settings Windows {get;set;}
    public string[] WindowsChanges {get;set;}
    public AimResponse Response {get;set;}
    public PresetAssessment Assessment {get;set;}
    public DeviceStackReport Stack {get;set;}
    public bool Applied {get;set;}
    public bool GameSettingsApplied {get;set;}
}
public sealed class GamePresetStatus {
    public string[] DriverMatches {get;set;}
    public bool WindowsMatch {get;set;}
    public string Source {get;set;}
    public bool GameSettingsVerified {get;set;}
    public string LastAppliedRecipe {get;set;}
    public bool? LastAppliedMatches {get;set;}
}
internal static class GamePresets {
    // Names and unrelated devices are not part of the selected mouse's processing.
    internal static string[] Matches(Dictionary<string,object> current,Dictionary<string,object> defaults,string id) {
        List<string> matches=new List<string>();
        foreach(GameRecipe recipe in List()) {
            foreach(string style in new string[]{"balanced","steady","linear"}) {
                GameRecipe variant=Get(recipe.Id+(style=="balanced" ? "" : "-"+style));
                Dictionary<string,object> expected=Aim.Canonical(Configure(current,defaults,id,variant));
                if(SameSelected(current,expected,id)) matches.Add(variant.Id);
            }
        }
        return matches.ToArray();
    }
    internal static bool SameSelected(Dictionary<string,object> current,Dictionary<string,object> expected,string id) {
        Dictionary<string,object> actualEntry=Aim.DeviceEntry(current,id),expectedEntry=Aim.DeviceEntry(expected,id);
        object actualConfig=actualEntry==null ? current["defaultDeviceConfig"] : actualEntry["config"];
        object expectedConfig=expectedEntry==null ? expected["defaultDeviceConfig"] : expectedEntry["config"];
        return Aim.SameValue(actualConfig,expectedConfig) && Aim.SameValue(Aim.EffectiveProfile(current,id),Aim.EffectiveProfile(expected,id));
    }
    internal static bool IsCurrent(Device device,GameRecipe recipe) {
        return Aim.Locked(delegate {
            Dictionary<string,object> current=Aim.Active();string id=Aim.Id(device);
            return SameSelected(current,Aim.Canonical(Configure(current,Aim.Defaults(),id,recipe)),id);
        });
    }
    internal static GamePresetStatus Status(Device device) {
        return Aim.Locked(delegate {
            Dictionary<string,object> current=Aim.Active();Settings windows=Settings.Read();
            GamePresetStatus result=new GamePresetStatus {DriverMatches=Matches(current,Aim.Defaults(),Aim.Id(device)),WindowsMatch=windows.Same(Windows(windows)),
                Source="live driver + windows readback / identical training recipes share a match",GameSettingsVerified=false};
            if(File.Exists(UndoPath)) {
                try {
                    Dictionary<string,object> last=ReadUndo(UndoPath);object lastDevice;
                    if(last.TryGetValue("deviceId",out lastDevice) && String.Equals(lastDevice as string,Aim.Id(device),StringComparison.OrdinalIgnoreCase)) {
                        result.LastAppliedRecipe=(string)last["game"];result.LastAppliedMatches=SameSelected(current,Aim.Map(last["afterAim"]),Aim.Id(device));
                    }
                }catch {result.LastAppliedMatches=null;}
            }
            return result;
        });
    }
    private static readonly string UndoPath=Path.Combine(Store.Root,"game-undo.json");
    internal static GameRecipe[] List() {return new GameRecipe[]{Get("valorant"),Get("cs2"),Get("kovaaks-valorant"),Get("kovaaks-cs2"),Get("kovaaks-tracking")};}
    internal static GameRecipe Get(string id) {
        string original=id;bool personal=id.EndsWith("-personal",StringComparison.Ordinal);if(personal) id=id.Substring(0,id.Length-9);
        string style="balanced";
        foreach(string variant in new string[]{"steady","linear"}) if(id.EndsWith("-"+variant,StringComparison.Ordinal)) {style=variant;id=id.Substring(0,id.Length-variant.Length-1);break;}
        if(style=="linear" && personal) throw new ArgumentException("linear is a reference / use balanced or steady with personal tuning");
        bool val=id=="valorant" || id=="kovaaks-valorant",cs=id=="cs2" || id=="kovaaks-cs2",tracking=id=="kovaaks-tracking",trainer=id.StartsWith("kovaaks-",StringComparison.Ordinal);
        if(!val && !cs && !tracking) throw new ArgumentException("unknown game preset / use preset list");
        GameRecipe result=new GameRecipe {Id=original,Style=style,Personal=personal,Game=trainer ? "kovaak's" : val ? "valorant" : "counter-strike 2",Engine=trainer ? "unreal engine / exact version not asserted" : val ? "unreal engine 5 / migration to 5.3 in 11.02" : "source 2",
            InputPath=val && !trainer ? "raw input buffer always on since 11.06" : trainer ? "trainer sensitivity and fov scales / driver counts upstream" : "source 2 input / no legacy cs:go mouse commands applied",
            Purpose=tracking ? "tracking practice / mild slow-motion damping / not the matched tactical set" : val ? "fine corrections with a delayed, bounded fast-turn ramp" : "fine corrections with a wider fast-turn transition",
            Curve=new AimCurve {Base=1,Start=val ? 3 : cs ? 4 : 2,End=val ? 35 : cs ? 45 : 30,Limit=val ? 1.2 : cs ? 1.25 : 1.35,Shape=tracking ? 1 : 1.5},Stability=tracking,MicroDamping=tracking,
            GameSteps=trainer ? new string[]{"sensitivity scale: "+(cs ? "cs2 / source" : "valorant"),"use your matching game's sensitivity and physical dpi stage","fov scale: "+(cs ? "cs2 / source; match your gameplay aspect ratio" : "valorant; match your gameplay aspect ratio"),tracking ? "tracking variant changes the filter / not a 1:1 tactical practice match" : "keep the same helox recipe in the trainer and target game"}
                : val ? new string[]{"keep your in-game sensitivity and physical dpi stage","raw input buffer is automatic / no toggle needed","compare slow corrections and fast turns in the practice range"}
                : new string[]{"keep your in-game sensitivity and physical dpi stage","no m_rawinput or m_customaccel cfg changes from cs:go guides","compare in offline practice; use valve telemetry if frame-time stutters remain"},
            Sources=trainer ? new string[]{"https://wiki.kovaaks.com/en/home/KovaaK%27s/Settings","https://www.kovaak.com/film-notation/"}
                : val ? new string[]{"https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-02/","https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-06/"}
                : new string[]{"https://store.steampowered.com/app/730/CounterStrike_2/","https://help.steampowered.com/en/faqs/view/5E6F-5B36-5485-F6B9"}};
        if(style=="steady") {result.Stability=true;result.MicroDamping=false;result.Curve.Limit=val ? 1.15 : cs ? 1.2 : 1.25;}
        if(style=="linear") {result.Stability=false;result.MicroDamping=false;result.Curve.Limit=1;result.Purpose="flat 1x reference / compare with acceleration using the same sensitivity";}
        List<string> steps=new List<string>(result.GameSteps);
        steps.Add(trainer ? "compare using the same scenario, style and speed range; do not compare different filter setups" : "test first in practice with frame-time/FPS and network stats visible; separate camera motion from online hit feedback");
        if(trainer) steps.Add("fov is engine-specific / a matching number alone does not match the projection");
        result.GameSteps=steps.ToArray();
        result.Decisions=new string[]{
            "acceleration / "+(style=="linear" ? "flat 1x / reference setup" : "slow 1x / bounded fast-turn ramp"),
            "gain stability / "+(result.Stability ? "4 ms / averages scale changes, not direction" : "off / immediate scale response"),
            "micro damping / "+(result.MicroDamping ? "0.9x / also reduces deliberate slow corrections" : "off / preserve small corrections"),
            "output smoothing / off / avoid flick tails",
            "angle snapping + direction reduction / off / preserve free aiming",
            "timing / automatic / never force a guessed polling rate",
            "windows pointer / 10 of 20 + acceleration off / desktop only",
            "hardware dpi + game sensitivity / kept / physical dpi stage must stay unchanged"};
        return result;
    }
    internal static GameRecipe Build(Device device,string name) {
        GameRecipe recipe=Get(name);
        if(recipe.Personal) {
            if(recipe.Style=="linear") throw new ArgumentException("linear is a reference / use balanced or steady with personal tuning");
            MovementTuning tuning=PresetTuning.Read(device);recipe.Movement=tuning;
            recipe.Curve.Start=Math.Round(tuning.SlowP90*1.1,3);
            recipe.Curve.End=Math.Round(Math.Max(recipe.Curve.Start+.1,tuning.FastP75),3);recipe.Curve.Check();
        }
        return recipe;
    }
    internal static Settings Windows(Settings before) {
        Settings result=Store.Json.Deserialize<Settings>(Store.Json.Serialize(before));result.Speed=10;result.Acceleration=0;result.Validate();return result;
    }
    internal static Dictionary<string,object> Configure(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,GameRecipe recipe) {
        Dictionary<string,object> result=Aim.ConfigurePreset(current,defaults,id,recipe.Controls(),true);
        Dictionary<string,object> config=Aim.Map(Aim.DeviceEntry(result,id)["config"]);
        config["DPI (normalizes input speed unit: counts/ms -> in/s)"]=0;
        config["Polling rate Hz (keep at 0 for automatic adjustment)"]=0;
        config["Use constant time interval based on polling rate"]=false;
        config["minimumTime"]=.0625;config["maximumTime"]=100.0;return result;
    }
    internal static GamePresetPreview Preview(Device device,string name) {
        GameRecipe recipe=Build(device,name);Dictionary<string,object> before=Aim.Active();string id=Aim.Id(device);
        Settings windowsBefore=Settings.Read(),windows=Windows(windowsBefore);
        AimResponse response=AimResponseTest.Run(Configure(before,Aim.Defaults(),id,recipe),id,8);
        response.IntervalSource="8 ms example / not measured hardware polling";response.Readback.Note="proposed game preset / not activated";Aim.DescribeDamping(response.Readback,recipe.Controls());
        return new GamePresetPreview {Recipe=recipe,Windows=windows,WindowsChanges=windows.PreviewChanges(windowsBefore),Response=response,Stack=DeviceStack.Read(device),Assessment=PresetAssessment.Run(Configure(before,Aim.Defaults(),id,recipe),id),Applied=false,GameSettingsApplied=false};
    }
    internal static void RequireStartedStack(DeviceStackReport stack) {
        if(stack==null || stack.RawAccelPresent!=true || stack.Started!=true || stack.ProblemCode!=0)
            throw new InvalidOperationException("selected mouse's started raw accel stack must be confirmed / check driver before applying / nothing changed");
    }
    internal static Dictionary<string,object> CommitPair(Settings beforeWindows,Dictionary<string,object> beforeAim,Settings afterWindows,Dictionary<string,object> afterAim,
        Action<Settings> applyWindows,Func<Dictionary<string,object>,Dictionary<string,object>> writeAim,Action<Dictionary<string,object>> persist,Action restoreFiles,Action recovered=null) {
        bool windowsStarted=false,driverStarted=false;
        try {
            if(!beforeWindows.Same(afterWindows)) {windowsStarted=true;applyWindows(afterWindows);}
            Dictionary<string,object> read=beforeAim;
            if(!Aim.SameValue(beforeAim,afterAim)) {driverStarted=true;read=writeAim(afterAim);}
            persist(read);return read;
        }
        catch(Exception original) {
            List<string> failures=new List<string>();
            // Independent recovery attempts: one failed component must not skip the others.
            if(driverStarted) try {writeAim(beforeAim);}catch(Exception e) {failures.Add("driver: "+e.Message);}
            if(windowsStarted) try {applyWindows(beforeWindows);}catch(Exception e) {failures.Add("windows: "+e.Message);}
            try {restoreFiles();}catch(Exception e) {failures.Add("saved files: "+e.Message);}
            if(failures.Count==0 && recovered!=null) try {recovered();}catch(Exception e) {failures.Add("recovery snapshot: "+e.Message);}
            throw new IOException("game preset failed: "+original.Message+(failures.Count==0 ? "; previous settings and saved files restored" : "; rollback failed: "+String.Join(" / ",failures.ToArray())+" / preset recover"));
        }
    }
    internal static GamePresetPreview Apply(Device device,string name) {
        Get(name);GamePresetPreview result=null;
        Store.Locked(delegate {result=Aim.Locked(delegate {
            GameRecipe recipe=Build(device,name);
            DeviceStackReport stack=DeviceStack.Read(device);RequireStartedStack(stack);
            LiveVerify.RequireNoRecovery();Dictionary<string,object> before=Aim.Active();string id=Aim.Id(device);Settings beforeWindows=Settings.Read(),afterWindows=Windows(beforeWindows);
            Dictionary<string,object> after=Aim.Canonical(Configure(before,Aim.Defaults(),id,recipe));
            AimResponse response=AimResponseTest.Run(after,id,8);response.IntervalSource="8 ms example / not measured hardware polling";
            PresetAssessment assessment=PresetAssessment.Run(after,id);
            if(!assessment.ModelChecksPassed) throw new InvalidOperationException("recipe failed flick/reversal model checks / nothing applied");
            string preferences=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> saved=Aim.Saved(preferences);
            if(File.Exists(UndoPath)) ReadUndo(UndoPath);
            string aimBackup=Path.Combine(Store.Root,"aim-before.json");if(File.Exists(aimBackup)) Aim.Validate(Aim.Parse(File.ReadAllText(aimBackup)));
            Store.Backup();if(!File.Exists(aimBackup)) Store.Save(aimBackup,before);
            SavedFiles files=new SavedFiles(new string[]{preferences,UndoPath,Path.Combine(Store.Root,"undo.json")});
            GameRecovery.Begin(beforeWindows,before,files);
            saved[id]=recipe.Controls().ToMap();
            Dictionary<string,object> live=CommitPair(beforeWindows,before,afterWindows,after,delegate(Settings s){s.Apply();},Aim.Write,delegate(Dictionary<string,object> read) {
                Store.Save(preferences,saved);if(!beforeWindows.Same(afterWindows)) Store.Save(Path.Combine(Store.Root,"undo.json"),beforeWindows);
                // Preserve the useful undo when applying an identical recipe again.
                if(!beforeWindows.Same(afterWindows) || !Aim.SameValue(before,read)) Store.Save(UndoPath,new Dictionary<string,object>{{"game",name},{"deviceId",id},{"beforeWindows",beforeWindows},{"beforeAim",before},{"afterWindows",afterWindows},{"afterAim",read},{"afterPresets",saved},{"previousPresets",Encode(files.Bytes(preferences))},{"previousWindowsUndo",Encode(files.Bytes(Path.Combine(Store.Root,"undo.json")))}});
                GameRecovery.Complete();
            },files.Restore,GameRecovery.Complete);
            response.Readback=Aim.Describe(live,id);Aim.DescribeDamping(response.Readback,recipe.Controls());
            return new GamePresetPreview {Recipe=recipe,Windows=afterWindows,WindowsChanges=afterWindows.PreviewChanges(beforeWindows),Response=response,Stack=stack,Assessment=assessment,Applied=true,GameSettingsApplied=false};
        });});return result;
    }
    private static Dictionary<string,object> ReadUndo(string path) {
        Dictionary<string,object> map=ValidateUndo(Aim.Parse(File.ReadAllText(path)));
        foreach(string key in new string[]{"beforeAim","afterAim"}) Aim.Validate(Aim.Map(map[key]));return map;
    }
    internal static Dictionary<string,object> ValidateUndo(Dictionary<string,object> map) {
        string[] keys={"game","beforeWindows","beforeAim","afterWindows","afterAim","afterPresets","previousPresets","previousWindowsUndo"};
        if(map==null || (map.Count!=keys.Length && !(map.Count==keys.Length+1 && map.ContainsKey("deviceId")))) throw new ArgumentException("invalid game undo snapshot");
        foreach(string key in keys) if(!map.ContainsKey(key)) throw new ArgumentException("invalid game undo snapshot");
        if(map.ContainsKey("deviceId") && (!(map["deviceId"] is string) || String.IsNullOrWhiteSpace((string)map["deviceId"]) || ((string)map["deviceId"]).Length>512)) throw new ArgumentException("invalid game undo mouse");
        Get((string)map["game"]);
        foreach(string key in new string[]{"beforeWindows","afterWindows"}) ReadWindows(map[key]);
        foreach(string key in new string[]{"beforeAim","afterAim"}) AimConfigGuard.Check(Aim.Map(map[key]));
        Aim.ValidateSaved(Aim.Map(map["afterPresets"]));
        ReadBytes(map["previousPresets"]);byte[] windowsUndo=Decode(map["previousWindowsUndo"]);if(windowsUndo!=null) ReadWindows(Aim.Parse(System.Text.Encoding.UTF8.GetString(windowsUndo).TrimStart('\uFEFF')));return map;
    }
    internal static Settings ReadWindows(object value) {
        // Reuse strict settings-file validation instead of coercing imported fields.
        Dictionary<string,object> fields=Aim.Map(value);string[] keys={"Speed","Threshold1","Threshold2","Acceleration","WheelLines","DoubleClickMs","SwapButtons"};
        if(fields==null || fields.Count!=7) throw new ArgumentException("invalid game windows snapshot");
        foreach(string key in keys) if(!fields.ContainsKey(key) || !(fields[key] is int)) throw new ArgumentException("invalid game windows snapshot");
        Settings result=Store.Json.Deserialize<Settings>(Store.Json.Serialize(fields));result.Validate();return result;
    }
    private static string Encode(byte[] bytes) {return bytes==null ? null : Convert.ToBase64String(bytes);}
    internal static byte[] Decode(object value) {
        if(value==null) return null;string encoded=value as string;if(encoded==null || encoded.Length>1400000) throw new ArgumentException("invalid game preset recovery data");
        byte[] bytes;try {bytes=Convert.FromBase64String(encoded);}catch(FormatException) {throw new ArgumentException("invalid game preset recovery data");}
        return bytes;
    }
    internal static byte[] ReadBytes(object value) {
        byte[] bytes=Decode(value);if(bytes==null) return null;
        // Recovery must not import a broken preset file.
        Aim.ValidateSaved(Aim.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')));return bytes;
    }
    internal static void Undo() {
        Store.Locked(delegate {Aim.Locked(delegate {
            LiveVerify.RequireNoRecovery();if(!File.Exists(UndoPath)) throw new InvalidOperationException("no game preset to undo");
            Dictionary<string,object> undo=ReadUndo(UndoPath),live=Aim.Active();Settings current=Settings.Read();
            if(!current.Same(ReadWindows(undo["afterWindows"])) || !Aim.SameValue(live,undo["afterAim"])) throw new InvalidOperationException("settings changed since preset / undo stopped to preserve newer changes");
            string preferences=Path.Combine(Store.Root,"aim-presets.json");
            if(!Aim.SameValue(Aim.Saved(preferences),undo["afterPresets"])) throw new InvalidOperationException("saved controls changed since preset / undo stopped to preserve newer choices");
            SavedFiles files=new SavedFiles(new string[]{preferences,UndoPath,Path.Combine(Store.Root,"undo.json")});
            GameRecovery.Begin(current,live,files);
            CommitPair(current,live,ReadWindows(undo["beforeWindows"]),Aim.Map(undo["beforeAim"]),delegate(Settings s){s.Apply();},Aim.Write,delegate(Dictionary<string,object> read) {
                SavedFiles.Write(preferences,ReadBytes(undo["previousPresets"]));SavedFiles.Write(Path.Combine(Store.Root,"undo.json"),Decode(undo["previousWindowsUndo"]));File.Delete(UndoPath);
                GameRecovery.Complete();
            },files.Restore,GameRecovery.Complete);return true;
        });});
    }
}
internal sealed class SavedFiles {
    private readonly Dictionary<string,byte[]> originals=new Dictionary<string,byte[]>();
    internal SavedFiles(string[] paths) {foreach(string path in paths) originals.Add(path,File.Exists(path) ? File.ReadAllBytes(path) : null);}
    internal SavedFiles(Dictionary<string,byte[]> bytes) {foreach(KeyValuePair<string,byte[]> file in bytes) originals.Add(file.Key,file.Value);}
    internal byte[] Bytes(string path) {return originals[path];}
    internal void Restore() {List<string> errors=new List<string>();foreach(KeyValuePair<string,byte[]> file in originals) try {
        byte[] current=File.Exists(file.Key) ? File.ReadAllBytes(file.Key) : null;if(!Same(current,file.Value)) Write(file.Key,file.Value);
    }catch(Exception e) {errors.Add(e.Message);}if(errors.Count>0) throw new IOException(String.Join(" / ",errors.ToArray()));}
    private static bool Same(byte[] a,byte[] b) {if(a==null || b==null) return a==null && b==null;if(a.Length!=b.Length) return false;for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false;return true;}
    internal static void Write(string path,byte[] bytes) {
        if(bytes==null) {if(File.Exists(path)) File.Delete(path);return;}
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("n")+".tmp";
        try {File.WriteAllBytes(temp,bytes);try {File.Move(temp,path);}catch(IOException) {if(!File.Exists(path)) throw;File.Replace(temp,path,null);}}
        finally {if(File.Exists(temp)) File.Delete(temp);}
    }
}
}
