using System;
using System.IO;
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
        string temp=path+".tmp"; File.WriteAllText(temp,Json.Serialize(value));
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
    internal static T Load<T>(string path) { return Json.Deserialize<T>(File.ReadAllText(path)); }
    internal static string Profile(string name) {
        if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[a-z0-9][a-z0-9_-]{0,31}$")) throw new ArgumentException("profile name: 1..32 lowercase letters, numbers, underscores or hyphens");
        return Path.Combine(Root,"profiles",name+".json");
    }
    internal static void Backup() {
        string path=Path.Combine(Root,"original.json");
        if(!File.Exists(path)) Save(path,Settings.Read());
    }
}
}
