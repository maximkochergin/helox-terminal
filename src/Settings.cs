using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;

namespace Helox {
public sealed class Settings {
    public int Speed {get;set;}
    public int Threshold1 {get;set;}
    public int Threshold2 {get;set;}
    public int Acceleration {get;set;}
    public int WheelLines {get;set;}
    public int DoubleClickMs {get;set;}
    public int SwapButtons {get;set;}
    public void SetAcceleration(bool enabled) {
        // Preserve an already enabled mode and custom thresholds.
        if(enabled) {if(Acceleration==0) Acceleration=1;} else Acceleration=0;
    }
    public void Setup() {Speed=10;SetAcceleration(true);}
    public static Settings Read() {
        int[] m=Native.Read(3,3);
        return new Settings { Speed=Native.Read(0x70,1)[0], Threshold1=m[0], Threshold2=m[1], Acceleration=m[2],
            WheelLines=Native.Read(0x68,1)[0], DoubleClickMs=(int)Native.GetDoubleClickTime(), SwapButtons=Native.GetSystemMetrics(23) };
    }
    public void Validate() {
        if(Speed<1 || Speed>20 || Threshold1<0 || Threshold2<0 || Acceleration<0 || Acceleration>2 ||
           (WheelLines<0 && WheelLines!=-1) || DoubleClickMs<1 || DoubleClickMs>5000 || SwapButtons<0 || SwapButtons>1)
            throw new ArgumentException("invalid windows settings snapshot");
    }
    internal void Write() {
        Native.Check(Native.SystemParametersInfo(0x71,0,new IntPtr(Speed),3));
        Native.Write(4,new int[]{Threshold1,Threshold2,Acceleration});
        Native.Check(Native.SystemParametersInfo(0x69,unchecked((uint)WheelLines),IntPtr.Zero,3));
        Native.Check(Native.SystemParametersInfo(0x20,(uint)DoubleClickMs,IntPtr.Zero,3));
        Native.Check(Native.SystemParametersInfo(0x21,(uint)SwapButtons,IntPtr.Zero,3));
    }
    public bool Same(Settings other) {
        return other!=null && Speed==other.Speed && Threshold1==other.Threshold1 && Threshold2==other.Threshold2 &&
            Acceleration==other.Acceleration && WheelLines==other.WheelLines && DoubleClickMs==other.DoubleClickMs && SwapButtons==other.SwapButtons;
    }
    internal string[] PreviewChanges(Settings current) {
        Validate();current.Validate();
        List<string> changes=new List<string>();
        if(Speed!=current.Speed) changes.Add("speed / "+current.Speed+" -> "+Speed+" / 20");
        if(Acceleration!=current.Acceleration) changes.Add("acceleration / "+AccelerationLabel(current.Acceleration)+" -> "+AccelerationLabel(Acceleration));
        if(Threshold1!=current.Threshold1 || Threshold2!=current.Threshold2)
            changes.Add("thresholds / "+current.Threshold1+", "+current.Threshold2+" -> "+Threshold1+", "+Threshold2);
        if(WheelLines!=current.WheelLines) changes.Add("wheel / "+WheelLabel(current.WheelLines)+" -> "+WheelLabel(WheelLines));
        if(DoubleClickMs!=current.DoubleClickMs) changes.Add("doubleclick / "+current.DoubleClickMs+" ms -> "+DoubleClickMs+" ms");
        if(SwapButtons!=current.SwapButtons) changes.Add("buttons / "+(current.SwapButtons==0 ? "normal" : "swapped")+" -> "+(SwapButtons==0 ? "normal" : "swapped"));
        return changes.ToArray();
    }
    private static string AccelerationLabel(int value) {return value==0 ? "off" : "on (mode "+value+")";}
    private static string WheelLabel(int value) {return value==-1 ? "page" : value+" lines";}
    public void Apply() {
        Validate(); Settings before=Read();
        try { Write(); if(!Same(Read())) throw new IOException("windows readback did not match requested settings"); }
        catch(Exception error) {
            try { before.Write(); if(!before.Same(Read())) throw new IOException("rollback readback failed"); }
            catch(Exception rollback) { throw new IOException("apply failed: "+error.Message+"; rollback failed: "+rollback.Message); }
            throw new IOException("apply failed; previous settings restored: "+error.Message);
        }
    }
}
internal static class Store {
    internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"helox-terminal");
    internal static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    internal static void Save(string path, object value) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+"."+Guid.NewGuid().ToString("n")+".tmp";
        try {
            File.WriteAllText(temp,Json.Serialize(value));
            try {File.Move(temp,path);}
            catch(IOException) {if(!File.Exists(path)) throw;File.Replace(temp,path,null);}
        }finally {if(File.Exists(temp)) File.Delete(temp);}
    }
    internal static T Load<T>(string path) {
        string text=File.ReadAllText(path);
        try {
            if(typeof(T)==typeof(Settings)) {
                Dictionary<string,object> fields=Json.DeserializeObject(text) as Dictionary<string,object>;
                if(fields==null) throw new ArgumentException();
                foreach(string key in new string[]{"Speed","Threshold1","Threshold2","Acceleration","WheelLines","DoubleClickMs","SwapButtons"}) {
                    object value;
                    if(!fields.TryGetValue(key,out value) || !(value is int)) throw new ArgumentException();
                    foreach(string supplied in fields.Keys)
                        if(supplied!=key && String.Equals(supplied,key,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
                }
                // Apply precisely the values checked above; typed deserialization coerces
                // strings and matches property names without regard to case.
                Settings snapshot=new Settings {Speed=(int)fields["Speed"],Threshold1=(int)fields["Threshold1"],
                    Threshold2=(int)fields["Threshold2"],Acceleration=(int)fields["Acceleration"],
                    WheelLines=(int)fields["WheelLines"],DoubleClickMs=(int)fields["DoubleClickMs"],SwapButtons=(int)fields["SwapButtons"]};
                snapshot.Validate();return (T)(object)snapshot;
            }
            T result=Json.Deserialize<T>(text);
            Settings settings=result as Settings;
            if(settings!=null) settings.Validate();
            return result;
        }catch(ArgumentException) {throw new ArgumentException("invalid or incomplete settings file");}
    }
    internal static string Profile(string name) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"\A[a-z0-9][a-z0-9_-]{0,31}\z")) throw new ArgumentException("profile name: 1..32 lowercase letters, numbers, underscores or hyphens");
        if(System.Text.RegularExpressions.Regex.IsMatch(name,@"^(con|prn|aux|nul|com[1-9]|lpt[1-9])$")) throw new ArgumentException("profile name is reserved by windows / choose another name");
        return Path.Combine(Root,"profiles",name+".json");
    }
    internal static List<string> Profiles(string directory=null) {
        List<string> names=new List<string>();if(directory==null) directory=Path.Combine(Root,"profiles");
        if(Directory.Exists(directory)) foreach(string path in Directory.GetFiles(directory,"*.json")) {
            string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            try {Profile(name);}catch(ArgumentException) {continue;}
            if(!names.Contains(name)) names.Add(name);
        }
        names.Sort(StringComparer.Ordinal);return names;
    }
    internal static void Backup() {
        string path=Path.Combine(Root,"original.json");
        if(!File.Exists(path)) Save(path,Settings.Read());
        else {
            Settings original=Load<Settings>(path);
            if(original==null) throw new ArgumentException("invalid backup file / settings unchanged");
        }
    }
    internal static void Apply(Settings settings,bool backup) {
        Locked(delegate {settings.Validate();if(backup) Backup();Change(settings);});
    }
    internal static void Update(Action<Settings> edit) {
        Locked(delegate {
            Settings current=Settings.Read();edit(current);current.Validate();Backup();Change(current);
        });
    }
    internal static void CommitChange(Settings before,Settings after,Action<Settings> apply,Action<Settings> saveUndo) {
        before.Validate();after.Validate();
        if(before.Same(after)) return;
        apply(after);
        try {saveUndo(before);}
        catch(Exception error) {
            try {apply(before);}
            catch(Exception rollback) {throw new IOException("undo backup failed: "+error.Message+"; rollback failed: "+rollback.Message);}
            throw new IOException("undo backup failed; previous windows settings restored: "+error.Message);
        }
    }
    private static void Change(Settings settings) {
        CommitChange(Settings.Read(),settings,delegate(Settings value) {value.Apply();},
            delegate(Settings value) {Save(Path.Combine(Root,"undo.json"),value);});
    }
    internal static void Undo() {
        Locked(delegate {
            string path=Path.Combine(Root,"undo.json");
            if(!File.Exists(path)) throw new InvalidOperationException("nothing to undo yet");
            Settings previous=Load<Settings>(path);Change(previous);
        });
    }
    internal static void SaveProfile(string name) {
        string path=Profile(name);
        Locked(delegate {Settings current=Settings.Read();current.Validate();Save(path,current);});
    }
    internal static void Locked(Action work) {
        // One user's cli instances must not interleave backup, write and rollback.
        string name="Local\\helox-settings-"+WindowsIdentity.GetCurrent().User.Value;
        using(Mutex gate=new Mutex(false,name)) {
            bool acquired=false;
            try {
                try {acquired=gate.WaitOne(5000);}catch(AbandonedMutexException) {acquired=true;}
                if(!acquired) throw new IOException("settings busy / try again");
                work();
            }finally {if(acquired) gate.ReleaseMutex();}
        }
    }
}
}
