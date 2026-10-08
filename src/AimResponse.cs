using System;
using System.Collections;
using System.Collections.Generic;

namespace Helox {
public sealed class AimResponsePoint {
    public int InputCounts {get;set;}
    public double InputCountsPerMs {get;set;}
    public double OutputRatio {get;set;}
}
public sealed class AimDirectionPoint {
    public string Name {get;set;}
    public int InputX {get;set;}
    public int InputY {get;set;}
    public double OutputX {get;set;}
    public double OutputY {get;set;}
    public double OutputRatio {get;set;}
}
public sealed class AimResponse {
    public AimStatus Readback {get;set;}
    public double ExampleIntervalMs {get;set;}
    public double ProcessedIntervalMs {get;set;}
    public string IntervalSource {get;set;}
    public int WarmupReports {get;set;}
    public int BurstReports {get;set;}
    public double SmallMotionRatio {get;set;}
    public double FastMotionRatio {get;set;}
    public AimResponsePoint[] HorizontalSamples {get;set;}
    public AimDirectionPoint[] DirectionSamples {get;set;}
    public long AfterFlickPeakCounts {get;set;}
    public int AfterFlickZeroReports {get;set;}
    public int[] AfterFlickX {get;set;}
    public int[] AfterFlickY {get;set;}
    public string Source {get;set;}
}
// Reproduce the released callback's truncation and fractional carry for example motion.
// No packet injection or kernel writes: this is a calculation diagnostic.
internal sealed class AimCarry {
    internal double X,Y;
    internal int[] Emit(double x,double y) {
        double cx=x+X,cy=y+Y;
        if(!Finite(cx) || !Finite(cy) || cx<Int32.MinValue || cx>Int32.MaxValue || cy<Int32.MinValue || cy>Int32.MaxValue) throw new InvalidOperationException("response simulation exceeds integer mouse range");
        int ox=(int)cx,oy=(int)cy;X=cx-ox;Y=cy-oy;return new int[]{ox,oy};
    }
    private static bool Finite(double n) {return !double.IsNaN(n) && !double.IsInfinity(n);}
}
internal static class AimResponseTest {
    private const string Dpi="DPI (normalizes input speed unit: counts/ms -> in/s)";
    private const string Polling="Polling rate Hz (keep at 0 for automatic adjustment)";
    internal static double Time(Dictionary<string,object> device,double exampleMs) {
        if(double.IsNaN(exampleMs) || double.IsInfinity(exampleMs) || exampleMs<=0) throw new ArgumentException("invalid response example interval");
        double hz=Convert.ToDouble(device[Polling]);
        object constant;
        if(hz>0 && device.TryGetValue("Use constant time interval based on polling rate",out constant) && (bool)constant) return 1000/hz;
        double min=hz>0 ? 1000/hz : device.ContainsKey("minimumTime") ? Convert.ToDouble(device["minimumTime"]) : .0625;
        double max=device.ContainsKey("maximumTime") ? Convert.ToDouble(device["maximumTime"]) : 100;
        return Math.Min(max,Math.Max(min,exampleMs));
    }
    private sealed class Simulation : IDisposable {
        private readonly object engine;
        private readonly double factor,time;
        private readonly bool enabled;
        private readonly AimCarry carry=new AimCarry();
        internal Simulation(object prototype,Dictionary<string,object> device,double dt) {
            object profile=prototype.GetType().GetProperty("Settings").GetValue(prototype,null);
            engine=Activator.CreateInstance(prototype.GetType(),new object[]{profile});enabled=!(bool)device["disable"];time=dt;
            double dpi=Convert.ToDouble(device[Dpi]);factor=dpi>0 ? 1000/dpi : 1;
        }
        internal double[] Step(int x,int y) {
            if(!enabled || (x==0 && y==0)) return new double[]{x,y};
            object pair=Aim.Call(engine,"ManagedAccel","Accelerate",x,y,factor,time);
            return new double[]{Convert.ToDouble(pair.GetType().GetProperty("Item1").GetValue(pair,null)),Convert.ToDouble(pair.GetType().GetProperty("Item2").GetValue(pair,null))};
        }
        internal int[] Packet(int x,int y) {
            // The real callback skips zero motion entirely, including the carry.
            if(!enabled || (x==0 && y==0)) return new int[]{x,y};
            double[] output=Step(x,y);return carry.Emit(output[0],output[1]);
        }
        public void Dispose() {IDisposable disposable=engine as IDisposable;if(disposable!=null) disposable.Dispose();}
    }
    internal static AimResponse Run(Dictionary<string,object> cfg,string id,double exampleMs) {
        AimConfigGuard.Check(cfg);
        AimStatus status=Aim.Describe(cfg,id);Dictionary<string,object> device=Aim.Map(cfg["defaultDeviceConfig"]);
        Dictionary<string,object> entry=Aim.DeviceEntry(cfg,id);if(entry!=null) device=Aim.Map(entry["config"]);
        List<object> profiles=Aim.Items(cfg["profiles"]);int index=profiles.FindIndex(delegate(object p){return (string)Aim.Map(p)["name"]==status.Profile;});
        if(index<0) throw new InvalidOperationException("response profile not found");
        double dt=Time(device,exampleMs);
        if(double.IsInfinity(800/dt)) throw new InvalidOperationException("response interval too small for finite speed examples");
        AimResponse result=new AimResponse {Readback=status,ExampleIntervalMs=exampleMs,ProcessedIntervalMs=dt,WarmupReports=120,BurstReports=8,
            AfterFlickX=new int[16],AfterFlickY=new int[16],Source="official calculation engine + integer carry model / example motion, not game or latency measurement"};
        object valid=Aim.Validate(cfg);IList accels=(IList)valid.GetType().GetField("accels").GetValue(valid);
        try {
            List<AimResponsePoint> points=new List<AimResponsePoint>();
            foreach(int input in new int[]{1,8,24,40,80,160,400,800}) using(Simulation steady=new Simulation(accels[index],device,dt)) {
                double[] output=null;
                for(int i=0;i<result.WarmupReports;i++) output=steady.Step(input,0);
                double ratio=Math.Sqrt(output[0]*output[0]+output[1]*output[1])/input;
                if(double.IsNaN(ratio) || double.IsInfinity(ratio)) throw new InvalidOperationException("nonfinite response simulation");
                points.Add(new AimResponsePoint {InputCounts=input,InputCountsPerMs=input/dt,OutputRatio=ratio});
                if(input==8) result.SmallMotionRatio=ratio;else if(input==800) result.FastMotionRatio=ratio;
            }
            result.HorizontalSamples=points.ToArray();
            List<AimDirectionPoint> directions=new List<AimDirectionPoint>();
            string[] names={"left","right","up","down","diagonal","near horizontal"};int[,] inputs={{-8,0},{8,0},{0,-8},{0,8},{8,-8},{1000,10}};
            for(int n=0;n<names.Length;n++) using(Simulation steady=new Simulation(accels[index],device,dt)) {
                int x=inputs[n,0],y=inputs[n,1];double[] output=null;
                for(int i=0;i<result.WarmupReports;i++) output=steady.Step(x,y);
                directions.Add(new AimDirectionPoint {Name=names[n],InputX=x,InputY=y,OutputX=output[0],OutputY=output[1],OutputRatio=Math.Sqrt(output[0]*output[0]+output[1]*output[1])/Math.Sqrt((double)x*x+(double)y*y)});
            }
            result.DirectionSamples=directions.ToArray();
            using(Simulation recovery=new Simulation(accels[index],device,dt)) {
                for(int i=0;i<result.WarmupReports;i++) recovery.Packet(8,0);
                for(int i=0;i<result.BurstReports;i++) recovery.Packet(800,0);
                for(int i=0;i<16;i++) {
                    int[] packet=recovery.Packet(0,1);result.AfterFlickX[i]=packet[0];result.AfterFlickY[i]=packet[1];
                    result.AfterFlickPeakCounts=Math.Max(result.AfterFlickPeakCounts,Math.Max(Math.Abs((long)packet[0]),Math.Abs((long)packet[1])));
                    if(packet[0]==0 && packet[1]==0) result.AfterFlickZeroReports++;
                }
            }
        }finally {foreach(object accel in accels) {IDisposable disposable=accel as IDisposable;if(disposable!=null) disposable.Dispose();}}
        return result;
    }
}
}
