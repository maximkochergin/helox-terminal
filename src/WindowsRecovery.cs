using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
internal static class WindowsRecovery {
    internal static readonly string PathName=Path.Combine(Store.Root,"windows-recovery.json");
    internal static bool Pending {get {return File.Exists(PathName) || Directory.Exists(PathName);}}
    internal static void RequireNone() {if(Pending) throw new InvalidOperationException("unfinished windows change / recover first");}
    internal static Dictionary<string,object> Snapshot(Settings before,byte[] undo) {
        return Read(Aim.Parse(Store.Json.Serialize(new Dictionary<string,object>{{"version",1},{"windows",before},{"undo",undo==null ? null : Convert.ToBase64String(undo)}})));
    }
    internal static Dictionary<string,object> Read(Dictionary<string,object> value) {
        if(value==null || value.Count!=3 || !value.ContainsKey("version") || !(value["version"] is int) || (int)value["version"]!=1 || !value.ContainsKey("windows") || !value.ContainsKey("undo")) throw new ArgumentException("invalid windows recovery snapshot / file kept");
        GamePresets.ReadWindows(value["windows"]);GamePresets.Decode(value["undo"]);return value;
    }
    internal static void Commit(Settings before,Settings after,Action<Settings> apply,Action<Settings> saveUndo,Action begin,Action restoreUndo,Action complete) {
        before.Validate();after.Validate();if(before.Same(after)) return;
        begin();
        try {apply(after);saveUndo(before);complete();}
        catch(Exception error) {
            List<string> failures=new List<string>();
            try {apply(before);}catch(Exception e) {failures.Add("windows: "+e.Message);}
            try {restoreUndo();}catch(Exception e) {failures.Add("undo: "+e.Message);}
            if(failures.Count==0) try {complete();}catch(Exception e) {failures.Add("checkpoint: "+e.Message);}
            throw new IOException("windows change failed: "+error.Message+(failures.Count==0 ? "; previous settings and undo restored" : "; recovery incomplete: "+String.Join(" / ",failures.ToArray())+" / recover first"));
        }
    }
    internal static void Recover(Dictionary<string,object> snapshot,Action<Settings> windows,Action<byte[]> undo,Action complete) {
        snapshot=Read(snapshot);List<string> failures=new List<string>();
        try {windows(GamePresets.ReadWindows(snapshot["windows"]));}catch(Exception e) {failures.Add("windows: "+e.Message);}
        try {undo(GamePresets.Decode(snapshot["undo"]));}catch(Exception e) {failures.Add("undo: "+e.Message);}
        if(failures.Count==0) try {complete();}catch(Exception e) {failures.Add("checkpoint: "+e.Message);}
        if(failures.Count>0) throw new IOException("windows recovery incomplete: "+String.Join(" / ",failures.ToArray())+" / snapshot retained; recover");
    }
    internal static void Restore() {
        Store.Locked(delegate {
            if(GameRecovery.Pending || File.Exists(LiveVerify.RecoveryPath) || Directory.Exists(LiveVerify.RecoveryPath)) throw new InvalidOperationException("aim recovery pending / preset recover or aim verify restore first");
            if(!Pending) throw new InvalidOperationException("no windows recovery snapshot");
            Recover(Aim.Parse(File.ReadAllText(PathName)),delegate(Settings before) {if(!before.Same(Settings.Read())) before.Apply();},
                delegate(byte[] bytes) {new SavedFiles(new Dictionary<string,byte[]>{{Path.Combine(Store.Root,"undo.json"),bytes}}).Restore();},delegate {File.Delete(PathName);});
        });
    }
}
internal static class MouseSelection {
    internal static readonly string PathName=Path.Combine(Store.Root,"selected-mouse.json");
    internal static string Read(string path) {
        if(!File.Exists(path)) return null;
        Dictionary<string,object> value;
        try {value=Aim.Parse(File.ReadAllText(path));}
        catch(ArgumentException) {throw new InvalidOperationException("invalid saved mouse selection / more > choose mouse");}
        catch(IOException) {throw new InvalidOperationException("saved mouse selection unreadable / more > choose mouse");}
        catch(UnauthorizedAccessException) {throw new InvalidOperationException("saved mouse selection unreadable / more > choose mouse");}
        object id;
        if(value==null || value.Count!=1 || !value.TryGetValue("path",out id) || !(id is string) || String.IsNullOrWhiteSpace((string)id) || ((string)id).Length>4096 || ((string)id).IndexOf('\0')>=0) throw new InvalidOperationException("invalid saved mouse selection / more > choose mouse");
        return (string)id;
    }
    internal static void Save(Device device) {
        if(device==null || String.IsNullOrEmpty(device.Path)) throw new ArgumentException("mouse selection missing");
        Store.Locked(delegate {Store.Save(PathName,new Dictionary<string,object>{{"path",device.Path}});});
    }
}
}
