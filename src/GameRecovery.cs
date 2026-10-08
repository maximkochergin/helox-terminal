using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Helox {
internal static class GameRecovery {
    internal static readonly string PathName=Path.Combine(Store.Root,"game-recovery.json");
    internal static readonly string[] FileNames={"aim-presets.json","game-undo.json","undo.json"};
    internal static bool Pending {get {return File.Exists(PathName) || Directory.Exists(PathName);}}
    internal static void RequireNoRecovery() {if(Pending) throw new InvalidOperationException("unfinished game preset / preset recover first");}
    internal static Dictionary<string,object> Snapshot(Settings windows,Dictionary<string,object> aim,Dictionary<string,object> files) {
        Dictionary<string,object> snapshot=new Dictionary<string,object>{{"version",1},{"windows",windows},{"aim",aim},{"files",files}};
        // Validate the actual serialized shape, including all saved recovery bytes, before mutation.
        return Read(Aim.Parse(Store.Json.Serialize(snapshot)));
    }
    internal static Dictionary<string,object> Read(Dictionary<string,object> snapshot) {
        string[] fields={"version","windows","aim","files"};
        if(snapshot==null || snapshot.Count!=fields.Length) throw new ArgumentException("invalid game recovery snapshot");
        foreach(string key in fields) if(!snapshot.ContainsKey(key)) throw new ArgumentException("invalid game recovery snapshot");
        if(!(snapshot["version"] is int) || (int)snapshot["version"]!=1) throw new ArgumentException("unsupported game recovery snapshot");
        GamePresets.ReadWindows(snapshot["windows"]);AimConfigGuard.Check(Aim.Map(snapshot["aim"]));
        Dictionary<string,object> files=Aim.Map(snapshot["files"]);
        if(files==null || files.Count!=FileNames.Length) throw new ArgumentException("invalid game recovery files");
        foreach(string name in FileNames) {
            if(!files.ContainsKey(name)) throw new ArgumentException("invalid game recovery files");
            byte[] bytes=GamePresets.Decode(files[name]);if(bytes==null) continue;
            Dictionary<string,object> json=Aim.Parse(new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF'));
            if(name=="aim-presets.json") Aim.ValidateSaved(json);
            else if(name=="game-undo.json") GamePresets.ValidateUndo(json);
            else GamePresets.ReadWindows(json);
        }
        return snapshot;
    }
    internal static void Begin(Settings windows,Dictionary<string,object> aim,SavedFiles saved) {
        RequireNoRecovery();Dictionary<string,object> files=new Dictionary<string,object>();
        foreach(string name in FileNames) {byte[] bytes=saved.Bytes(Path.Combine(Store.Root,name));files.Add(name,bytes==null ? null : Convert.ToBase64String(bytes));}
        Store.Save(PathName,Snapshot(windows,aim,files));
    }
    internal static void Complete() {File.Delete(PathName);}
    internal static void Recover(Dictionary<string,object> snapshot,Action<Dictionary<string,object>> driver,Action<Settings> windows,Action<Dictionary<string,object>> files,Action complete) {
        snapshot=Read(snapshot);List<string> failures=new List<string>();
        // Preserve the checkpoint until every independent component is restored.
        try {driver(Aim.Map(snapshot["aim"]));}catch(Exception e) {failures.Add("driver: "+e.Message);}
        try {windows(GamePresets.ReadWindows(snapshot["windows"]));}catch(Exception e) {failures.Add("windows: "+e.Message);}
        try {files(Aim.Map(snapshot["files"]));}catch(Exception e) {failures.Add("saved files: "+e.Message);}
        if(failures.Count==0) try {complete();}catch(Exception e) {failures.Add("checkpoint: "+e.Message);}
        if(failures.Count>0) throw new IOException("game recovery incomplete: "+String.Join(" / ",failures.ToArray())+" / snapshot retained; preset recover");
    }
    internal static void Restore() {
        LiveVerify.RequireClosedGames();Store.Locked(delegate {Aim.Locked(delegate {
            if(!Pending) throw new InvalidOperationException("no game preset recovery snapshot");
            Dictionary<string,object> snapshot=Read(Aim.Parse(File.ReadAllText(PathName)));
            // Interrupted writes have an uncertain outcome; explicitly write and verify the checkpoint.
            Recover(snapshot,delegate(Dictionary<string,object> cfg) {Aim.Write(cfg);},
                delegate(Settings s) {if(!s.Same(Settings.Read())) s.Apply();},delegate(Dictionary<string,object> files) {
                    Dictionary<string,byte[]> bytes=new Dictionary<string,byte[]>();foreach(string name in FileNames) bytes.Add(Path.Combine(Store.Root,name),GamePresets.Decode(files[name]));
                    new SavedFiles(bytes).Restore();
                },Complete);return true;
        });});
    }
}
}
