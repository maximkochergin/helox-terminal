using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
    public double? InputHalfLifeMs {get;set;}
    public double? ScaleHalfLifeMs {get;set;}
    public bool? StabilityEnabled {get;set;}
    public double? GainLimit {get;set;}
    public bool? Enabled {get;set;}
    public bool? InputTransformed {get;set;}
}
internal sealed class AimPreset {
    internal bool Precision,Smooth,Stability;
    internal double SmoothMs=4,GainLimit=1.4,StabilityMs=8;
    internal Dictionary<string,object> ToMap() {
        return new Dictionary<string,object>{{"precision",Precision},{"smooth",Smooth},{"smoothMs",SmoothMs},{"gainLimit",GainLimit},{"stability",Stability},{"stabilityMs",StabilityMs}};
    }
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
        if(!File.Exists(path)) return new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        try {
            Dictionary<string,object> presets=Parse(File.ReadAllText(path));
            if(presets==null) throw new ArgumentException();
            Dictionary<string,object> normalized=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            foreach(KeyValuePair<string,object> entry in presets) {
                if(String.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length>199 || entry.Key.IndexOf('\0')>=0) throw new ArgumentException();
                Dictionary<string,object> preset=Map(entry.Value);
                if(preset==null) throw new ArgumentException();
                ReadPreset(preset);
                normalized.Add(entry.Key,entry.Value);
            }
            return normalized;
        }catch(Exception e) {
            if(e is IOException || e is UnauthorizedAccessException) throw;
            throw new ArgumentException("invalid saved aim preset / aim restore to reset it");
        }
    }
    internal static double SavedHalfLife(Dictionary<string,object> preset) {
        object value;if(!preset.TryGetValue("smoothMs",out value)) return 4;
        if(!(value is int) && !(value is long) && !(value is double) && !(value is decimal)) throw new ArgumentException("invalid smoothing strength");
        double ms=Convert.ToDouble(value);CheckHalfLife(ms);return ms;
    }
    internal static void CheckHalfLife(double ms) {
        if(double.IsNaN(ms) || double.IsInfinity(ms) || ms<1 || ms>12) throw new ArgumentException("smooth half-life: 1..12 ms");
    }
    private static double SavedNumber(Dictionary<string,object> preset,string key,double fallback) {
        object value;if(!preset.TryGetValue(key,out value)) return fallback;
        if(!(value is int) && !(value is long) && !(value is double) && !(value is decimal)) throw new ArgumentException("invalid "+key);
        return Convert.ToDouble(value);
    }
    internal static void CheckGain(double gain) {
        if(double.IsNaN(gain) || double.IsInfinity(gain) || gain<1.1 || gain>1.8) throw new ArgumentException("precision gain limit: 1.1..1.8x");
    }
    private static void CheckStability(double ms) {
        if(double.IsNaN(ms) || double.IsInfinity(ms) || ms<8 || ms>12) throw new ArgumentException("stability input half-life: 8..12 ms");
    }
    internal static AimPreset ReadPreset(Dictionary<string,object> saved) {
        AimPreset preset=new AimPreset();if(saved==null) return preset;
        object precision,smooth,stability;
        if(!saved.TryGetValue("precision",out precision) || !(precision is bool) || !saved.TryGetValue("smooth",out smooth) || !(smooth is bool)) throw new ArgumentException("invalid saved aim toggles");
        preset.Precision=(bool)precision;preset.Smooth=(bool)smooth;preset.SmoothMs=SavedHalfLife(saved);
        preset.GainLimit=SavedNumber(saved,"gainLimit",1.4);CheckGain(preset.GainLimit);
        if(saved.TryGetValue("stability",out stability)) {if(!(stability is bool)) throw new ArgumentException("invalid saved stability toggle");preset.Stability=(bool)stability;}
        preset.StabilityMs=SavedNumber(saved,"stabilityMs",8);CheckStability(preset.StabilityMs);return preset;
    }
    internal static RateResult RecentRate(RateResult history,string devicePath,DateTime now) {
        DateTime measured;
        if(history==null || String.IsNullOrEmpty(devicePath) || !Analysis.ValidHistory(history) || !String.Equals(history.DevicePath,devicePath,StringComparison.OrdinalIgnoreCase) ||
            !DateTime.TryParseExact(history.MeasuredUtc,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out measured) ||
            measured.ToUniversalTime()<now.ToUniversalTime().AddHours(-24) || measured.ToUniversalTime()>now.ToUniversalTime().AddMinutes(5)) return null;
        return history;
    }
    internal static double StabilityHalfLife(RateResult history,string devicePath,DateTime now) {
        RateResult recent=RecentRate(history,devicePath,now);return recent==null ? 8 : BoundedStability(recent.MedianIntervalMs);
    }
    private static double BoundedStability(double interval) {return Math.Round(Math.Max(8,Math.Min(12,interval))*2,MidpointRounding.AwayFromZero)/2;}
    private static RateResult RecentRate(Device device) {
        RateResult history=null;
        try {history=Store.Load<RateResult>(Path.Combine(Store.Root,"rate.json"));}catch {} // Invalid or inaccessible optional history uses the explicit 8 ms fallback.
        return RecentRate(history,device.Path,DateTime.UtcNow);
    }
    internal static void CheckChange(string feature,bool on,double? smoothMs,double? gainLimit) {
        if(feature!="precision" && feature!="smooth" && feature!="stability" && feature!="resume" && feature!="tracking") throw new ArgumentException("use aim precision|smooth|stability on|off or aim resume|tracking");
        if(feature=="tracking" && !on) throw new ArgumentException("use aim tracking / restore components with precision, stability and smooth");
        if(smoothMs.HasValue) {CheckHalfLife(smoothMs.Value);if(feature!="smooth" || !on) throw new ArgumentException("strength requires aim smooth on");}
        if(gainLimit.HasValue) {CheckGain(gainLimit.Value);if(feature!="precision" || !on) throw new ArgumentException("gain limit requires aim precision on");}
    }
    internal static AimPreset Resolve(AimStatus status,Dictionary<string,object> saved,string feature,bool on,double? smoothMs=null,double? gainLimit=null,double stabilityMs=8) {
        CheckChange(feature,on,smoothMs,gainLimit);CheckStability(stabilityMs);
        AimPreset preset=ReadPreset(saved);
        if(feature=="resume") {if(saved==null) throw new InvalidOperationException("no saved aim preset for this mouse / enable precision or smooth first");return preset;}
        bool own=status.Profile!=null && status.Profile.StartsWith("helox-",StringComparison.Ordinal);
        preset.Precision=own && status.Mode=="natural";preset.Smooth=own && status.OutputHalfLifeMs>0;
        if(preset.Smooth && status.OutputHalfLifeMs>=1 && status.OutputHalfLifeMs<=12) preset.SmoothMs=status.OutputHalfLifeMs.Value;
        if(preset.Precision) {
            if(status.GainLimit>=1.1 && status.GainLimit<=1.8) preset.GainLimit=status.GainLimit.Value;
            preset.Stability=status.InputHalfLifeMs>=8 && status.InputHalfLifeMs<=12 && status.ScaleHalfLifeMs==status.InputHalfLifeMs/2;
            if(preset.Stability) preset.StabilityMs=status.InputHalfLifeMs.Value;
        }
        if(feature=="precision") preset.Precision=on;
        else if(feature=="smooth") preset.Smooth=on;
        else if(feature=="tracking") {preset.Precision=true;preset.Smooth=false;preset.Stability=true;preset.StabilityMs=stabilityMs;}
        else {
            if(on && !preset.Precision) throw new InvalidOperationException("enable precision first / stability smooths acceleration only");
            preset.Stability=on;if(on) preset.StabilityMs=stabilityMs;
        }
        if(smoothMs.HasValue) preset.SmoothMs=smoothMs.Value;
        if(gainLimit.HasValue) preset.GainLimit=gainLimit.Value;
        return preset;
    }
    private static void Load() {
        if(bridge!=null) return;
        if(!Environment.Is64BitProcess) throw new InvalidOperationException("aim tools require 64-bit windows");
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) throw new InvalidOperationException("aim backend missing / aim prepare, then aim install");
        if(!File.Exists(Path.Combine(Root,"Newtonsoft.Json.dll"))) throw new InvalidOperationException("aim backend incomplete / run aim prepare and restart helox");
        Maintenance.RequireVerifiedBackend();
        Assembly.LoadFrom(Path.Combine(Root,"Newtonsoft.Json.dll"));
        bridge=Assembly.LoadFrom(Path.Combine(Root,"wrapper.dll"));
    }
    internal static object Call(object target,string type,string method,params object[] args) {
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
    internal static Dictionary<string,object> DeviceEntry(Dictionary<string,object> cfg,string id) {
        // The released callback uses wcsncmp, not Windows' usual insensitive id comparison.
        foreach(object entry in Items(cfg["devices"])) if(String.Equals((string)Map(entry)["id"],id,StringComparison.Ordinal)) return Map(entry);
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
    internal static bool? EndpointState() {
        try {
            using(Microsoft.Win32.SafeHandles.SafeFileHandle handle=Native.CreateFile(@"\\.\rawaccel",0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                if(!handle.IsInvalid) return true;
                int error=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return error==2 || error==3 ? (bool?)false : null;
            }
        }catch {return null;}
    }
    internal static string KernelVersion() {return (string)Active()["version"];}
    internal static AimResponse Response(Device device) {
        Dictionary<string,object> current=Active();string id=Id(device);RateResult history=RecentRate(device);
        AimResponse response=AimResponseTest.Run(current,id,history==null ? 8 : history.MedianIntervalMs);
        response.IntervalSource=history==null ? "8 ms example / no recent matching history" : "recent delivery median / not kernel timing readback";
        return response;
    }
    internal static AimStatus Describe(Dictionary<string,object> cfg,string id) {
            Dictionary<string,object> entry=DeviceEntry(cfg,id);
            string name=entry==null || String.IsNullOrEmpty((string)entry["profile"]) ? (string)Map(Items(cfg["profiles"])[0])["name"] : (string)entry["profile"];
            Dictionary<string,object> profile=Profile(cfg,name);
            if(profile==null) throw new InvalidOperationException("driver profile not found");
            Dictionary<string,object> config=entry==null ? Map(cfg["defaultDeviceConfig"]) : Map(entry["config"]);
            return new AimStatus {State="ready",DeviceId=id,Profile=name,Enabled=!(bool)config["disable"],Mode=(string)Map(profile[X])["mode"],
                OutputHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Output]),GainLimit=Convert.ToDouble(Map(profile[X])["limit"]),
                InputHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Input]),ScaleHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Scale]),
                StabilityEnabled=!(bool)config["disable"] && (string)Map(profile[X])["mode"]=="natural" && Convert.ToDouble(Map(profile[Speed])[Input])>=8 && Convert.ToDouble(Map(profile[Speed])[Input])<=12 && Convert.ToDouble(Map(profile[Speed])[Scale])==Convert.ToDouble(Map(profile[Speed])[Input])/2,
                InputTransformed=!(bool)config["disable"] && ((string)Map(profile[X])["mode"]!="noaccel" || (string)Map(profile["Vertical accel parameters"])["mode"]!="noaccel" || Convert.ToDouble(Map(profile[Speed])[Output])>0 ||
                    Convert.ToDouble(profile["Output DPI"])!=1000 || Convert.ToDouble(config["DPI (normalizes input speed unit: counts/ms -> in/s)"])!=0 ||
                    Convert.ToDouble(profile["Y/X output DPI ratio (vertical sens multiplier)"])!=1 || Convert.ToDouble(profile["L/R output DPI ratio (left sens multiplier)"])!=1 || Convert.ToDouble(profile["U/D output DPI ratio (up sens multiplier)"])!=1 ||
                    Convert.ToDouble(profile["Degrees of rotation"])!=0 || Convert.ToDouble(profile["Degrees of angle snapping"])!=0 || Convert.ToDouble(profile["Input Speed Cap"])>0),Note="live driver readback; profile resets on reboot"};
    }
    // Preserve defaults and other devices; only replace our selected hardware-id override.
    internal static Dictionary<string,object> Configure(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,bool precision,bool smooth,double smoothMs=4,double gainLimit=1.4,bool stability=false,double stabilityMs=8,bool enable=false) {
        CheckHalfLife(smoothMs);CheckGain(gainLimit);CheckStability(stabilityMs);
        Dictionary<string,object> cfg=Parse(Store.Json.Serialize(current));
        string name="helox-"+id.ToLowerInvariant().Replace('\\','-');
        List<object> profiles=Items(cfg["profiles"]);
        // The first profile controls unmatched devices. A shared profile belongs to other devices too.
        string baseName=name;int suffix=0;
        while(Profile(cfg,name)!=null && ((string)Map(profiles[0])["name"]==name || Items(cfg["devices"]).Exists(delegate(object d){return !String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase) && (string)Map(d)["profile"]==name;}))) name=baseName+"-"+(++suffix);
        Dictionary<string,object> profile=Map(Items(defaults["profiles"])[0]);profile=Parse(Store.Json.Serialize(profile));profile["name"]=name;
        Dictionary<string,object> accel=Map(profile[X]);accel["mode"]=precision ? "natural" : "noaccel";
        accel["Gain / Velocity"]=true;accel["inputOffset"]=3.0;accel["decayRate"]=0.05;accel["limit"]=gainLimit;
        Dictionary<string,object> speed=Map(profile[Speed]);speed[Input]=precision ? (stability ? stabilityMs : 4.0) : 0.0;speed[Scale]=precision ? (stability ? stabilityMs/2 : 2.0) : 0.0;speed[Output]=smooth ? smoothMs : 0.0;
        int index=profiles.FindIndex(delegate(object p){return (string)Map(p)["name"]==name;});
        if(index<0) profiles.Add(profile);else profiles[index]=profile;
        cfg["profiles"]=profiles.ToArray();
        Dictionary<string,object> existing=DeviceEntry(cfg,id);
        // Effect switches must retain the device's normalization and timing. Off
        // also retains bypass; only an explicit on/resume request enables it.
        Dictionary<string,object> devConfig=Parse(Store.Json.Serialize(existing==null ? cfg["defaultDeviceConfig"] : existing["config"]));
        if(enable) devConfig["disable"]=false;
        List<object> devices=Items(cfg["devices"]);
        int deviceIndex=devices.FindIndex(delegate(object d){return String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase);});
        Dictionary<string,object> updated=new Dictionary<string,object>{{"id",id},{"name","helox mouse"},{"profile",name},{"config",devConfig}};
        if(deviceIndex<0) devices.Add(updated);else devices[deviceIndex]=updated;
        cfg["devices"]=devices.ToArray();return cfg;
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
    internal static AimStatus Set(Device device,string feature,bool on,double? smoothMs=null,double? gainLimit=null) {
        CheckChange(feature,on,smoothMs,gainLimit);
        using(Mutex mutex=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=mutex.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");
                Dictionary<string,object> before=Active();string id=Id(device);AimStatus status=Describe(before,id);
                string preferences=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> presets=Saved(preferences);
                object existing;Dictionary<string,object> saved=presets.TryGetValue(id,out existing) ? Map(existing) : null;
                RateResult history=(feature=="stability" && on) || feature=="tracking" ? RecentRate(device) : null;
                AimPreset preset=Resolve(status,saved,feature,on,smoothMs,gainLimit,history==null ? 8 : BoundedStability(history.MedianIntervalMs));
                Dictionary<string,object> after=Configure(before,Defaults(),id,preset.Precision,preset.Smooth,preset.SmoothMs,preset.GainLimit,preset.Stability,preset.StabilityMs,on || feature=="resume");Validate(after);
                string backup=Path.Combine(Store.Root,"aim-before.json");
                if(File.Exists(backup)) Validate(Parse(File.ReadAllText(backup)));else Store.Save(backup,before);
                presets[id]=preset.ToMap();
                return Describe(Commit(before,after,Write,delegate {Store.Save(preferences,presets);}),id);
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static Dictionary<string,object> Commit(Dictionary<string,object> before,Dictionary<string,object> after,Func<Dictionary<string,object>,Dictionary<string,object>> write,Action save) {
        // The active configuration already verifies an unchanged request; no activation delay needed.
        if(SameValue(before,after)) {save();return before;}
        return Transaction(before,after,delegate(Dictionary<string,object> requested) {
            Dictionary<string,object> readback=write(requested);
            if(Object.ReferenceEquals(requested,after)) save();
            return readback;
        });
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
                Commit(before,after,Write,delegate {Store.Save(Path.Combine(Store.Root,"aim-presets.json"),new Dictionary<string,object>());});
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
        string exampleId="HID\\VID_145F&PID_0326";
        AimResponse averaged=AimResponseTest.Run(Configure(defaults,Defaults(),exampleId,true,true,8),exampleId,8);
        AimResponse tracking=AimResponseTest.Run(Configure(defaults,Defaults(),exampleId,true,false,8,1.4,true,8),exampleId,8);
        if(averaged.AfterFlickPeakCounts<40 || averaged.AfterFlickZeroReports<1) throw new Exception("output smoothing transient was not reproduced");
        if(tracking.AfterFlickPeakCounts>2 || tracking.AfterFlickPeakCounts<1 || tracking.AfterFlickZeroReports!=0 || tracking.FastMotionRatio<=1 || tracking.FastMotionRatio>1.401) throw new Exception("tracking recovery response failed");
        if(tracking.AfterFlickX[0]!=0 || tracking.AfterFlickY[0]<1) throw new Exception("tracking one-count turn failed");
        Dictionary<string,object> bypass=Configure(defaults,Defaults(),exampleId,true,true,8);
        Map(Map(Items(bypass["devices"])[0])["config"])["disable"]=true;
        AimResponse disabled=AimResponseTest.Run(bypass,exampleId,8);
        if(disabled.SmallMotionRatio!=1 || disabled.FastMotionRatio!=1 || disabled.AfterFlickPeakCounts!=1 || disabled.AfterFlickZeroReports!=0) throw new Exception("disabled response was not bypassed");
        Dictionary<string,object> normalized=Configure(defaults,Defaults(),exampleId,false,false);
        Map(Map(Items(normalized["devices"])[0])["config"])["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        AimResponse scaled=AimResponseTest.Run(normalized,exampleId,8);
        if(Math.Abs(scaled.SmallMotionRatio-.625)>.000001 || Math.Abs(scaled.FastMotionRatio-.625)>.000001) throw new Exception("response missed configured dpi normalization");
        Dictionary<string,object> normalizedToggle=Configure(normalized,defaults,exampleId,false,true,2);
        AimResponse stillScaled=AimResponseTest.Run(normalizedToggle,exampleId,8);
        if(Math.Abs(stillScaled.SmallMotionRatio-.625)>.000001 || Math.Abs(stillScaled.FastMotionRatio-.625)>.000001) throw new Exception("effect toggle changed configured dpi normalization");
        Dictionary<string,object> disabledToggle=Configure(bypass,defaults,exampleId,false,true,8);
        AimResponse stillBypassed=AimResponseTest.Run(disabledToggle,exampleId,8);
        if(stillBypassed.Readback.Enabled!=false || stillBypassed.SmallMotionRatio!=1 || stillBypassed.FastMotionRatio!=1 || stillBypassed.AfterFlickPeakCounts!=1 || stillBypassed.AfterFlickZeroReports!=0) throw new Exception("effect off activated a bypassed device");
        Dictionary<string,object> caseAlias=Configure(defaults,defaults,exampleId.ToLowerInvariant(),true,true,8);
        Dictionary<string,object> aliasConfig=Map(Map(Items(caseAlias["devices"])[0])["config"]);
        aliasConfig["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        aliasConfig["Polling rate Hz (keep at 0 for automatic adjustment)"]=250;
        aliasConfig["Use constant time interval based on polling rate"]=true;
        AimResponse exactId=AimResponseTest.Run(caseAlias,exampleId,8);
        if(exactId.Readback.Profile!="default" || exactId.ProcessedIntervalMs!=8 || exactId.SmallMotionRatio!=1 || exactId.FastMotionRatio!=1 || exactId.AfterFlickPeakCounts!=1 || exactId.AfterFlickZeroReports!=0) throw new Exception("response simulated an override ignored by the kernel's exact id match");
        Console.WriteLine("  flick -> 1-count turn / smooth 8 ms peak "+averaged.AfterFlickPeakCounts+" counts, "+averaged.AfterFlickZeroReports+" zero outputs / tracking peak "+tracking.AfterFlickPeakCounts+" counts, "+tracking.AfterFlickZeroReports+" zero outputs / native engine + integer carry");
        double previousFast=1;
        foreach(double limit in new double[]{1.1,1.2,1.4,1.6,1.8}) {
            cfg=Validate(Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",true,false,4,limit));
            accels=(IList)cfg.GetType().GetField("accels").GetValue(cfg);engine=accels[accels.Count-1];
            for(int i=0;i<100;i++) low=Axis(Call(engine,"ManagedAccel","Accelerate",8,0,1.0,8.0));
            for(int i=0;i<100;i++) high=Axis(Call(engine,"ManagedAccel","Accelerate",800,0,1.0,8.0))/800;
            if(Math.Abs(low-8)>.001 || high<=previousFast || high>limit+.001) throw new Exception("precision gain choice response failed");
            previousFast=high;
            Console.WriteLine("  precision limit "+limit.ToString(CultureInfo.InvariantCulture)+"x / settled fast ratio "+high.ToString("0.000",CultureInfo.InvariantCulture)+"x / native engine");
        }
        double baselineVariation=0;
        foreach(double stabilityMs in new double[]{0,8,10,12}) {
            bool stable=stabilityMs>0;
            cfg=Validate(Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",true,false,4,1.4,stable,stable ? stabilityMs : 8));
            accels=(IList)cfg.GetType().GetField("accels").GetValue(cfg);engine=accels[accels.Count-1];
            double slowRatio=0,fastRatio=0;
            for(int i=0;i<100;i++) {
                slowRatio=Axis(Call(engine,"ManagedAccel","Accelerate",40,0,1.0,8.0))/40;
                fastRatio=Axis(Call(engine,"ManagedAccel","Accelerate",120,0,1.0,8.0))/120;
                if(double.IsNaN(slowRatio) || double.IsNaN(fastRatio) || slowRatio<=0 || slowRatio>1.401 || fastRatio<=0 || fastRatio>1.401) throw new Exception("stability transient gain bounds failed");
            }
            double variation=Math.Abs(fastRatio-slowRatio);
            if(slowRatio<1 || slowRatio>1.401 || fastRatio<1 || fastRatio>1.401 || variation<=0) throw new Exception("stability gain bounds failed");
            if(!stable) baselineVariation=variation;
            else if(variation>=baselineVariation) throw new Exception("stability did not reduce alternating acceleration variation");
            Console.WriteLine("  stability "+(stable ? stabilityMs+" ms" : "off")+" / alternating ratio spread "+variation.ToString("0.000",CultureInfo.InvariantCulture)+"x / native engine");
            object turn=Call(engine,"ManagedAccel","Accelerate",-80,40,1.0,8.0);
            double tx=Axis(turn),ty=Convert.ToDouble(turn.GetType().GetProperty("Item2").GetValue(turn,null));
            if(tx>=0 || ty<=0 || Math.Abs(tx/ty+2)>.001) throw new Exception("stability changed direction");
            object stop=Call(engine,"ManagedAccel","Accelerate",0,0,1.0,8.0);
            if(Axis(stop)!=0 || Convert.ToDouble(stop.GetType().GetProperty("Item2").GetValue(stop,null))!=0) throw new Exception("stability generated motion at rest");
        }
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
        double previousVariation=double.PositiveInfinity;
        foreach(double ms in new double[]{2,4,8}) {
            cfg=Validate(Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",false,true,ms));
            accels=(IList)cfg.GetType().GetField("accels").GetValue(cfg);engine=accels[accels.Count-1];
            for(int i=0;i<100;i++) {trough=Axis(Call(engine,"ManagedAccel","Accelerate",40,0,1.0,8.0));peak=Axis(Call(engine,"ManagedAccel","Accelerate",120,0,1.0,8.0));}
            double variation=peak-trough;
            if(variation<=0 || variation>=80 || variation>=previousVariation) throw new Exception("smoothing strength response failed");
            previousVariation=variation;
            Console.WriteLine("  smooth "+ms+" ms / alternating spread "+variation.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" counts / native engine");
            reverse=Call(engine,"ManagedAccel","Accelerate",-80,40,1.0,8.0);
            rx=Axis(reverse);ry=Convert.ToDouble(reverse.GetType().GetProperty("Item2").GetValue(reverse,null));
            if(rx>=0 || ry<=0 || Math.Abs(rx/ry+2)>.001) throw new Exception("smoothing strength changed direction");
            Dictionary<string,object> tuned=Configure(defaults,Defaults(),"HID\\VID_145F&PID_0326",true,true,ms);
            if(Describe(tuned,"HID\\VID_145F&PID_0326").OutputHalfLifeMs!=ms || !SameValue(defaults["defaultDeviceConfig"],tuned["defaultDeviceConfig"])) throw new Exception("smoothing strength configuration failed");
        }
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
        Console.WriteLine("  passed / official aim engine: gain choices, steadier acceleration, smoother output, direction preserved; no driver writes");
    }
    private static double Axis(object pair) {return Convert.ToDouble(pair.GetType().GetProperty("Item1").GetValue(pair,null));}
}
}
