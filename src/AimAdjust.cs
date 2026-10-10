using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
public sealed class AimControlsRecovery {
    internal Dictionary<string,object> SourceState;
    public bool Available {get;set;}
    public bool Applied {get;set;}
    public bool DriverChanged {get {return false;}}
    public string[] Matches {get;set;}
    public Dictionary<string,object> Controls {get;set;}
    public string Note {get;set;}
}
public sealed class AimTailRepair {
    internal Dictionary<string,object> SourceState;
    public bool Applied {get;set;}
    public bool Changed {get;set;}
    public AimStatus Before {get;set;}
    public AimStatus Readback {get;set;}
    public AimResponse BeforeResponse {get;set;}
    public AimResponse Response {get;set;}
    public string[] Changes {get;set;}
}
internal static partial class Aim {
    // Clone a shared/default profile before editing; do not alter another mouse's processing.
    internal static Dictionary<string,object> EditProfile(Dictionary<string,object> current,string id,Action<Dictionary<string,object>> edit) {
        Dictionary<string,object> cfg=Parse(Store.Json.Serialize(current)),entry=DeviceEntry(cfg,id);
        List<object> profiles=Items(cfg["profiles"]);string name=entry==null || String.IsNullOrEmpty((string)entry["profile"]) ? (string)Map(profiles[0])["name"] : (string)entry["profile"];
        Dictionary<string,object> original=Profile(cfg,name);if(original==null) throw new InvalidOperationException("driver profile not found");
        Dictionary<string,object> changed=Parse(Store.Json.Serialize(original));edit(changed);
        if(SameValue(original,changed)) return cfg;
        bool shared=(string)Map(profiles[0])["name"]==name || Items(cfg["devices"]).Exists(delegate(object d){return (string)Map(d)["id"]!=id && (string)Map(d)["profile"]==name;});
        if(shared) {
            // Keep the existing ownership classification; an imported default is not a helox curve.
            name=(name.StartsWith("helox-",StringComparison.Ordinal) ? "helox-filter-" : "filter-")+Guid.NewGuid().ToString("n");
            changed["name"]=name;profiles.Add(changed);
        }else profiles[profiles.FindIndex(delegate(object p){return (string)Map(p)["name"]==name;})]=changed;
        cfg["profiles"]=profiles.ToArray();bool disabled=entry==null ? (bool)Map(cfg["defaultDeviceConfig"])["disable"] : (bool)Map(entry["config"])["disable"];
        cfg=ConfigureBypass(cfg,id,disabled);DeviceEntry(cfg,id)["profile"]=name;return cfg;
    }
    internal static Dictionary<string,object> ConfigureSmoothing(Dictionary<string,object> current,string id,bool on,double ms,bool tail) {
        CheckHalfLife(ms);if(tail && on) throw new ArgumentException("flick tail repair disables averaging");
        Dictionary<string,object> cfg=EditProfile(current,id,delegate(Dictionary<string,object> profile) {
            Dictionary<string,object> speed=Map(profile[Speed]);speed[Output]=on ? ms : 0.0;
            // Only migrate shared LUT speed estimation; mixed per-axis curves retain their timing.
            bool whole=(bool)speed["Whole/combined accel (set false for 'by component' mode)"];
            if(tail && (string)Map(profile[X])["mode"]=="lut" && (whole || (string)Map(profile["Vertical accel parameters"])["mode"]=="lut")) speed[Input]=0.0;
        });
        cfg=on ? ConfigureBypass(cfg,id,false) : cfg;
        RequireSmoothingOnly(current,cfg,id,tail);return cfg;
    }
    internal static AimControlsRecovery RecoverableControls(Dictionary<string,object> current,Dictionary<string,object> defaults,string id,Func<Dictionary<string,object>,Dictionary<string,object>> normalize=null) {
        if(normalize==null) normalize=Canonical;
        Dictionary<string,object> enabled=ConfigureBypass(current,id,false);List<string> matches=new List<string>();AimPreset found=null;
        foreach(GameRecipe baseRecipe in GamePresets.List()) foreach(string style in new string[]{"","-steady","-linear"}) {
            GameRecipe recipe=GamePresets.Get(baseRecipe.Id+style);
            if(!GamePresets.SameSelected(enabled,normalize(GamePresets.Configure(enabled,defaults,id,recipe)),id)) continue;
            AimPreset candidate=recipe.Controls();
            if(found!=null && !SameValue(found.ToMap(),candidate.ToMap())) return new AimControlsRecovery {Available=false,Matches=matches.ToArray(),Note="ambiguous controls / nothing saved"};
            found=candidate;matches.Add(recipe.Id);
        }
        return new AimControlsRecovery {Available=found!=null,Matches=matches.ToArray(),Controls=found==null ? null : found.ToMap(),Note=found==null ? "no exact built-in recipe match / apply a curve or game setup" : "exact selected-mouse configuration match / saves controls only / driver and windows unchanged"};
    }
    internal static AimControlsRecovery RecoverControls(Device device,bool apply,Dictionary<string,object> expected=null) {
        AimControlsRecovery result=null;
        Store.Locked(delegate {result=Locked(delegate {
            if(apply) LiveVerify.RequireNoRecovery();Dictionary<string,object> current=Active();string id=Id(device);
            if(apply && expected!=null && !GamePresets.SameSelected(current,expected,id)) throw new InvalidOperationException("mouse setup changed / preview recovery again");
            AimControlsRecovery recovered=RecoverableControls(current,Defaults(),id);
            recovered.SourceState=current;
            if(apply) {
                if(!recovered.Available) throw new InvalidOperationException(recovered.Note);
                string path=Path.Combine(Store.Root,"aim-presets.json");Dictionary<string,object> saved=Saved(path);object old;
                if(!saved.TryGetValue(id,out old) || !SameValue(old,recovered.Controls)) {saved[id]=recovered.Controls;Store.Save(path,saved);}
                recovered.Applied=true;
            }
            return recovered;
        });});return result;
    }
    private static AimPreset SmoothingControls(Dictionary<string,object> before,string id,Dictionary<string,object> saved) {
        object value;
        if(saved.TryGetValue(id,out value)) {
            AimPreset preset=ReadPreset(Map(value));
            if((preset.Curve!=null || preset.Damping.Active) && preset.SpeedUnit!=null && preset.SpeedUnit!=Describe(before,id).CurveSpeedUnit) return null;
            Dictionary<string,object> expected=Canonical(ConfigurePreset(before,Defaults(),id,preset));
            Dictionary<string,object> actualProfile=EffectiveProfile(before,id),expectedProfile=EffectiveProfile(expected,id);
            Map(actualProfile[Speed]).Remove(Output);Map(expectedProfile[Speed]).Remove(Output);
            if(SameValue(actualProfile,expectedProfile)) return preset;
            return null;
        }
        AimControlsRecovery known=RecoverableControls(before,Defaults(),id);return known.Available ? ReadPreset(known.Controls) : null;
    }
    private static Dictionary<string,object> CommitSmoothing(Dictionary<string,object> before,Dictionary<string,object> after,string id,Dictionary<string,object> saved,AimPreset controls,bool on,double ms) {
        string preferences=Path.Combine(Store.Root,"aim-presets.json"),backup=Path.Combine(Store.Root,"aim-before.json");
        if(File.Exists(backup)) Validate(Parse(File.ReadAllText(backup)));else Store.Save(backup,before);
        SavedFiles files=new SavedFiles(new string[]{preferences,Path.Combine(Store.Root,"game-undo.json"),Path.Combine(Store.Root,"undo.json")});Settings windows=Settings.Read();
        GameRecovery.Begin(windows,before,files);
        return GamePresets.CommitPair(windows,before,windows,after,delegate(Settings s){s.Apply();},Write,delegate(Dictionary<string,object> live) {
            if(controls!=null) {
                controls.Smooth=on;if(on) controls.SmoothMs=ms;
                // Preserve a valid curve's metadata; unknown imported curves are never reconstructed.
                saved[id]=controls.ToMap();if(!SameValue(Saved(preferences),saved)) Store.Save(preferences,saved);
            }
            GameRecovery.Complete();
        },files.Restore,GameRecovery.Complete);
    }
    private static AimStatus AdjustSmoothing(Device device,bool on,double? strength,bool tail) {
        AimStatus result=null;
        Store.Locked(delegate {result=Locked(delegate {
            LiveVerify.RequireNoRecovery();Dictionary<string,object> before=Active();string id=Id(device);
            Dictionary<string,object> saved=Saved(Path.Combine(Store.Root,"aim-presets.json"));AimPreset controls=SmoothingControls(before,id,saved);
            AimStatus current=Describe(before,id);double ms=strength ?? (current.OutputHalfLifeMs>=1 && current.OutputHalfLifeMs<=12 ? current.OutputHalfLifeMs.Value : controls==null ? 4 : controls.SmoothMs);CheckHalfLife(ms);
            Dictionary<string,object> after=Canonical(ConfigureSmoothing(before,id,on,ms,tail));
            AimStatus state=Describe(CommitSmoothing(before,after,id,saved,controls,on,ms),id);DescribeSavedControls(state);return state;
        });});return result;
    }
    internal static AimTailRepair RepairTail(Device device,bool apply,Dictionary<string,object> expected=null) {
        AimTailRepair result=null;
        Store.Locked(delegate {result=Locked(delegate {
            if(apply) LiveVerify.RequireNoRecovery();Dictionary<string,object> before=Active();string id=Id(device);
            if(apply && expected!=null && !GamePresets.SameSelected(before,expected,id)) throw new InvalidOperationException("mouse setup changed / preview flick-tail repair again");
            Dictionary<string,object> after=Canonical(ConfigureSmoothing(before,id,false,4,true));List<string> changes=new List<string>();
            AimStatus old=Describe(before,id),next=Describe(after,id);if(old.OutputHalfLifeMs!=next.OutputHalfLifeMs) changes.Add("output averaging off");if(old.InputHalfLifeMs!=next.InputHalfLifeMs) changes.Add("legacy lut speed averaging off");
            RateResult history=RecentRate(device);double interval=history==null ? 8 : history.MedianIntervalMs;
            AimResponse beforeResponse=AimResponseTest.Run(before,id,interval),response=AimResponseTest.Run(after,id,interval);response.IntervalSource=history==null ? "8 ms example / no recent matching history" : "recent delivery median / not kernel timing readback";
            beforeResponse.IntervalSource=response.IntervalSource;
            if(apply) {
                Dictionary<string,object> saved=Saved(Path.Combine(Store.Root,"aim-presets.json"));AimPreset controls=SmoothingControls(before,id,saved);
                next=Describe(CommitSmoothing(before,after,id,saved,controls,false,4),id);DescribeSavedControls(next);
            }
            else next.Note="proposed flick-tail repair / not activated";
            response.Readback=next;
            return new AimTailRepair {SourceState=before,Applied=apply,Changed=!SameValue(before,after),Before=old,Readback=next,BeforeResponse=beforeResponse,Response=response,Changes=changes.ToArray()};
        });});return result;
    }
}
}
