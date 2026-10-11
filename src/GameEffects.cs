using System;
using System.Collections.Generic;
using System.Globalization;

namespace Helox {
public sealed class GameEffects {
    public double? SmoothMs {get;set;}
    public bool? Stability {get;set;}
    public double? MicroScale {get;set;}
    public double? MicroRecovery {get;set;}
    public double? SnapDegrees {get;set;}
    public AimDirections Directions {get;set;}
    public bool Customized {get {return SmoothMs.HasValue || Stability.HasValue || MicroScale.HasValue || MicroRecovery.HasValue || SnapDegrees.HasValue || Directions!=null;}}
    internal GameEffects Copy() {return new GameEffects {SmoothMs=SmoothMs,Stability=Stability,MicroScale=MicroScale,MicroRecovery=MicroRecovery,SnapDegrees=SnapDegrees,Directions=Directions==null ? null : Directions.Copy()};}
    internal void Check() {
        if(SmoothMs.HasValue && SmoothMs!=0) Aim.CheckHalfLife(SmoothMs.Value);
        if(SnapDegrees.HasValue) Aim.CheckSnap(SnapDegrees.Value);
        if(MicroScale.HasValue) AimFilterNumbers.Range(MicroScale.Value,.25,1,"micro scale: 0.25..1x / 1 disables");
        if(MicroRecovery.HasValue) {
            if(!MicroScale.HasValue || MicroScale>=1) throw new ArgumentException("recovery requires an active micro scale");
            AimFilterNumbers.Range(MicroRecovery.Value,.1,20,"micro recovery: 0.1..20 counts/ms");
        }
        if(Directions!=null) Directions.Check();
    }
    internal AimPreset Apply(AimPreset controls) {
        Check();
        if(SmoothMs.HasValue) {controls.Smooth=SmoothMs>0;if(controls.Smooth) controls.SmoothMs=SmoothMs.Value;}
        if(Stability.HasValue) controls.Stability=Stability.Value;
        if(MicroScale.HasValue) {controls.Damping.Enabled=MicroScale<1;if(controls.Damping.Enabled) controls.Damping.LowScale=MicroScale.Value;}
        if(MicroRecovery.HasValue) controls.Damping.RecoverySpeed=MicroRecovery.Value;
        if(SnapDegrees.HasValue) {controls.SnapDegrees=SnapDegrees.Value;if(SnapDegrees>0) controls.SnapStrength=SnapDegrees.Value;}
        if(Directions!=null) controls.Directions=Directions.Copy();
        return controls;
    }
    internal string[] Arguments() {
        Check();List<string> args=new List<string>();
        if(SmoothMs.HasValue) args.Add("smooth="+N(SmoothMs.Value));
        if(Stability.HasValue) args.Add("stability="+(Stability.Value ? "on" : "off"));
        if(MicroScale.HasValue) args.Add("micro="+N(MicroScale.Value));
        if(MicroRecovery.HasValue) args.Add("recovery="+N(MicroRecovery.Value));
        if(SnapDegrees.HasValue) args.Add("snap="+N(SnapDegrees.Value));
        if(Directions!=null) {args.Add("left="+N(Directions.Left));args.Add("right="+N(Directions.Right));args.Add("up="+N(Directions.Up));args.Add("down="+N(Directions.Down));}
        return args.ToArray();
    }
    private static string N(double n) {return n.ToString("R",CultureInfo.InvariantCulture);}
}
internal sealed class PresetRequest {
    internal string Name;
    internal GameEffects Effects=new GameEffects();
    internal bool AcceptTail;
    internal static PresetRequest Parse(string[] words) {
        if(words.Length<3 || words.Length>17) throw new ArgumentException("preset <action> <game> [style] [personal] [smooth=ms stability=on|off micro=scale recovery=speed snap=degrees left=scale right=scale up=scale down=scale] [accept-tail]");
        GameRecipe initial=GamePresets.Get(words[2]);string name=initial.Id;bool personal=initial.Personal;
        if(personal) name=name.Substring(0,name.Length-9);
        string style=initial.Style;if(style!="balanced") name=name.Substring(0,name.Length-style.Length-1);
        bool hasStyle=style!="balanced";HashSet<string> keys=new HashSet<string>(StringComparer.Ordinal);
        PresetRequest result=new PresetRequest();
        for(int i=3;i<words.Length;i++) {
            string token=words[i];
            if(token=="personal" && !personal) {personal=true;continue;}
            if((token=="balanced" || token=="steady" || token=="linear") && !hasStyle) {style=token;hasStyle=true;continue;}
            if(token=="accept-tail" && !result.AcceptTail) {result.AcceptTail=true;continue;}
            string[] parts=token.Split('=');if(parts.Length!=2 || String.IsNullOrEmpty(parts[1]) || !keys.Add(parts[0])) throw new ArgumentException("use each style, range and effect once");
            string key=parts[0],value=parts[1];
            if(key=="stability") {if(value!="on" && value!="off") throw new ArgumentException("stability=on|off");result.Effects.Stability=value=="on";continue;}
            if(key!="smooth" && key!="micro" && key!="recovery" && key!="snap" && key!="left" && key!="right" && key!="up" && key!="down") throw new ArgumentException("unknown preset effect: "+key);
            double n;if(value=="off" && (key=="smooth" || key=="snap" || key=="micro")) n=key=="micro" ? 1 : 0;
            else if(!Double.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out n)) throw new ArgumentException("invalid preset effect number");
            if(key=="smooth") result.Effects.SmoothMs=n;
            else if(key=="micro") result.Effects.MicroScale=n;
            else if(key=="recovery") result.Effects.MicroRecovery=n;
            else if(key=="snap") result.Effects.SnapDegrees=n;
            else {if(result.Effects.Directions==null) result.Effects.Directions=new AimDirections();switch(key) {case "left":result.Effects.Directions.Left=n;break;case "right":result.Effects.Directions.Right=n;break;case "up":result.Effects.Directions.Up=n;break;case "down":result.Effects.Directions.Down=n;break;}}
        }
        result.Effects.Check();result.Name=name+(style=="balanced" ? "" : "-"+style)+(personal ? "-personal" : "");GamePresets.Get(result.Name);return result;
    }
}
}
