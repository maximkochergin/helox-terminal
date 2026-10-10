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
    public double? SnapDegrees {get;set;}
    public double[] LookupData {get;set;}
    public bool? LookupIsSensitivity {get;set;}
    public string CurveSpeedUnit {get;set;}
    public bool? LookupLayoutCompatible {get;set;}
    public AimDirections Directions {get;set;}
    public bool? DampingEnabled {get;set;}
    public double? DampingLowScale {get;set;}
    public double? DampingRecoverySpeed {get;set;}
    public bool? LookupInputSmoothingRisk {get;set;}
}
internal sealed class AimPreset {
    internal bool Precision,Smooth,Stability;
    internal double SmoothMs=4,GainLimit=1.4,StabilityMs=8;
    internal double SnapDegrees;
    internal double SnapStrength=1;
    internal AimCurve Curve;
    internal AimDirections Directions=new AimDirections();
    internal AimDamping Damping=new AimDamping();
    internal string SpeedUnit;
    internal Dictionary<string,object> ToMap() {
        return new Dictionary<string,object>{{"precision",Precision},{"smooth",Smooth},{"smoothMs",SmoothMs},{"gainLimit",GainLimit},{"stability",Stability},{"stabilityMs",StabilityMs},{"snapDegrees",SnapDegrees},{"snapStrength",SnapStrength},{"curve",Curve==null ? null : Curve.ToMap()},{"directions",Directions.ToMap()},{"damping",Damping.ToMap()},{"speedUnit",SpeedUnit}};
    }
}
internal static partial class Aim {
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
            return ValidateSaved(Parse(File.ReadAllText(path)));
        }catch(Exception e) {
            if(e is IOException || e is UnauthorizedAccessException) throw;
            throw new ArgumentException("invalid saved aim preset / aim restore to reset it");
        }
    }
    internal static Dictionary<string,object> ValidateSaved(Dictionary<string,object> presets) {
        if(presets==null) throw new ArgumentException("invalid saved aim preset");
        Dictionary<string,object> normalized=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        foreach(KeyValuePair<string,object> entry in presets) {
            if(String.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length>199 || entry.Key.IndexOf('\0')>=0) throw new ArgumentException("invalid saved aim identity");
            Dictionary<string,object> preset=Map(entry.Value);
            if(preset==null) throw new ArgumentException("invalid saved aim preset");
            ReadPreset(preset);normalized.Add(entry.Key,entry.Value);
        }
        return normalized;
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
    internal static void CheckSnap(double degrees) {
        if(double.IsNaN(degrees) || double.IsInfinity(degrees) || degrees<0 || degrees>5) throw new ArgumentException("snap angle: 0..5 degrees / helox limit, not riot approval");
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
        preset.StabilityMs=SavedNumber(saved,"stabilityMs",8);CheckStability(preset.StabilityMs);
        preset.SnapDegrees=SavedNumber(saved,"snapDegrees",0);CheckSnap(preset.SnapDegrees);
        preset.SnapStrength=SavedNumber(saved,"snapStrength",preset.SnapDegrees>0 ? preset.SnapDegrees : 1);CheckSnap(preset.SnapStrength);if(preset.SnapStrength==0) throw new ArgumentException("saved snap strength must be positive");
        object curve;if(saved.TryGetValue("curve",out curve) && curve!=null) preset.Curve=AimCurve.FromMap(curve);
        object directions,damping;if(saved.TryGetValue("directions",out directions)) preset.Directions=AimDirections.FromMap(directions);
        if(saved.TryGetValue("damping",out damping)) preset.Damping=AimDamping.FromMap(damping);
        object unit;if(saved.TryGetValue("speedUnit",out unit) && unit!=null) {if(!(unit is string) || ((string)unit!="counts/ms" && (string)unit!="in/s")) throw new ArgumentException("invalid saved speed unit");preset.SpeedUnit=(string)unit;}
        return preset;
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
        if(feature!="precision" && feature!="smooth" && feature!="stability" && feature!="resume" && feature!="tracking" && feature!="curve" && feature!="snap" && feature!="directions" && feature!="damp") throw new ArgumentException("use aim precision|smooth|stability|snap|damp on|off, curve|directions or resume|tracking");
        if(feature=="tracking" && !on) throw new ArgumentException("use aim tracking / restore components with precision, stability and smooth");
        if(smoothMs.HasValue) {CheckHalfLife(smoothMs.Value);if(feature!="smooth" || !on) throw new ArgumentException("strength requires aim smooth on");}
        if(gainLimit.HasValue) {CheckGain(gainLimit.Value);if(feature!="precision" || !on) throw new ArgumentException("gain limit requires aim precision on");}
    }
    internal static AimPreset Resolve(AimStatus status,Dictionary<string,object> saved,string feature,bool on,double? smoothMs=null,double? gainLimit=null,double stabilityMs=8,AimCurve curve=null,double? snap=null,AimDirections directions=null,AimDamping damping=null) {
        CheckChange(feature,on,smoothMs,gainLimit);CheckStability(stabilityMs);
        if(curve!=null) {curve.Check();if(feature!="curve") throw new ArgumentException("curve definition requires curve apply");}
        if(snap.HasValue) {CheckSnap(snap.Value);if(feature!="snap" || !on) throw new ArgumentException("angle requires aim snap on");}
        if(directions!=null) {directions.Check();if(feature!="directions") throw new ArgumentException("direction weights require directions apply");}
        if(damping!=null) {damping.Check();if(feature!="damp" || !on) throw new ArgumentException("micro strength requires damp on");}
        AimPreset preset=ReadPreset(saved);
        if(feature=="resume") {if(saved==null) throw new InvalidOperationException("no saved aim preset for this mouse / apply a curve or game setup first");if(preset.Curve!=null || preset.Damping.Active) CheckUnit(status,preset.SpeedUnit);return preset;}
        bool own=status.Profile!=null && status.Profile.StartsWith("helox-",StringComparison.Ordinal);
        preset.Precision=own && (status.Mode=="natural" || status.Mode=="lut");preset.Smooth=own && status.OutputHalfLifeMs>0;
        if(own && status.Mode=="lut" && feature!="curve" && !gainLimit.HasValue) {
            CheckUnit(status,preset.SpeedUnit);
            if(status.LookupIsSensitivity!=true || status.LookupLayoutCompatible!=true || !AimLookup.Matches(preset,status.LookupData)) throw new InvalidOperationException("custom curve changed / apply a curve, resume saved or bypass on");
        }else if(own && status.Mode=="natural") {preset.Curve=null;preset.Damping.Enabled=false;}
        if(own && status.SnapDegrees.HasValue) preset.SnapDegrees=status.SnapDegrees.Value;
        if(preset.SnapDegrees>0 && preset.SnapDegrees<=5) preset.SnapStrength=preset.SnapDegrees;
        if(own && status.Directions!=null) preset.Directions=status.Directions.Copy();
        if(preset.Smooth && status.OutputHalfLifeMs>=1 && status.OutputHalfLifeMs<=12) preset.SmoothMs=status.OutputHalfLifeMs.Value;
        if(preset.Precision) {
            if(status.GainLimit>=1.1 && status.GainLimit<=1.8) preset.GainLimit=status.GainLimit.Value;
            preset.Stability=status.InputHalfLifeMs>=8 && status.InputHalfLifeMs<=12 && status.ScaleHalfLifeMs==status.InputHalfLifeMs/2;
            if(preset.Stability) preset.StabilityMs=status.InputHalfLifeMs.Value;
            else if(status.Mode=="lut" && status.InputHalfLifeMs==0 && status.ScaleHalfLifeMs>=4 && status.ScaleHalfLifeMs<=6) {preset.Stability=true;preset.StabilityMs=status.ScaleHalfLifeMs.Value*2;}
        }
        if(feature=="precision") preset.Precision=on;
        else if(feature=="smooth") preset.Smooth=on;
        else if(feature=="tracking") {preset.Precision=true;preset.Smooth=false;preset.Stability=true;preset.StabilityMs=stabilityMs;}
        else if(feature=="curve") {
            preset.Precision=true;preset.Curve=curve==null ? null : curve.Copy();
            // Explicitly rebuilding in a new unit must not carry an incompatible micro threshold.
            if(preset.SpeedUnit!=null && preset.SpeedUnit!=status.CurveSpeedUnit) preset.Damping=new AimDamping();
        }
        else if(feature=="snap") {preset.SnapDegrees=on ? (snap ?? preset.SnapStrength) : 0;if(preset.SnapDegrees>0) preset.SnapStrength=preset.SnapDegrees;}
        else if(feature=="directions") preset.Directions=directions==null ? new AimDirections() : directions.Copy();
        else if(feature=="damp") {
            if(on && !preset.Precision) throw new InvalidOperationException("enable precision or a curve first / micro damping uses the acceleration speed estimate");
            if(damping!=null) preset.Damping=damping.Copy();preset.Damping.Enabled=on;
        }
        else {
            if(on && !preset.Precision) throw new InvalidOperationException("enable precision first / stability smooths acceleration only");
            preset.Stability=on;if(on) preset.StabilityMs=stabilityMs;
        }
        if(smoothMs.HasValue) preset.SmoothMs=smoothMs.Value;
        if(gainLimit.HasValue) {preset.GainLimit=gainLimit.Value;preset.Curve=null;}
        bool newMicro=feature=="damp" && on && damping!=null && preset.Curve==null;
        if(newMicro) preset.SpeedUnit=status.CurveSpeedUnit;
        if(preset.Precision && ((feature!="curve" && preset.Curve!=null) || preset.Damping.Active)) CheckUnit(status,preset.SpeedUnit);
        if(feature=="curve" || newMicro || preset.SpeedUnit==null) preset.SpeedUnit=status.CurveSpeedUnit;
        preset.Directions.Check();CheckSnap(preset.SnapDegrees);
        return preset;
    }
    private static double[] ToDoubles(object[] values) {double[] result=new double[values.Length];for(int i=0;i<values.Length;i++) result[i]=Convert.ToDouble(values[i]);return result;}
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
    internal static Dictionary<string,object> Active() {
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
    internal static Dictionary<string,object> Canonical(Dictionary<string,object> cfg) {return Parse(Text(Validate(cfg)));}
    internal static string Id(Device device) {
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
    internal static Dictionary<string,object> EffectiveProfile(Dictionary<string,object> cfg,string id) {
        Dictionary<string,object> entry=DeviceEntry(cfg,id);
        string name=entry==null || String.IsNullOrEmpty((string)entry["profile"]) ? (string)Map(Items(cfg["profiles"])[0])["name"] : (string)entry["profile"];
        Dictionary<string,object> profile=Profile(cfg,name);
        if(profile==null) throw new InvalidOperationException("driver profile not found");
        Dictionary<string,object> result=Parse(Store.Json.Serialize(profile));result.Remove("name");return result;
    }
    internal static void RequireSmoothingOnly(Dictionary<string,object> before,Dictionary<string,object> after,string id,bool tail=false) {
        Dictionary<string,object> left=EffectiveProfile(before,id),right=EffectiveProfile(after,id);
        // Output averaging is independent. Only explicit flick-tail repair may also change input averaging.
        Map(left[Speed]).Remove(Output);Map(right[Speed]).Remove(Output);
        if(tail) {Map(left[Speed]).Remove(Input);Map(right[Speed]).Remove(Input);}
        if(!SameValue(left,right)) throw new InvalidOperationException("smoothing would replace custom curve settings / use the original profile editor, or explicitly apply a helox curve or game preset");
    }
    internal static AimStatus Read(Device device) {
        bool installed=DriverPresent();
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) return new AimStatus {State=installed ? "unavailable" : "not installed",InputTransformed=installed ? (bool?)null : false,Note="2 tune mouse > 3 driver / install backend"};
        if(device==null) return new AimStatus {State="unavailable",InputTransformed=installed ? (bool?)null : false,Note="choose a connected mouse in 6 > 1 / install and undo remain available"};
        try {
            AimStatus status=Describe(Active(),Id(device));
            DescribeSavedControls(status);
            if(File.Exists(LiveVerify.RecoveryPath)) status.Note+=" / unfinished verification: aim verify restore";
            if(GameRecovery.Pending) status.Note+=" / unfinished game preset: preset recover";
            return status;
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
        DescribeSavedControls(response.Readback);
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
                Directions=AimDirections.Read(profile),DampingEnabled=(string)Map(profile[X])["mode"]=="lut" ? (bool?)null : false,
                LookupInputSmoothingRisk=(string)Map(profile[X])["mode"]=="lut" && Convert.ToDouble(Map(profile[Speed])[Input])>0,
                LookupLayoutCompatible=(bool)Map(profile[Speed])["Whole/combined accel (set false for 'by component' mode)"] && Convert.ToDouble(Map(profile[Speed])["lpNorm"])==2 &&
                    Convert.ToDouble(Map(profile["Stretches domain for horizontal vs vertical inputs"])["x"])==1 && Convert.ToDouble(Map(profile["Stretches domain for horizontal vs vertical inputs"])["y"])==1 &&
                    Convert.ToDouble(Map(profile["Stretches accel range for horizontal vs vertical inputs"])["x"])==1 && Convert.ToDouble(Map(profile["Stretches accel range for horizontal vs vertical inputs"])["y"])==1 && Convert.ToDouble(profile["Degrees of rotation"])==0 && Convert.ToDouble(profile["Input Speed Cap"])==0,
                SnapDegrees=Convert.ToDouble(profile["Degrees of angle snapping"]),LookupData=(string)Map(profile[X])["mode"]=="lut" ? ToDoubles((object[])Map(profile[X])["data"]) : null,
                LookupIsSensitivity=(string)Map(profile[X])["mode"]=="lut" ? (bool?)!(bool)Map(profile[X])["Gain / Velocity"] : null,
                CurveSpeedUnit=Convert.ToDouble(config["DPI (normalizes input speed unit: counts/ms -> in/s)"])>0 ? "in/s" : "counts/ms",
                OutputHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Output]),GainLimit=(string)Map(profile[X])["mode"]=="lut" ? null : (double?)Convert.ToDouble(Map(profile[X])["limit"]),
                InputHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Input]),ScaleHalfLifeMs=Convert.ToDouble(Map(profile[Speed])[Scale]),
                StabilityEnabled=!(bool)config["disable"] && ((string)Map(profile[X])["mode"]=="natural" || (string)Map(profile[X])["mode"]=="lut") &&
                    ((Convert.ToDouble(Map(profile[Speed])[Input])>=8 && Convert.ToDouble(Map(profile[Speed])[Input])<=12 && Convert.ToDouble(Map(profile[Speed])[Scale])==Convert.ToDouble(Map(profile[Speed])[Input])/2) ||
                    ((string)Map(profile[X])["mode"]=="lut" && Convert.ToDouble(Map(profile[Speed])[Input])==0 && Convert.ToDouble(Map(profile[Speed])[Scale])>=4 && Convert.ToDouble(Map(profile[Speed])[Scale])<=6)),
                InputTransformed=!(bool)config["disable"] && ((string)Map(profile[X])["mode"]!="noaccel" || (string)Map(profile["Vertical accel parameters"])["mode"]!="noaccel" || Convert.ToDouble(Map(profile[Speed])[Output])>0 ||
                    Convert.ToDouble(profile["Output DPI"])!=1000 || Convert.ToDouble(config["DPI (normalizes input speed unit: counts/ms -> in/s)"])!=0 ||
                    Convert.ToDouble(profile["Y/X output DPI ratio (vertical sens multiplier)"])!=1 || Convert.ToDouble(profile["L/R output DPI ratio (left sens multiplier)"])!=1 || Convert.ToDouble(profile["U/D output DPI ratio (up sens multiplier)"])!=1 ||
                    Convert.ToDouble(profile["Degrees of rotation"])!=0 || Convert.ToDouble(profile["Degrees of angle snapping"])!=0 || Convert.ToDouble(profile["Input Speed Cap"])>0),Note="live driver readback; profile resets on reboot"};
    }
    // Preserve defaults and other devices; only replace our selected hardware-id override.
    internal static Dictionary<string,object> Configure(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,bool precision,bool smooth,double smoothMs=4,double gainLimit=1.4,bool stability=false,double stabilityMs=8,bool enable=false,AimCurve curve=null,double snap=0,AimDirections directions=null,AimDamping damping=null) {
        CheckHalfLife(smoothMs);CheckGain(gainLimit);CheckStability(stabilityMs);CheckSnap(snap);if(curve!=null) curve.Check();
        directions=directions ?? new AimDirections();damping=damping ?? new AimDamping();directions.Check();damping.Check();
        Dictionary<string,object> cfg=Parse(Store.Json.Serialize(current));
        string name="helox-"+id.ToLowerInvariant().Replace('\\','-');
        List<object> profiles=Items(cfg["profiles"]);
        // The first profile controls unmatched devices. A shared profile belongs to other devices too.
        string baseName=name;int suffix=0;
        while(Profile(cfg,name)!=null && ((string)Map(profiles[0])["name"]==name || Items(cfg["devices"]).Exists(delegate(object d){return !String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase) && (string)Map(d)["profile"]==name;}))) name=baseName+"-"+(++suffix);
        Dictionary<string,object> profile=Map(Items(defaults["profiles"])[0]);profile=Parse(Store.Json.Serialize(profile));profile["name"]=name;
        Dictionary<string,object> accel=Map(profile[X]);accel["mode"]=precision ? "natural" : "noaccel";
        accel["Gain / Velocity"]=true;accel["inputOffset"]=3.0;accel["decayRate"]=0.05;accel["limit"]=gainLimit;
        if(precision && (curve!=null || damping.Active)) {accel["mode"]="lut";accel["Gain / Velocity"]=false;accel["inputOffset"]=0.0;accel["data"]=AimLookup.Table(curve,gainLimit,damping);}
        profile["Degrees of angle snapping"]=snap;
        directions.Apply(profile);
        // LUT returns zero at speed <= 0. The native input trend smoother can
        // clamp a nonzero correction to zero after a flick. Smooth scale only
        // for LUT stability, so an estimated speed cannot swallow that motion.
        bool lookup=(string)accel["mode"]=="lut";
        Dictionary<string,object> speed=Map(profile[Speed]);speed[Input]=precision && !lookup ? (stability ? stabilityMs : 4.0) : 0.0;speed[Scale]=precision ? (stability ? stabilityMs/2 : lookup ? 0.0 : 2.0) : 0.0;speed[Output]=smooth ? smoothMs : 0.0;
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
    internal static Dictionary<string,object> Write(Dictionary<string,object> cfg) {
        object valid=Validate(cfg);Call(valid,"DriverConfig","Activate");
        // Released Activate uses synchronous DeviceIoControl; its one-second delay finishes before return.
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
    internal static T Locked<T>(Func<T> work) {
        using(Mutex gate=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=gate.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");return work();
            }finally {if(held) gate.ReleaseMutex();}
        }
    }
    internal static AimStatus Set(Device device,string feature,bool on,double? smoothMs=null,double? gainLimit=null,AimCurve curve=null,double? snap=null,AimDirections directions=null,AimDamping damping=null,string expectedUnit=null) {
        CheckChange(feature,on,smoothMs,gainLimit);
        if(curve!=null) curve.Check();if(snap.HasValue) CheckSnap(snap.Value);
        if(directions!=null) directions.Check();if(damping!=null) damping.Check();
        if(feature=="smooth") {
            if(curve!=null || snap.HasValue || directions!=null || damping!=null) throw new ArgumentException("smoothing changes output averaging only");
            return AdjustSmoothing(device,on,smoothMs,false);
        }
        using(Mutex mutex=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=mutex.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");
                LiveVerify.RequireNoRecovery();
                Dictionary<string,object> before=Active();string id=Id(device);AimStatus status=Describe(before,id);
                CheckUnit(status,expectedUnit);
                string preferences=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> presets=Saved(preferences);
                object existing;Dictionary<string,object> saved=presets.TryGetValue(id,out existing) ? Map(existing) : null;
                RateResult history=(feature=="stability" && on) || feature=="tracking" ? RecentRate(device) : null;
                AimPreset preset=Resolve(status,saved,feature,on,smoothMs,gainLimit,history==null ? 8 : BoundedStability(history.MedianIntervalMs),curve,snap,directions,damping);
                Dictionary<string,object> after=Canonical(ConfigurePreset(before,Defaults(),id,preset,on || feature=="resume"));
                string backup=Path.Combine(Store.Root,"aim-before.json");
                if(File.Exists(backup)) Validate(Parse(File.ReadAllText(backup)));else Store.Save(backup,before);
                presets[id]=preset.ToMap();
                AimStatus result=Describe(Commit(before,after,Write,delegate {Store.Save(preferences,presets);}),id);DescribeDamping(result,preset);return result;
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static Dictionary<string,object> ConfigurePreset(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,AimPreset preset,bool enable=false) {
        return Configure(current,defaults,id,preset.Precision,preset.Smooth,preset.SmoothMs,preset.GainLimit,preset.Stability,preset.StabilityMs,enable,preset.Curve,preset.SnapDegrees,preset.Directions,preset.Damping);
    }
    // This escape hatch deliberately needs neither saved presets nor the original backup.
    internal static Dictionary<string,object> ConfigureBypass(Dictionary<string,object> current,string id,bool bypass) {
        Dictionary<string,object> cfg=Parse(Store.Json.Serialize(current));List<object> devices=Items(cfg["devices"]);
        Dictionary<string,object> existing=DeviceEntry(cfg,id);
        Dictionary<string,object> entry=existing==null ? new Dictionary<string,object>{{"id",id},{"name","helox mouse"},{"profile",""},{"config",Parse(Store.Json.Serialize(cfg["defaultDeviceConfig"]))}} : existing;
        Map(entry["config"])["disable"]=bypass;
        if(existing==null) {
            // A differently cased entry is ignored by the kernel. Replace that alias
            // using the effective default config rather than creating duplicate ids.
            int alias=devices.FindIndex(delegate(object d){return String.Equals((string)Map(d)["id"],id,StringComparison.OrdinalIgnoreCase);});
            if(alias<0) devices.Add(entry);else devices[alias]=entry;cfg["devices"]=devices.ToArray();
        }return cfg;
    }
    internal static AimStatus Bypass(Device device,bool bypass) {
        using(Mutex mutex=new Mutex(false,"Local\\helox-aim-settings")) {
            bool held=false;try {
                try {held=mutex.WaitOne(5000);}catch(AbandonedMutexException) {held=true;}
                if(!held) throw new InvalidOperationException("aim settings busy / retry");
                LiveVerify.RequireNoRecovery();
                Dictionary<string,object> before=Active();string id=Id(device);
                Dictionary<string,object> after=ConfigureBypass(before,id,bypass);Validate(after);
                AimStatus result=Describe(Commit(before,after,Write,delegate {}),id);DescribeSavedControls(result);return result;
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static AimCurve Draft(Device device) {
        string id=Id(device);object value;Dictionary<string,object> saved=Saved(Path.Combine(Store.Root,"aim-presets.json"));
        return saved.TryGetValue(id,out value) && ReadPreset(Map(value)).Curve!=null ? ReadPreset(Map(value)).Curve.Copy() : new AimCurve();
    }
    internal static AimResponse PreviewCurve(Device device,AimCurve curve,string expectedUnit=null) {
        curve.Check();return PreviewControls(device,"curve",true,curve,null,null,expectedUnit);
    }
    internal static AimResponse PreviewControls(Device device,string feature,bool on,AimCurve curve=null,AimDirections directions=null,AimDamping damping=null,string expectedUnit=null) {
        Dictionary<string,object> before=Active();string id=Id(device);object value;
        Dictionary<string,object> saved=Saved(Path.Combine(Store.Root,"aim-presets.json"));
        AimStatus current=Describe(before,id);CheckUnit(current,expectedUnit);
        AimPreset preset=Resolve(current,saved.TryGetValue(id,out value) ? Map(value) : null,feature,on,null,null,8,curve,null,directions,damping);
        RateResult history=RecentRate(device);
        AimResponse result=AimResponseTest.Run(ConfigurePreset(before,Defaults(),id,preset,true),id,history==null ? 8 : history.MedianIntervalMs);
        result.IntervalSource=history==null ? "8 ms example / no recent matching history" : "recent delivery median / not kernel timing readback";
        result.Readback.Note="proposed profile / not activated";DescribeDamping(result.Readback,preset);
        return result;
    }
    internal static void CheckUnit(AimStatus status,string expected) {if(expected!=null && expected!=status.CurveSpeedUnit) throw new InvalidOperationException("curve speed unit changed / reopen curve builder");}
    internal static void DescribeDamping(AimStatus status,AimPreset preset) {
        if(status.Mode=="lut" && status.LookupIsSensitivity==true && status.LookupLayoutCompatible==true && (preset.SpeedUnit==null || preset.SpeedUnit==status.CurveSpeedUnit) && AimLookup.Matches(preset,status.LookupData)) {
            status.DampingEnabled=status.Enabled==true && preset.Precision && preset.Damping.Active;
            status.DampingLowScale=preset.Damping.LowScale;status.DampingRecoverySpeed=preset.Damping.RecoverySpeed;
        }
    }
    private static void DescribeSavedControls(AimStatus status) {
        if(status.Mode!="lut") return;
        try {object saved;Dictionary<string,object> presets=Saved(Path.Combine(Store.Root,"aim-presets.json"));if(presets.TryGetValue(status.DeviceId,out saved)) DescribeDamping(status,ReadPreset(Map(saved)));}
        catch {status.Note+=" / saved controls unavailable / reapply a curve or game setup; bypass remains available";return;}
        if(status.DampingEnabled==null && status.Profile!=null && status.Profile.StartsWith("helox-",StringComparison.Ordinal))
            status.Note+=" / saved curve controls missing or changed / reapply a curve or game setup; bypass remains available";
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
                LiveVerify.RequireNoRecovery();
                Dictionary<string,object> before=Active();
                Dictionary<string,object> after=Parse(File.ReadAllText(path));Validate(after);
                Commit(before,after,Write,delegate {Store.Save(Path.Combine(Store.Root,"aim-presets.json"),new Dictionary<string,object>());});
            }finally {if(held) mutex.ReleaseMutex();}
        }
    }
    internal static void TestEngine() {
        if(!File.Exists(Path.Combine(Root,"wrapper.dll"))) {Console.WriteLine("  aim engine tests skipped / aim prepare first");return;}
        Dictionary<string,object> defaults=Defaults();
        TestPersonalCurve(defaults);
        TestFilters(defaults);
        TestLutRecovery(defaults);
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
        Dictionary<string,object> smoothed=Configure(defaults,defaults,exampleId,true,true,8);
        AimPreset recoveredPreset=Resolve(Describe(smoothed,exampleId),null,"smooth",false,null,null,8);
        Dictionary<string,object> recoveredConfig=ConfigurePreset(smoothed,defaults,exampleId,recoveredPreset);
        foreach(double interval in new double[]{1,8,16}) {
            AimResponse recovered=AimResponseTest.Run(recoveredConfig,exampleId,interval);
            AimResponse reference=AimResponseTest.Run(smoothed,exampleId,interval);
            Console.WriteLine("  flick recovery / "+interval+" ms / turn peak "+recovered.AfterFlickPeakCounts+" zeros "+recovered.AfterFlickZeroReports+" / reverse peak "+recovered.ReversalPeakCounts+" wrong way "+recovered.ReversalWrongWayReports+" zeros "+recovered.ReversalZeroReports);
            // Opposite fractional carry can cancel one first reversal count even without a filter.
            if(recovered.AfterFlickPeakCounts>2 || recovered.AfterFlickZeroReports!=0 || recovered.ReversalPeakCounts>2 || recovered.ReversalWrongWayReports!=0 || recovered.ReversalZeroReports>1) throw new Exception("output averaging removal failed flick recovery");
            if(Math.Abs(recovered.SmallMotionRatio-reference.SmallMotionRatio)>.0001 || Math.Abs(recovered.FastMotionRatio-reference.FastMotionRatio)>.0001) throw new Exception("output averaging removal changed settled sensitivity");
        }
        if(averaged.AfterFlickPeakCounts<40 || averaged.AfterFlickZeroReports<1) throw new Exception("output smoothing transient was not reproduced");
        if(tracking.AfterFlickPeakCounts>2 || tracking.AfterFlickPeakCounts<1 || tracking.AfterFlickZeroReports!=0 || tracking.FastMotionRatio<=1 || tracking.FastMotionRatio>1.401) throw new Exception("tracking recovery response failed");
        if(tracking.AfterFlickX[0]!=0 || tracking.AfterFlickY[0]<1) throw new Exception("tracking one-count turn failed");
        int pointIndex=0;double previousRatio=0;
        foreach(int input in new int[]{1,8,24,40,80,160,400,800}) {
            AimResponsePoint point=averaged.HorizontalSamples[pointIndex++];
            if(point.InputCounts!=input || point.InputCountsPerMs!=input/8.0 || point.OutputRatio<previousRatio-.000001 || point.OutputRatio<1-.000001 || point.OutputRatio>1.401) throw new Exception("horizontal response sweep failed");
            if(input<=24 && Math.Abs(point.OutputRatio-1)>.000001) throw new Exception("horizontal sweep missed the precision threshold");
            if(input==8 && point.OutputRatio!=averaged.SmallMotionRatio || input==800 && point.OutputRatio!=averaged.FastMotionRatio) throw new Exception("horizontal sweep changed legacy response ratios");
            previousRatio=point.OutputRatio;
        }
        if(averaged.HorizontalSamples[4].OutputRatio<=1 || averaged.HorizontalSamples[4].OutputRatio>=1.1) throw new Exception("intermediate response was mistaken for the full gain limit");
        Dictionary<string,object> bypass=Configure(defaults,Defaults(),exampleId,true,true,8);
        Map(Map(Items(bypass["devices"])[0])["config"])["disable"]=true;
        AimResponse disabled=AimResponseTest.Run(bypass,exampleId,8);
        if(disabled.SmallMotionRatio!=1 || disabled.FastMotionRatio!=1 || disabled.AfterFlickPeakCounts!=1 || disabled.AfterFlickZeroReports!=0) throw new Exception("disabled response was not bypassed");
        foreach(AimResponsePoint point in disabled.HorizontalSamples) if(point.OutputRatio!=1) throw new Exception("disabled sweep was not bypassed");
        Dictionary<string,object> normalized=Configure(defaults,Defaults(),exampleId,false,false);
        Map(Map(Items(normalized["devices"])[0])["config"])["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        AimResponse scaled=AimResponseTest.Run(normalized,exampleId,8);
        if(Math.Abs(scaled.SmallMotionRatio-.625)>.000001 || Math.Abs(scaled.FastMotionRatio-.625)>.000001) throw new Exception("response missed configured dpi normalization");
        foreach(AimResponsePoint point in scaled.HorizontalSamples) if(Math.Abs(point.OutputRatio-.625)>.000001) throw new Exception("response sweep missed configured dpi normalization");
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
    private static void WithEngine(Dictionary<string,object> config,Action<object> run) {
        object valid=Validate(config);IList engines=(IList)valid.GetType().GetField("accels").GetValue(valid);
        try {run(engines[engines.Count-1]);}finally {foreach(object engine in engines) {IDisposable disposable=engine as IDisposable;if(disposable!=null) disposable.Dispose();}}
    }
    private static double TableRatio(object[] table,double speed) {
        if(speed<=Convert.ToDouble(table[0])) return Convert.ToDouble(table[1]);
        for(int i=2;i<table.Length;i+=2) if(speed<=Convert.ToDouble(table[i])) {
            double a=Convert.ToDouble(table[i-2]),b=Convert.ToDouble(table[i]),low=Convert.ToDouble(table[i-1]),high=Convert.ToDouble(table[i+1]);
            return low+(high-low)*(speed-a)/(b-a);
        }
        return Convert.ToDouble(table[table.Length-1]);
    }
    private static void TestPersonalCurve(Dictionary<string,object> defaults) {
        foreach(AimCurve curve in new AimCurve[]{new AimCurve(),new AimCurve {Base=.25,Start=0,End=.1,Limit=3,Shape=.5},new AimCurve {Base=2,Start=1000,End=1000.1,Limit=3,Shape=3},new AimCurve {Base=.8,Start=2,End=24,Limit=1.8,Shape=1.5},new AimCurve {Base=1,Start=0,End=2000,Limit=1,Shape=3}}) {
            Dictionary<string,object> config=Configure(defaults,Defaults(),"HID\\CURVE",true,false,4,1.4,false,8,true,curve);
            Dictionary<string,object> speed=Map(Map(Items(config["profiles"])[1])[Speed]);speed[Input]=0.0;speed[Scale]=0.0;
            object valid=Validate(config);AimStatus roundtrip=Describe(Parse(Text(valid)),"HID\\CURVE");
            if(!curve.Matches(roundtrip.LookupData)) throw new Exception("native curve serialization changed the table");
            IList unused=(IList)valid.GetType().GetField("accels").GetValue(valid);foreach(object item in unused) {IDisposable disposable=item as IDisposable;if(disposable!=null) disposable.Dispose();}
            object[] table=curve.Table();
            WithEngine(config,delegate(object engine) {
                foreach(double inputSpeed in new double[]{.0001,curve.Start+.00001,(curve.Start+curve.End)/2,curve.End,curve.End+100}) {
                    object output=Call(engine,"ManagedAccel","Accelerate",1000,0,1.0,1000/inputSpeed);
                    if(Math.Abs(Axis(output)/1000-TableRatio(table,inputSpeed))>1e-6) throw new Exception("custom curve disagrees with official engine / speed "+inputSpeed.ToString(CultureInfo.InvariantCulture)+" / actual "+(Axis(output)/1000).ToString(CultureInfo.InvariantCulture)+" / expected "+TableRatio(table,inputSpeed).ToString(CultureInfo.InvariantCulture));
                }
                object turn=Call(engine,"ManagedAccel","Accelerate",-200,100,1.0,8.0);double y=Convert.ToDouble(turn.GetType().GetProperty("Item2").GetValue(turn,null));
                if(Axis(turn)>=0 || y<=0 || Math.Abs(Axis(turn)/y+2)>1e-8) throw new Exception("custom curve changed direction with snapping off");
                object stop=Call(engine,"ManagedAccel","Accelerate",0,0,1.0,8.0);
                if(Axis(stop)!=0 || Convert.ToDouble(stop.GetType().GetProperty("Item2").GetValue(stop,null))!=0) throw new Exception("custom curve generated rest motion");
            });
        }
        foreach(double angle in new double[]{0,1,5}) {
            Dictionary<string,object> config=Configure(defaults,Defaults(),"HID\\CURVE",false,false,4,1.4,false,8,true,null,angle);
            WithEngine(config,delegate(object engine) {
                object near=Call(engine,"ManagedAccel","Accelerate",1000,10,1.0,8.0);double x=Axis(near),y=Convert.ToDouble(near.GetType().GetProperty("Item2").GetValue(near,null));
                if(angle==0 ? (x!=1000 || y!=10) : (y!=0 || Math.Abs(x-Math.Sqrt(1000100))>1e-6)) throw new Exception("angle snap horizontal/off response failed");
                near=Call(engine,"ManagedAccel","Accelerate",-10,-1000,1.0,8.0);x=Axis(near);y=Convert.ToDouble(near.GetType().GetProperty("Item2").GetValue(near,null));
                if(angle==0 ? (x!=-10 || y!=-1000) : (x!=0 || y>=0)) throw new Exception("angle snap vertical sign response failed");
                object diagonal=Call(engine,"ManagedAccel","Accelerate",100,-100,1.0,8.0);
                if(Axis(diagonal)!=100 || Convert.ToDouble(diagonal.GetType().GetProperty("Item2").GetValue(diagonal,null))!=-100) throw new Exception("angle snap affected motion outside its threshold");
            });
        }
        Console.WriteLine("  passed / personal LUT curves, native serialization, direction/rest and optional snap thresholds / no driver writes");
    }
    private static void TestFilters(Dictionary<string,object> defaults) {
        Dictionary<string,object> natural=Configure(defaults,defaults,"HID\\FILTER",true,false);
        Dictionary<string,object> naturalSpeed=Map(Map(Items(natural["profiles"])[1])[Speed]);naturalSpeed[Input]=0.0;naturalSpeed[Scale]=0.0;
        WithEngine(natural,delegate(object engine) {
            foreach(double velocity in new double[]{.1,3,5,25,100,1000,40000}) if(Math.Abs(Axis(Call(engine,"ManagedAccel","Accelerate",1000,0,1.0,1000/velocity))/1000-AimLookup.Natural(velocity,1.4))>1e-12) throw new Exception("natural formula differs from native gain implementation");
        });
        AimDirections weights=new AimDirections {Left=.25,Right=.9,Up=.4,Down=.8};
        Dictionary<string,object> directional=Configure(defaults,defaults,"HID\\FILTER",false,false,4,1.4,false,8,true,null,0,weights);
        AimResponse response=AimResponseTest.Run(directional,"HID\\FILTER",8);
        double[] expected={.25,.9,.4,.8};
        for(int i=0;i<4;i++) if(Math.Abs(response.DirectionSamples[i].OutputRatio-expected[i])>1e-12) throw new Exception("direction scale sign/order disagrees with official engine");
        if(Math.Abs(response.DirectionSamples[4].OutputX-7.2)>1e-12 || Math.Abs(response.DirectionSamples[4].OutputY+3.2)>1e-12) throw new Exception("diagonal directional scaling failed");
        foreach(AimCurve curve in new AimCurve[]{null,new AimCurve(),new AimCurve {Base=.25,Start=0,End=.1,Limit=3,Shape=.5},new AimCurve {Base=2,Start=1000,End=1000.1,Limit=3,Shape=3}})
        foreach(double recover in new double[]{.1,1,20}) {
            AimDamping damping=new AimDamping {Enabled=true,LowScale=.25,RecoverySpeed=recover};
            Dictionary<string,object> cfg=Configure(defaults,defaults,"HID\\FILTER",true,false,4,1.4,false,8,true,curve,0,null,damping);
            Dictionary<string,object> speed=Map(Map(Items(cfg["profiles"])[1])[Speed]);speed[Input]=0.0;speed[Scale]=0.0;
            object[] table=AimLookup.Table(curve,1.4,damping);object valid=Validate(cfg);
            try {if(!AimLookup.Matches(new AimPreset {Precision=true,Curve=curve,Damping=damping},Describe(Parse(Text(valid)),"HID\\FILTER").LookupData)) throw new Exception("micro lookup native float roundtrip failed");}
            finally {foreach(object engine in (IList)valid.GetType().GetField("accels").GetValue(valid)) ((IDisposable)engine).Dispose();}
            WithEngine(cfg,delegate(object engine) {
                foreach(double velocity in new double[]{.0001,recover/2,recover,3,5,25,100,1000,40000}) {
                    double ratio=Axis(Call(engine,"ManagedAccel","Accelerate",1000,0,1.0,1000/velocity))/1000;
                    if(Math.Abs(ratio-AimLookup.Interpolate(table,velocity))>1e-6 || ratio<=0) throw new Exception("micro lookup disagrees with official engine");
                    if(curve==null && Math.Abs(ratio-AimLookup.Natural(velocity,1.4)*damping.Scale(velocity))>.001) throw new Exception("micro natural approximation error exceeds 0.001x");
                    if(curve!=null && velocity>=recover && Math.Abs(ratio-AimLookup.Interpolate(curve.Table(),velocity))>1e-6) throw new Exception("micro changed custom curve above recovery");
                }
                object stop=Call(engine,"ManagedAccel","Accelerate",0,0,1.0,8.0);if(Axis(stop)!=0 || Convert.ToDouble(stop.GetType().GetProperty("Item2").GetValue(stop,null))!=0) throw new Exception("micro generated motion at rest");
            });
        }
        foreach(double interval in new double[]{1,8,16}) foreach(bool smooth in new bool[]{false,true}) foreach(bool stability in new bool[]{false,true}) foreach(double snap in new double[]{0,1,5}) {
            Dictionary<string,object> cfg=Configure(defaults,defaults,"HID\\FILTER",true,smooth,4,1.4,stability,8,true,new AimCurve(),snap,weights,new AimDamping {Enabled=true});
            AimResponse combined=AimResponseTest.Run(cfg,"HID\\FILTER",interval);
            foreach(AimDirectionPoint p in combined.DirectionSamples) if(double.IsNaN(p.OutputRatio) || double.IsInfinity(p.OutputRatio) || p.OutputRatio<=0 || p.InputX*p.OutputX<0 || p.InputY*p.OutputY<0) throw new Exception("combined filters invalid direction/output");
            AimDirectionPoint near=combined.DirectionSamples[5];if(snap>0 ? near.OutputY!=0 : near.OutputY<=0) throw new Exception("combined snap threshold failed");
            AimResponse bypass=AimResponseTest.Run(ConfigureBypass(cfg,"HID\\FILTER",true),"HID\\FILTER",interval);
            foreach(AimDirectionPoint p in bypass.DirectionSamples) if(p.OutputX!=p.InputX || p.OutputY!=p.InputY) throw new Exception("bypass failed to disable combined filters");
            if(bypass.AfterFlickPeakCounts!=1 || bypass.AfterFlickZeroReports!=0) throw new Exception("bypass retained filter carry");
        }
        Dictionary<string,object> micro=Configure(defaults,defaults,"HID\\FILTER",true,false,4,1.4,false,8,true,null,0,null,new AimDamping {Enabled=true,LowScale=.25,RecoverySpeed=20});
        WithEngine(micro,delegate(object engine) {
            AimCarry carry=new AimCarry();int emitted=0;
            for(int i=0;i<128;i++) {object pair=Call(engine,"ManagedAccel","Accelerate",1,0,1.0,8.0);emitted+=carry.Emit(Axis(pair),Convert.ToDouble(pair.GetType().GetProperty("Item2").GetValue(pair,null)))[0];}
            if(emitted<31 || emitted>33) throw new Exception("micro fractional counts were lost instead of accumulated");
        });
        Console.WriteLine("  passed / native directional attenuation, micro LUT, natural approximation <0.001x at samples, combined filters and bypass / no driver writes");
    }
    private static void TestLutRecovery(Dictionary<string,object> defaults) {
        const string id="HID\\RECOVERY";
        Dictionary<string,object> fixedCurve=GamePresets.Configure(defaults,defaults,id,GamePresets.Get("valorant"));
        Dictionary<string,object> legacy=Parse(Store.Json.Serialize(fixedCurve));Dictionary<string,object> speed=Map(Map(Items(legacy["profiles"])[1])[Speed]);speed[Input]=4.0;speed[Scale]=2.0;
        AimResponse old=AimResponseTest.Run(legacy,id,8),current=AimResponseTest.Run(fixedCurve,id,8);
        if(old.AfterFlickZeroReports<1 || current.AfterFlickZeroReports!=0 || current.AfterFlickPeakCounts!=1) throw new Exception("LUT flick recovery regression failed");
        Dictionary<string,object> renamed=Canonical(fixedCurve);
        Map(Items(renamed["profiles"])[1])["name"]="renamed training curve";
        DeviceEntry(renamed,id)["profile"]="renamed training curve";
        if(Array.IndexOf(GamePresets.Matches(renamed,defaults,id),"valorant")<0) throw new Exception("renamed equivalent recipe was not recognized");
        Map(Items(renamed["profiles"])[0])["Input Speed Cap"]=10.0;
        if(Array.IndexOf(GamePresets.Matches(renamed,defaults,id),"valorant")<0) throw new Exception("unrelated default profile affected selected recipe matching");
        Map(DeviceEntry(renamed,id)["config"])["Polling rate Hz (keep at 0 for automatic adjustment)"]=125.0;
        if(GamePresets.Matches(renamed,defaults,id).Length!=0) throw new Exception("changed device timing still matched a recipe");
        foreach(GameRecipe recipe in GamePresets.List()) foreach(double interval in new double[]{1,8,16}) {
            Dictionary<string,object> recipeConfig=Canonical(GamePresets.Configure(defaults,defaults,id,recipe));
            if(Array.IndexOf(GamePresets.Matches(recipeConfig,defaults,id),recipe.Id)<0) throw new Exception("live recipe match missed an applied recipe");
            if(GamePresets.Matches(Canonical(ConfigureBypass(recipeConfig,id,true)),defaults,id).Length!=0) throw new Exception("bypassed recipe reported active");
            AimResponse response=AimResponseTest.Run(recipeConfig,id,interval);
            if(response.ReversalWrongWayReports!=0) throw new Exception("recipe reversed a one-count correction after flick");
            if(response.Readback.LookupInputSmoothingRisk!=false || response.Readback.OutputHalfLifeMs!=0 || response.FastMotionRatio>recipe.Curve.Limit+1e-6 || response.FastMotionRatio<=1) throw new Exception("game recipe response bounds failed");
            if(!recipe.MicroDamping && (response.AfterFlickPeakCounts>2 || response.AfterFlickZeroReports!=0)) throw new Exception("undamped game recipe swallowed a micro correction");
            foreach(int output in response.AfterFlickY) if(output<0) throw new Exception("game recipe reversed correction direction");
        }
        double unfilteredSpread=0;
        foreach(double halfLife in new double[]{0,8,10,12}) {
            Dictionary<string,object> cfg=Configure(defaults,defaults,id,true,false,2,1.4,halfLife>0,halfLife>0 ? halfLife : 8,true,new AimCurve());
            WithEngine(cfg,delegate(object engine) {
                double slow=0,fast=0;
                for(int i=0;i<120;i++) {
                    slow=Axis(Call(engine,"ManagedAccel","Accelerate",40,0,1.0,8.0))/40;
                    fast=Axis(Call(engine,"ManagedAccel","Accelerate",120,0,1.0,8.0))/120;
                }
                double spread=Math.Abs(fast-slow);
                if(slow<1 || fast<1 || slow>1.401 || fast>1.401 || spread<=0) throw new Exception("LUT scale stability gain bounds failed");
                if(halfLife==0) unfilteredSpread=spread;
                else if(spread>=unfilteredSpread) throw new Exception("LUT scale stability has no measured effect");
                object turn=Call(engine,"ManagedAccel","Accelerate",-80,40,1.0,8.0);
                double x=Axis(turn),y=Convert.ToDouble(turn.GetType().GetProperty("Item2").GetValue(turn,null));
                if(x>=0 || y<=0 || Math.Abs(x/y+2)>.001) throw new Exception("LUT scale stability changed direction");
                object rest=Call(engine,"ManagedAccel","Accelerate",0,0,1.0,8.0);
                if(Axis(rest)!=0 || Convert.ToDouble(rest.GetType().GetProperty("Item2").GetValue(rest,null))!=0) throw new Exception("LUT scale stability generated motion at rest");
                Console.WriteLine("  LUT stability "+(halfLife==0 ? "off" : halfLife.ToString(CultureInfo.InvariantCulture)+" ms")+" / alternating ratio spread "+spread.ToString("0.000",CultureInfo.InvariantCulture)+"x / native engine");
            });
        }
        Console.WriteLine("  passed / native LUT flick recovery: legacy "+old.AfterFlickZeroReports+" zero outputs -> fixed "+current.AfterFlickZeroReports+" / game recipes at 1, 8, 16 ms / no driver writes");
    }
}
}
