using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Helox {
public sealed class AimStatus {
    public string State {get;set;}
    public string Note {get;set;}
    public string DeviceId {get;set;}
    public string Profile {get;set;}
    public string Mode {get;set;}
    public double? OutputHalfLifeMs {get;set;}
    public double? GainLimit {get;set;}
    public bool? Enabled {get;set;}
    public bool? InputTransformed {get;set;}
}
internal static class Aim {
    internal static readonly string Root=Path.Combine(Store.Root,"rawaccel-1.7.1","RawAccel");
    private static Assembly bridge;
    private const string Speed="Input speed calculation parameters";
    private const string Output="Time in ms after which an output is weighted at half its original value.";
    private const string Input="Time in ms after which an input is weighted at half its original value.";
    private const string Scale="Time in ms after which scale is weighted at half its original value.";
    private const string X="Whole or horizontal accel parameters";
    internal static Dictionary<string,object> Saved(string path) {
        if(!File.Exists(path)) return new Dictionary<string,object>();
        try {
            Dictionary<string,object> presets=Parse(File.ReadAllText(path));
            if(presets==null) throw new ArgumentException();
            foreach(KeyValuePair<string,object> entry in presets) {
                Dictionary<string,object> preset=Map(entry.Value);object precision,smooth;
                if(preset==null || !preset.TryGetValue("precision",out precision) || !(precision is bool) || !preset.TryGetValue("smooth",out smooth) || !(smooth is bool)) throw new ArgumentException();
            }
            return presets;
        }catch(Exception e) {
            if(e is IOException || e is UnauthorizedAccessException) throw;
            throw new ArgumentException("invalid saved aim preset / aim restore to reset it");
        }
    }
    private static void Load() {
        if(bridge!=null) return;
        if(!Environment.Is64BitProcess) throw new InvalidOperationException("aim tools require 64-bit windows");
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) throw new InvalidOperationException("aim backend missing / aim prepare, then aim install");
        Assembly.LoadFrom(Path.Combine(Root,"Newtonsoft.Json.dll"));
        bridge=Assembly.LoadFrom(Path.Combine(Root,"wrapper.dll"));
    }
    private static object Call(object target,string type,string method,params object[] args) {
        Load();
        try {return bridge.GetType(type,true).GetMethod(method).Invoke(target,args);}
        catch(TargetInvocationException e) {throw e.InnerException ?? e;}
    }
    internal static Dictionary<string,object> Map(object value) {return (Dictionary<string,object>)value;}
    internal static List<object> Items(object value) {return new List<object>((IEnumerable<object>)value);}
    internal static Dictionary<string,object> Parse(string text) {return Map(Store.Json.DeserializeObject(text));}
    private static string Text(object cfg) {return (string)Call(cfg,"DriverConfig","ToJSON");}
    internal static Dictionary<string,object> Defaults() {return Parse(Text(Call(null,"DriverConfig","GetDefault")));}
    private static Dictionary<string,object> Active() {
        Call(null,"VersionHelper","ValidOrThrow");
        return Parse(Text(Call(null,"DriverConfig","GetActive")));
    }
    internal static object Validate(Dictionary<string,object> cfg) {
        AimConfigGuard.Check(cfg);
        object result=Call(null,"DriverConfig","Convert",Store.Json.Serialize(cfg));
        string error=(string)result.GetType().GetProperty("Item2").GetValue(result,null);
        if(error!=null) throw new ArgumentException("invalid aim settings / "+error);
        return result.GetType().GetProperty("Item1").GetValue(result,null);
    }
    private static string Id(Device device) {
        if(device==null) throw new InvalidOperationException("choose a connected mouse first");
        IEnumerable list=(IEnumerable)Call(null,"MultiHandleDevice","GetList");
        foreach(object item in list) {
            IEnumerable handles=(IEnumerable)item.GetType().GetField("handles").GetValue(item);
            foreach(IntPtr handle in handles) if(handle==device.Handle) return (string)item.GetType().GetField("id").GetValue(item);
        }
        throw new InvalidOperationException("mouse identity changed / reconnect and retry");
    }
    private static Dictionary<string,object> DeviceEntry(Dictionary<string,object> cfg,string id) {
        foreach(object entry in Items(cfg["devices"])) if(String.Equals((string)Map(entry)["id"],id,StringComparison.OrdinalIgnoreCase)) return Map(entry);
        return null;
    }
    private static Dictionary<string,object> Profile(Dictionary<string,object> cfg,string name) {
        foreach(object entry in Items(cfg["profiles"])) if((string)Map(entry)["name"]==name) return Map(entry);
        return null;
    }
    internal static AimStatus Read(Device device) {
        bool installed=DriverPresent();
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) return new AimStatus {State=installed ? "unavailable" : "not installed",InputTransformed=installed ? (bool?)null : false,Note="8 aim tools / install backend"};
        if(device==null) return new AimStatus {State="unavailable",InputTransformed=installed ? (bool?)null : false,Note="choose a connected mouse in 6 > 1 / install and undo remain available"};
        try {
            return Describe(Active(),Id(device));
        }catch(Exception e) {return new AimStatus {State="unavailable",InputTransformed=installed ? (bool?)null : false,Note=e.Message.ToLowerInvariant()+" / install or restart if pending"};}
    }
    private static bool DriverPresent() {
        try {
            using(Microsoft.Win32.RegistryKey key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\rawaccel")) if(key!=null) return true;
            // A driver may remain loaded after its service is removed, until the next restart.
            using(Microsoft.Win32.SafeHandles.SafeFileHandle handle=Native.CreateFile(@"\\.\rawaccel",0,0,IntPtr.Zero,3,0,IntPtr.Zero)) {
                if(!handle.IsInvalid) return true;
                int error=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return EndpointMayExist(false,error);
            }
        }catch {return true;} // An unreadable state cannot be treated as an unfiltered mouse.
    }
    internal static bool EndpointMayExist(bool opened,int error) {return opened || (error!=2 && error!=3);}
    internal static AimStatus Describe(Dictionary<string,object> cfg,string id) {
            Dictionary<string,object> entry=DeviceEntry(cfg,id);
            string name=entry==null || String.IsNullOrEmpty((string)entry["profile"]) ? (string)Map(Items(cfg["profiles"])[0])["name"] : (string)entry["profile"];
            Dictionary<string,object> profile=Profile(cfg,name);
            if(profile==null) throw new InvalidOperationException("driver profile not found");
            Dictionary<string,object> config=entry==null ? Map(cfg["defaultDeviceConfig"]) : Map(entry["config"]);
            return new AimStatus {State="ready",DeviceId=id,Profile=name,Enabled=!(bool)config["disable"],Mode=(string)Map(profile[X])["mode"],
                OutputHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Output]),GainLimit=Convert.ToDouble(Map(profile[X])["limit"]),
                InputTransformed=!(bool)config["disable"] && ((string)Map(profile[X])["mode"]!="noaccel" || (string)Map(profile["Vertical accel parameters"])["mode"]!="noaccel" || Convert.ToDouble(Map(profile[Speed])[Output])>0 ||
                    Convert.ToDouble(profile["Output DPI"])!=1000 || Convert.ToDouble(config["DPI (normalizes input speed unit: counts/ms -> in/s)"])!=0 ||
                    Convert.ToDouble(profile["Y/X output DPI ratio (vertical sens multiplier)"])!=1 || Convert.ToDouble(profile["L/R output DPI ratio (left sens multiplier)"])!=1 || Convert.ToDouble(profile["U/D output DPI ratio (up sens multiplier)"])!=1 ||
                    Convert.ToDouble(profile["Degrees of rotation"])!=0 || Convert.ToDouble(profile["Degrees of angle snapping"])!=0 || Convert.ToDouble(profile["Input Speed Cap"])>0),Note="live driver readback; profile resets on reboot"};
    }
    // Preserve defaults and other devices; only replace our selected hardware-id override.
    internal static Dictionary<string,object> Configure(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,bool precision,bool smooth) {
        Dictionary<string,object> cfg=Parse(Store.Json.Serialize(current));
        string name="helox-"+id.ToLowerInvariant().Replace('\\','-');
        List<object> profiles=Items(cfg["profiles"]);
        // The first profile controls unmatched devices. A shared profile belongs to other devices too.
        string baseName=name;int suffix=0;
        while(Profile(cfg,name)!=null && ((string)Map(profiles[0])["name"]==name || Items(cfg["devices"]).Exists(delegate(object d){return !String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase) && (string)Map(d)["profile"]==name;}))) name=baseName+"-"+(++suffix);
        Dictionary<string,object> profile=Map(Items(defaults["profiles"])[0]);profile=Parse(Store.Json.Serialize(profile));profile["name"]=name;
        Dictionary<string,object> accel=Map(profile[X]);accel["mode"]=precision ? "natural" : "noaccel";
        accel["Gain / Velocity"]=true;accel["inputOffset"]=3.0;accel["decayRate"]=0.05;accel["limit"]=1.4;
        Dictionary<string,object> speed=Map(profile[Speed]);speed[Input]=precision ? 4.0 : 0.0;speed[Scale]=precision ? 2.0 : 0.0;speed[Output]=smooth ? 4.0 : 0.0;
        int index=profiles.FindIndex(delegate(object p){return (string)Map(p)["name"]==name;});
        if(index<0) profiles.Add(profile);else profiles[index]=profile;
        cfg["profiles"]=profiles.ToArray();
        Dictionary<string,object> devConfig=Parse(Store.Json.Serialize(defaults["defaultDeviceConfig"]));devConfig["disable"]=false;
        List<object> devices=Items(cfg["devices"]);devices.RemoveAll(delegate(object d){return String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase);});
        devices.Add(new Dictionary<string,object>{{"id",id},{"name","helox mouse"},{"profile",name},{"config",devConfig}});cfg["devices"]=devices.ToArray();return cfg;
    }
    internal static bool SameValue(object a,object b) {
        Dictionary<string,object> left=a as Dictionary<string,object>,right=b as Dictionary<string,object>;
        if(left!=null || right!=null) {
            if(left==null || right==null || left.Count!=right.Count) return false;
            foreach(KeyValuePair<string,object> pair in left) {object value;if(!right.TryGetValue(pair.Key,out value) || !SameValue(pair.Value,value)) return false;}return true;
        }
        object[] la=a as object[],ra=b as object[];
        if(la!=null || ra!=null) {if(la==null || ra==null || la.Length!=ra.Length) return false;for(int i=0;i<la.Length;i++) if(!SameValue(la[i],ra[i])) return false;return true;}
        if(a is IConvertible && b is IConvertible && !(a is string) && !(b is string) && !(a is bool) && !(b is bool)) return Convert.ToDouble(a)==Convert.ToDouble(b);
        return Object.Equals(a,b);
    }
    private static Dictionary<string,object> Write(Dictionary<string,object> cfg) {
        object valid=Validate(cfg);Call(valid,"DriverConfig","Activate");
        // Driver deliberately delays updates by one second. Never verify against the queued write.
        Thread.Sleep(1200);
        Dictionary<string,object> read=Active();
        Dictionary<string,object> expected=Parse(Text(valid));
        foreach(string key in new string[]{"defaultDeviceConfig","profiles","devices"})
            if(!SameValue(read[key],expected[key])) throw new InvalidOperationException("aim driver readback mismatch");
        return read;
    }
    internal static Dictionary<string,object> Transaction(Dictionary<string,object> before,Dictionary<string,object> after,Func<Dictionary<string,object>,Dictionary<string,object>> write) {
        try {return write(after);}catch(Exception error) {
            try {write(before);}catch(Exception rollback) {throw new IOException("aim apply failed: "+error.Message+"; rollback failed: "+rollback.Message);}
            throw new IOException("aim apply failed; previous driver settings restored: "+error.Message);
        }
    }
    internal static AimStatus Set(Device device,string feature,bool on) {
        using(Mutex mutex=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=mutex.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");
                Dictionary<string,object> before=Active();string id=Id(device);AimStatus status=Describe(before,id);
                bool precision=status.Profile!=null && status.Profile.StartsWith("helox-") && status.Mode=="natural";
                bool smooth=status.Profile!=null && status.Profile.StartsWith("helox-") && status.OutputHalfLifeMs>0;
                string preferences=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> presets=Saved(preferences);
                if(feature=="resume") {
                    object saved;if(!presets.TryGetValue(id,out saved)) throw new InvalidOperationException("no saved aim preset for this mouse / enable precision or smooth first");
                    precision=(bool)Map(saved)["precision"];smooth=(bool)Map(saved)["smooth"];
                } else if(feature=="precision") precision=on;else if(feature=="smooth") smooth=on;else throw new ArgumentException("use aim resume or aim precision|smooth on|off");
                Dictionary<string,object> after=Configure(before,Defaults(),id,precision,smooth);Validate(after);
                string backup=Path.Combine(Store.Root,"aim-before.json");
                if(File.Exists(backup)) Validate(Parse(File.ReadAllText(backup)));else Store.Save(backup,before);
                presets[id]=new Dictionary<string,object>{{"precision",precision},{"smooth",smooth}};
                return Describe(Transaction(before,after,delegate(Dictionary<string,object> requested) {
                    Dictionary<string,object> readback=Write(requested);
                    if(Object.ReferenceEquals(requested,after)) Store.Save(preferences,presets);
                    return readback;
                }),id);
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static void Restore() {
        string path=Path.Combine(Store.Root,"aim-before.json");
        if(!File.Exists(path)) throw new InvalidOperationException("no aim backup yet");
        using(Mutex mutex=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=mutex.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");
                Dictionary<string,object> before=Active();
                Dictionary<string,object> after=Parse(File.ReadAllText(path));Validate(after);
                Transaction(before,after,delegate(Dictionary<string,object> requested) {
                    Dictionary<string,object> readback=Write(requested);
                    if(Object.ReferenceEquals(requested,after)) Store.Save(Path.Combine(Store.Root,"aim-presets.json"),new Dictionary<string,object>());
                    return readback;
                });
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static void TestEngine() {
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) {Console.WriteLine("  aim engine tests skipped / aim prepare first");return;}
        Dictionary<string,object> defaults=Defaults();
        foreach(Device device in Device.List()) if(device.TrustCandidate) {
            if(Id(device).IndexOf("VID_145F&PID_0326",StringComparison.OrdinalIgnoreCase)<0) throw new Exception("aim hardware id mapping failed");
        }
        Dictionary<string,object> precision=Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",true,false);
        object cfg=Validate(precision);
        IList accels=(IList)cfg.GetType().GetField("accels").GetValue(cfg);object engine=accels[accels.Count-1];
        double low=0,high=0;
        for(int i=0;i<100;i++) low=Axis(Call(engine,"ManagedAccel","Accelerate",8,0,1.0,8.0));
        for(int i=0;i<100;i++) high=Axis(Call(engine,"ManagedAccel","Accelerate",800,0,1.0,8.0));
        if(Math.Abs(low-8)>.001 || high<=800 || high>800*1.401) throw new Exception("precision engine response failed");
        cfg=Validate(Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",false,true));
        accels=(IList)cfg.GetType().GetField("accels").GetValue(cfg);engine=accels[accels.Count-1];
        double peak=0,trough=0;
        for(int i=0;i<100;i++) {
            trough=Axis(Call(engine,"ManagedAccel","Accelerate",40,0,1.0,8.0));
            peak=Axis(Call(engine,"ManagedAccel","Accelerate",120,0,1.0,8.0));
        }
        if(peak-trough>=80 || peak<=trough) throw new Exception("smoothing engine response failed");
        object reverse=Call(engine,"ManagedAccel","Accelerate",-80,40,1.0,8.0);
        double rx=Axis(reverse),ry=Convert.ToDouble(reverse.GetType().GetProperty("Item2").GetValue(reverse,null));
        if(rx>=0 || ry<=0 || Math.Abs(rx/ry+2)>.001) throw new Exception("smoothing changed direction");
        Dictionary<string,object> again=Configure(precision,Defaults(),"HID\\VID_145F&PID_0326",true,true);
        if(Items(again["profiles"]).Count!=2 || Items(again["devices"]).Count!=1 || Store.Json.Serialize(again["defaultDeviceConfig"])!=Store.Json.Serialize(defaults["defaultDeviceConfig"])) throw new Exception("aim scope or repeated update failed");
        Validate(again);
        Dictionary<string,object> unrelated=Configure(defaults,Defaults(),"HID\\VID_0001&PID_0002",true,true);
        string originalDevice=Store.Json.Serialize(Items(unrelated["devices"])[0]),originalProfile=Store.Json.Serialize(Items(unrelated["profiles"])[1]);
        Dictionary<string,object> scoped=Configure(unrelated,Defaults(),"HID\\VID_145F&PID_0326",true,false);
        if(Items(scoped["devices"]).Count!=2 || Items(scoped["profiles"]).Count!=3 || Store.Json.Serialize(Items(scoped["devices"])[0])!=originalDevice || Store.Json.Serialize(Items(scoped["profiles"])[1])!=originalProfile) throw new Exception("unrelated aim settings overwritten");
        Validate(scoped);
        Dictionary<string,object> reordered=Parse(Store.Json.Serialize(precision));
        List<object> reorderedProfiles=Items(reordered["profiles"]);reordered["profiles"]=new object[]{reorderedProfiles[1],reorderedProfiles[0]};
        Dictionary<string,object> protectedDefault=Configure(reordered,Defaults(),"HID\\VID_145F&PID_0326",false,true);
        if(!SameValue(Items(reordered["profiles"])[0],Items(protectedDefault["profiles"])[0]) || Items(protectedDefault["profiles"]).Count!=3) throw new Exception("default aim profile changed");
        Validate(protectedDefault);
        Dictionary<string,object> shared=Parse(Store.Json.Serialize(precision));
        Dictionary<string,object> peer=Parse(Store.Json.Serialize(Items(shared["devices"])[0]));peer["id"]="HID\\VID_0001&PID_0002";
        shared["devices"]=new object[]{Items(shared["devices"])[0],peer};
        Dictionary<string,object> protectedShared=Configure(shared,Defaults(),"HID\\VID_145F&PID_0326",false,true);
        if(!SameValue(Items(shared["profiles"])[1],Items(protectedShared["profiles"])[1]) || Items(protectedShared["profiles"]).Count!=3) throw new Exception("shared aim profile changed");
        Validate(protectedShared);
        Dictionary<string,object> capped=Defaults();Map(Items(capped["profiles"])[0])["Input Speed Cap"]=10;
        if(Describe(capped,"HID\\VID_145F&PID_0326").InputTransformed!=true) throw new Exception("dpi filter misses input speed cap");
        Console.WriteLine("  passed / official aim engine: slow 1x, fast <=1.4x, smoother output, direction preserved; no driver writes");
    }
    private static double Axis(object pair) {return Convert.ToDouble(pair.GetType().GetProperty("Item1").GetValue(pair,null));}
}
}
