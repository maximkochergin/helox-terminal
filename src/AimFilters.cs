using System;
using System.Collections.Generic;

namespace Helox {
public sealed class AimDirections {
    public double Left {get;set;}
    public double Right {get;set;}
    public double Up {get;set;}
    public double Down {get;set;}
    public AimDirections() {Left=Right=Up=Down=1;}
    internal AimDirections Copy() {return new AimDirections {Left=Left,Right=Right,Up=Up,Down=Down};}
    internal void Check() {foreach(double n in new double[]{Left,Right,Up,Down}) AimFilterNumbers.Range(n,.25,1,"direction scale: 0.25..1x");}
    internal bool Neutral {get {return Left==1 && Right==1 && Up==1 && Down==1;}}
    internal Dictionary<string,object> ToMap() {Check();return new Dictionary<string,object>{{"left",Left},{"right",Right},{"up",Up},{"down",Down}};}
    internal static AimDirections FromMap(object value) {
        Dictionary<string,object> map=AimFilterNumbers.Map(value,4,"directions");
        AimDirections result=new AimDirections {Left=AimFilterNumbers.Number(map,"left"),Right=AimFilterNumbers.Number(map,"right"),Up=AimFilterNumbers.Number(map,"up"),Down=AimFilterNumbers.Number(map,"down")};result.Check();return result;
    }
    internal static AimDirections Read(Dictionary<string,object> profile) {
        double r=Convert.ToDouble(profile["Output DPI"])/1000,d=r*Convert.ToDouble(profile["Y/X output DPI ratio (vertical sens multiplier)"]);
        return new AimDirections {Right=Roundoff(r),Left=Roundoff(r*Convert.ToDouble(profile["L/R output DPI ratio (left sens multiplier)"])),Down=Roundoff(d),Up=Roundoff(d*Convert.ToDouble(profile["U/D output DPI ratio (up sens multiplier)"]))};
    }
    private static double Roundoff(double n) {return n<.25 && n>=.25-1e-12 ? .25 : n>1 && n<=1+1e-12 ? 1 : n;}
    internal void Apply(Dictionary<string,object> profile) {
        Check();profile["Output DPI"]=1000*Right;profile["Y/X output DPI ratio (vertical sens multiplier)"]=Down/Right;
        profile["L/R output DPI ratio (left sens multiplier)"]=Left/Right;profile["U/D output DPI ratio (up sens multiplier)"]=Up/Down;
    }
}
public sealed class AimDamping {
    public bool Enabled {get;set;}
    public double LowScale {get;set;}
    public double RecoverySpeed {get;set;}
    public AimDamping() {LowScale=.75;RecoverySpeed=1;}
    internal AimDamping Copy() {return new AimDamping {Enabled=Enabled,LowScale=LowScale,RecoverySpeed=RecoverySpeed};}
    internal bool Active {get {return Enabled && LowScale<1;}}
    internal void Check() {AimFilterNumbers.Range(LowScale,.25,1,"micro low scale: 0.25..1x");AimFilterNumbers.Range(RecoverySpeed,.1,20,"micro recovery speed: 0.1..20");}
    internal double Scale(double speed) {double t=Math.Min(1,Math.Max(0,speed/RecoverySpeed));return LowScale+(1-LowScale)*t*t*(3-2*t);}
    internal Dictionary<string,object> ToMap() {Check();return new Dictionary<string,object>{{"enabled",Enabled},{"lowScale",LowScale},{"recoverySpeed",RecoverySpeed}};}
    internal static AimDamping FromMap(object value) {
        Dictionary<string,object> map=AimFilterNumbers.Map(value,3,"micro damping");object enabled;
        if(!map.TryGetValue("enabled",out enabled) || !(enabled is bool)) throw new ArgumentException("invalid saved micro toggle");
        AimDamping result=new AimDamping {Enabled=(bool)enabled,LowScale=AimFilterNumbers.Number(map,"lowScale"),RecoverySpeed=AimFilterNumbers.Number(map,"recoverySpeed")};result.Check();return result;
    }
}
internal static class AimFilterNumbers {
    internal static void Range(double n,double min,double max,string message) {if(double.IsNaN(n) || double.IsInfinity(n) || n<min || n>max) throw new ArgumentException(message);}
    internal static Dictionary<string,object> Map(object value,int count,string name) {Dictionary<string,object> map=value as Dictionary<string,object>;if(map==null || map.Count!=count) throw new ArgumentException("invalid saved "+name);return map;}
    internal static double Number(Dictionary<string,object> map,string key) {object n;if(!map.TryGetValue(key,out n) || (!(n is int) && !(n is long) && !(n is double) && !(n is decimal))) throw new ArgumentException("invalid saved "+key);return Convert.ToDouble(n);}
}
internal static class AimLookup {
    internal static double Natural(double speed,double limit) {
        if(speed<=3) return 1;double span=limit-1,a=.05/span,d=speed-3;
        return 1+span*(d+(Math.Exp(-a*d)-1)/a)/speed;
    }
    internal static object[] Table(AimCurve curve,double gain,AimDamping damping) {
        Aim.CheckGain(gain);damping.Check();if(curve!=null) curve.Check();
        if(!damping.Active) return curve==null ? null : curve.Table();
        SortedSet<float> speeds=new SortedSet<float>();speeds.Add(0);object[] original=curve==null ? null : curve.Table();
        if(original!=null) {for(int i=0;i<original.Length;i+=2) speeds.Add((float)Convert.ToDouble(original[i]));}
        else {
            speeds.Add(3);for(int i=0;i<=128;i++) speeds.Add((float)(3+.001*Math.Pow(32768000,i/128.0)));
        }
        for(int i=1;i<=64;i++) speeds.Add((float)(damping.RecoverySpeed*i/64));
        List<object> data=new List<object>();double lastSpeed=0,lastValue=0;
        foreach(float speed in speeds) {
            double value=(original==null ? Natural(speed,gain) : Interpolate(original,speed))*damping.Scale(speed);
            data.Add((double)speed);data.Add((double)(float)value);lastSpeed=speed;lastValue=(double)(float)value;
        }
        // The native implementation extrapolates. Keep its last segment flat.
        data.Add((double)(float)(lastSpeed+1));data.Add(lastValue);
        if(data.Count>514) throw new ArgumentException("micro lookup exceeds native capacity");
        return data.ToArray();
    }
    internal static double Interpolate(object[] table,double speed) {
        for(int i=2;i<table.Length;i+=2) if(speed<=Convert.ToDouble(table[i])) {
            double a=Convert.ToDouble(table[i-2]),b=Convert.ToDouble(table[i]),lo=Convert.ToDouble(table[i-1]),hi=Convert.ToDouble(table[i+1]);return lo+(hi-lo)*(speed-a)/(b-a);
        }
        return Convert.ToDouble(table[table.Length-1]);
    }
    internal static bool Matches(AimPreset preset,double[] active) {
        object[] table=Table(preset.Curve,preset.GainLimit,preset.Damping);
        if(table==null || active==null || table.Length!=active.Length) return false;
        for(int i=0;i<table.Length;i++) if((float)active[i]!=(float)Convert.ToDouble(table[i])) return false;
        return true;
    }
}
}
