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
    public string[] GameSteps {get;set;}
    public string[] Sources {get;set;}
    public string Scope {get {return "applies windows + selected raw accel device / in-game steps are manual / curve values are helox starting points";}}
    public AimCurve Curve {get;set;}
    public bool Stability {get;set;}
    public bool MicroDamping {get;set;}
    public Dictionary<string,object> DriverControls {get {return Controls().ToMap();}}
    internal AimPreset Controls() {
        return new AimPreset {Precision=true,Smooth=false,SmoothMs=2,GainLimit=1.4,Stability=Stability,StabilityMs=8,
            SnapDegrees=0,SnapStrength=1,Directions=new AimDirections(),Damping=new AimDamping {Enabled=MicroDamping,LowScale=.9,RecoverySpeed=.75},Curve=Curve.Copy(),SpeedUnit="counts/ms"};
    }
}
public sealed class GamePresetPreview {
    public GameRecipe Recipe {get;set;}
    public Settings Windows {get;set;}
    public string[] WindowsChanges {get;set;}
    public AimResponse Response {get;set;}
    public bool Applied {get;set;}
    public bool GameSettingsApplied {get;set;}
}
internal static class GamePresets {
    private static readonly string UndoPath=Path.Combine(Store.Root,"game-undo.json");
    internal static GameRecipe[] List() {return new GameRecipe[]{Get("valorant"),Get("cs2"),Get("kovaaks-valorant"),Get("kovaaks-cs2"),Get("kovaaks-tracking")};}
    internal static GameRecipe Get(string id) {
        bool val=id=="valorant" || id=="kovaaks-valorant",cs=id=="cs2" || id=="kovaaks-cs2",tracking=id=="kovaaks-tracking",trainer=id.StartsWith("kovaaks-",StringComparison.Ordinal);
        if(!val && !cs && !tracking) throw new ArgumentException("unknown game preset / use preset list");
        return new GameRecipe {Id=id,Game=trainer ? "kovaak's" : val ? "valorant" : "counter-strike 2",Engine=trainer ? "unreal engine / exact version not asserted" : val ? "unreal engine 5 / migration to 5.3 in 11.02" : "source 2",
            InputPath=val && !trainer ? "raw input buffer always on since 11.06" : trainer ? "trainer sensitivity and fov scales / driver counts upstream" : "source 2 input / no legacy cs:go mouse commands applied",
            Purpose=tracking ? "tracking practice / mild slow-motion damping / not the matched tactical set" : val ? "fine corrections with a delayed, bounded fast-turn ramp" : "fine corrections with a wider fast-turn transition",
            Curve=new AimCurve {Base=1,Start=val ? 3 : cs ? 4 : 2,End=val ? 35 : cs ? 45 : 30,Limit=val ? 1.2 : cs ? 1.25 : 1.35,Shape=tracking ? 1 : 1.5},Stability=tracking,MicroDamping=tracking,
            GameSteps=trainer ? new string[]{"sensitivity scale: "+(cs ? "cs2 / source" : "valorant"),"use your matching game's sensitivity and physical dpi stage","fov scale: "+(cs ? "cs2 / source; match your gameplay aspect ratio" : "valorant; match your gameplay aspect ratio"),tracking ? "tracking variant changes the filter / not a 1:1 tactical practice match" : "keep the same helox recipe in the trainer and target game"}
                : val ? new string[]{"keep your in-game sensitivity and physical dpi stage","raw input buffer is automatic / no toggle needed","compare slow corrections and fast turns in the practice range"}
                : new string[]{"keep your in-game sensitivity and physical dpi stage","no m_rawinput or m_customaccel cfg changes from cs:go guides","compare in offline practice; use valve telemetry if frame-time stutters remain"},
            Sources=trainer ? new string[]{"https://wiki.kovaaks.com/en/home/KovaaK%27s/Settings","https://www.kovaak.com/film-notation/"}
                : val ? new string[]{"https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-02/","https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-06/"}
                : new string[]{"https://store.steampowered.com/app/730/CounterStrike_2/","https://help.steampowered.com/en/faqs/view/5E6F-5B36-5485-F6B9"}};
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
        GameRecipe recipe=Get(name);Dictionary<string,object> before=Aim.Active();string id=Aim.Id(device);
        Settings windowsBefore=Settings.Read(),windows=Windows(windowsBefore);
        AimResponse response=AimResponseTest.Run(Configure(before,Aim.Defaults(),id,recipe),id,8);
        response.IntervalSource="8 ms example / not measured hardware polling";response.Readback.Note="proposed game preset / not activated";Aim.DescribeDamping(response.Readback,recipe.Controls());
        return new GamePresetPreview {Recipe=recipe,Windows=windows,WindowsChanges=windows.PreviewChanges(windowsBefore),Response=response,Applied=false,GameSettingsApplied=false};
    }
    internal static Dictionary<string,object> CommitPair(Settings beforeWindows,Dictionary<string,object> beforeAim,Settings afterWindows,Dictionary<string,object> afterAim,
        Action<Settings> applyWindows,Func<Dictionary<string,object>,Dictionary<string,object>> writeAim,Action<Dictionary<string,object>> persist,Action restoreFiles) {
        try {if(!beforeWindows.Same(afterWindows)) applyWindows(afterWindows);Dictionary<string,object> read=Aim.SameValue(beforeAim,afterAim) ? beforeAim : writeAim(afterAim);persist(read);return read;}
        catch(Exception original) {
            List<string> failures=new List<string>();
            // Independent recovery attempts: one failed component must not skip the others.
            try {writeAim(beforeAim);}catch(Exception e) {failures.Add("driver: "+e.Message);}
            try {applyWindows(beforeWindows);}catch(Exception e) {failures.Add("windows: "+e.Message);}
            try {restoreFiles();}catch(Exception e) {failures.Add("saved files: "+e.Message);}
            throw new IOException("game preset failed: "+original.Message+(failures.Count==0 ? "; previous settings and saved files restored" : "; rollback failed: "+String.Join(" / ",failures.ToArray())));
        }
    }
    internal static GamePresetPreview Apply(Device device,string name) {
        GameRecipe recipe=Get(name);GamePresetPreview result=null;
        Store.Locked(delegate {result=Aim.Locked(delegate {
            LiveVerify.RequireNoRecovery();Dictionary<string,object> before=Aim.Active();string id=Aim.Id(device);Settings beforeWindows=Settings.Read(),afterWindows=Windows(beforeWindows);
            Dictionary<string,object> after=Aim.Canonical(Configure(before,Aim.Defaults(),id,recipe));
            AimResponse response=AimResponseTest.Run(after,id,8);response.IntervalSource="8 ms example / not measured hardware polling";
            string preferences=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> saved=Aim.Saved(preferences);
            if(File.Exists(UndoPath)) ReadUndo(UndoPath);
            string aimBackup=Path.Combine(Store.Root,"aim-before.json");if(File.Exists(aimBackup)) Aim.Validate(Aim.Parse(File.ReadAllText(aimBackup)));
            Store.Backup();if(!File.Exists(aimBackup)) Store.Save(aimBackup,before);
            SavedFiles files=new SavedFiles(new string[]{preferences,UndoPath,Path.Combine(Store.Root,"undo.json")});
            saved[id]=recipe.Controls().ToMap();
            Dictionary<string,object> live=CommitPair(beforeWindows,before,afterWindows,after,delegate(Settings s){s.Apply();},Aim.Write,delegate(Dictionary<string,object> read) {
                Store.Save(preferences,saved);if(!beforeWindows.Same(afterWindows)) Store.Save(Path.Combine(Store.Root,"undo.json"),beforeWindows);
                // Preserve the useful undo when applying an identical recipe again.
                if(!beforeWindows.Same(afterWindows) || !Aim.SameValue(before,read)) Store.Save(UndoPath,new Dictionary<string,object>{{"game",name},{"beforeWindows",beforeWindows},{"beforeAim",before},{"afterWindows",afterWindows},{"afterAim",read},{"afterPresets",saved},{"previousPresets",Encode(files.Bytes(preferences))},{"previousWindowsUndo",Encode(files.Bytes(Path.Combine(Store.Root,"undo.json")))}});
            },files.Restore);
            response.Readback=Aim.Describe(live,id);Aim.DescribeDamping(response.Readback,recipe.Controls());
            return new GamePresetPreview {Recipe=recipe,Windows=afterWindows,WindowsChanges=afterWindows.PreviewChanges(beforeWindows),Response=response,Applied=true,GameSettingsApplied=false};
        });});return result;
    }
    private static Dictionary<string,object> ReadUndo(string path) {
        Dictionary<string,object> map=Aim.Parse(File.ReadAllText(path));
        if(map==null || map.Count!=8 || !map.ContainsKey("previousPresets") || !map.ContainsKey("previousWindowsUndo")) throw new ArgumentException("invalid game undo snapshot");
        Get((string)map["game"]);
        foreach(string key in new string[]{"beforeWindows","afterWindows"}) ReadWindows(map[key]);
        foreach(string key in new string[]{"beforeAim","afterAim"}) Aim.Validate(Aim.Map(map[key]));
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
    private static byte[] Decode(object value) {
        if(value==null) return null;string encoded=value as string;if(encoded==null || encoded.Length>1400000) throw new ArgumentException("invalid game preset recovery data");
        byte[] bytes;try {bytes=Convert.FromBase64String(encoded);}catch(FormatException) {throw new ArgumentException("invalid game preset recovery data");}
        return bytes;
    }
    private static byte[] ReadBytes(object value) {
        byte[] bytes=Decode(value);if(bytes==null) return null;
        // Recovery must not import a broken preset file.
        Dictionary<string,object> presets=Aim.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        if(presets==null) throw new ArgumentException("invalid game preset recovery file");HashSet<string> ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(KeyValuePair<string,object> p in presets) {if(String.IsNullOrWhiteSpace(p.Key) || p.Key.Length>199 || p.Key.IndexOf('\0')>=0 || !ids.Add(p.Key)) throw new ArgumentException("invalid game recovery identity");Aim.ReadPreset(Aim.Map(p.Value));}return bytes;
    }
    internal static void Undo() {
        Store.Locked(delegate {Aim.Locked(delegate {
            LiveVerify.RequireNoRecovery();if(!File.Exists(UndoPath)) throw new InvalidOperationException("no game preset to undo");
            Dictionary<string,object> undo=ReadUndo(UndoPath),live=Aim.Active();Settings current=Settings.Read();
            if(!current.Same(ReadWindows(undo["afterWindows"])) || !Aim.SameValue(live,undo["afterAim"])) throw new InvalidOperationException("settings changed since preset / undo stopped to preserve newer changes");
            string preferences=Path.Combine(Store.Root,"aim-presets.json");
            if(!Aim.SameValue(Aim.Saved(preferences),undo["afterPresets"])) throw new InvalidOperationException("saved controls changed since preset / undo stopped to preserve newer choices");
            SavedFiles files=new SavedFiles(new string[]{preferences,UndoPath,Path.Combine(Store.Root,"undo.json")});
            CommitPair(current,live,ReadWindows(undo["beforeWindows"]),Aim.Map(undo["beforeAim"]),delegate(Settings s){s.Apply();},Aim.Write,delegate(Dictionary<string,object> read) {
                SavedFiles.Write(preferences,ReadBytes(undo["previousPresets"]));SavedFiles.Write(Path.Combine(Store.Root,"undo.json"),Decode(undo["previousWindowsUndo"]));File.Delete(UndoPath);
            },files.Restore);return true;
        });});
    }
}
internal sealed class SavedFiles {
    private readonly Dictionary<string,byte[]> originals=new Dictionary<string,byte[]>();
    internal SavedFiles(string[] paths) {foreach(string path in paths) originals.Add(path,File.Exists(path) ? File.ReadAllBytes(path) : null);}
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
