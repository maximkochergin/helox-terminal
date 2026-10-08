using System;
using System.Collections.Generic;

namespace Helox {
// A bounded sensitivity table consumed by the unchanged official driver.
public sealed class AimCurve {
    public double Base {get;set;}
    public double Start {get;set;}
    public double End {get;set;}
    public double Limit {get;set;}
    public double Shape {get;set;}
    public AimCurve() {Base=1;Start=3;End=30;Limit=1.4;Shape=1;}
    internal AimCurve Copy() {return new AimCurve {Base=Base,Start=Start,End=End,Limit=Limit,Shape=Shape};}
    internal void Check() {
        Range(Base,.25,2,"base sensitivity: 0.25..2x");Range(Start,0,1000,"start speed: 0..1000");
        Range(End,.1,2000,"end speed: 0.1..2000");Range(Limit,1,3,"fast / base limit: 1..3x");Range(Shape,.5,3,"transition shape: 0.5..3");
        if(End<Start+.1) throw new ArgumentException("end speed must exceed start by at least 0.1");
    }
    private static void Range(double value,double min,double max,string message) {
        if(double.IsNaN(value) || double.IsInfinity(value) || value<min || value>max) throw new ArgumentException(message);
    }
    internal double Sensitivity(double speed) {
        if(speed<=Start) return Base;if(speed>=End) return Base*Limit;
        double t=Math.Pow((speed-Start)/(End-Start),Shape);
        double blend=t*t*t*(10+t*(-15+6*t));
        return Base*(1+(Limit-1)*blend);
    }
    internal object[] Table() {
        Check();List<object> table=new List<object>();table.Add(0.0);table.Add((double)(float)Base);
        if((float)Start>0) {table.Add((double)(float)Start);table.Add((double)(float)Base);}
        // Round exactly as the native LUT does, before persistence and comparison.
        for(int i=1;i<=128;i++) {
            double speed=Start+(End-Start)*i/128;
            table.Add((double)(float)speed);table.Add((double)(float)Sensitivity(speed));
        }
        // The released lookup extrapolates its final segment. A horizontal tail
        // is required for an actual cap, rather than a merely displayed limit.
        table.Add((double)(float)(End+1));table.Add((double)(float)(Base*Limit));
        return table.ToArray();
    }
    internal Dictionary<string,object> ToMap() {Check();return new Dictionary<string,object>{{"base",Base},{"start",Start},{"end",End},{"limit",Limit},{"shape",Shape}};}
    internal bool Matches(double[] active) {
        object[] table=Table();if(active==null || active.Length!=table.Length) return false;
        for(int i=0;i<table.Length;i++) if((float)active[i]!=(float)Convert.ToDouble(table[i])) return false;
        return true;
    }
    internal static AimCurve FromMap(object value) {
        Dictionary<string,object> map=value as Dictionary<string,object>;
        if(map==null || map.Count!=5) throw new ArgumentException("invalid saved curve");
        AimCurve result=new AimCurve {Base=Number(map,"base"),Start=Number(map,"start"),End=Number(map,"end"),Limit=Number(map,"limit"),Shape=Number(map,"shape")};
        result.Check();return result;
    }
    private static double Number(Dictionary<string,object> map,string key) {
        object value;if(!map.TryGetValue(key,out value) || (!(value is int) && !(value is long) && !(value is double) && !(value is decimal))) throw new ArgumentException("invalid saved curve "+key);
        return Convert.ToDouble(value);
    }
}
}
