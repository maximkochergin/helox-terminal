using System;
using System.Collections.Generic;

namespace Helox {
internal static class SelfTest {
    private static void Expect(bool ok,string name) {if(!ok) throw new Exception("selftest failed: "+name);}
    internal static void Run() {
        List<Sample> samples=new List<Sample>();
        for(int i=0;i<1001;i++) samples.Add(new Sample(i,1,0));
        RateResult rate=Analysis.Rate(samples); Expect(Math.Abs(rate.ObservedHz-1000)<.01,"1000 hz analysis");
        for(int i=0;i<1001;i++) samples[i].Ms=i*8;
        Expect(Math.Abs(Analysis.Rate(samples).ObservedHz-125)<.01,"125 hz analysis");
        samples.Add(new Sample(10000,1,0));Expect(Analysis.Rate(samples).IdleGaps==1,"idle gap exclusion");
        List<Sample> batched=new List<Sample>();
        for(int i=0;i<1001;i++) batched.Add(new Sample((i/8)*8+(i%8)*.01,1,0));
        RateResult batch=Analysis.Rate(batched);
        Expect(Math.Abs(batch.ObservedHz-1000)<.01 && batch.MedianHz>90000,"batched input must not inflate headline hz");
        Expect(batch.Quality.Contains("uneven"),"batched delivery quality warning");
        Expect(Analysis.Distance("10,5")==10.5 && Analysis.Distance("10.5")==10.5,"distance decimal separators");
        bool rejected=false;try {Analysis.Rate(new List<Sample>());} catch(InvalidOperationException){rejected=true;}Expect(rejected,"empty rate rejection");
        List<Sample> stroke=new List<Sample>{new Sample(0,3150,0)};
        Expect(Math.Abs(Analysis.Dpi(stroke,10).EstimatedDpi-800.1)<.01,"distance dpi analysis");
        stroke.Add(new Sample(1,-2000,0));rejected=false;try {Analysis.Dpi(stroke,10);}catch(InvalidOperationException){rejected=true;}Expect(rejected,"backtrack rejection");
        rejected=false;try {Store.Profile("../bad");}catch(ArgumentException){rejected=true;}Expect(rejected,"profile traversal rejection");
        Settings before=Settings.Read();
        Settings clone=Store.Json.Deserialize<Settings>(Store.Json.Serialize(before));Expect(before.Same(clone),"profile roundtrip");
        Settings custom=Store.Json.Deserialize<Settings>(Store.Json.Serialize(before));
        custom.Acceleration=2;custom.Threshold1=7;custom.Threshold2=13;
        custom.Setup();Expect(custom.Acceleration==2 && custom.Threshold1==7 && custom.Threshold2==13,"setup preserves active acceleration");
        custom.SetAcceleration(false);custom.Setup();
        Expect(custom.Acceleration==1 && custom.Threshold1==7 && custom.Threshold2==13,"setup enables acceleration without resetting thresholds");
        // Actual read/write/read verification followed by unconditional restore.
        try {
            clone.Speed=before.Speed==10 ? 11 : 10;clone.Apply();Expect(Settings.Read().Speed==clone.Speed,"real speed readback");
            clone.Acceleration=before.Acceleration==0 ? 1 : 0;clone.Apply();Expect(Settings.Read().Acceleration==clone.Acceleration,"real acceleration readback");
            clone.WheelLines=before.WheelLines==3 ? 4 : 3;clone.Apply();Expect(Settings.Read().WheelLines==clone.WheelLines,"real wheel readback");
            clone.DoubleClickMs=before.DoubleClickMs==500 ? 450 : 500;clone.Apply();Expect(Settings.Read().DoubleClickMs==clone.DoubleClickMs,"real doubleclick readback");
            clone.SwapButtons=before.SwapButtons==0 ? 1 : 0;clone.Apply();Expect(Settings.Read().SwapButtons==clone.SwapButtons,"real button swap readback");
            clone.Speed=0;rejected=false;try {clone.Apply();}catch(ArgumentException){rejected=true;}Expect(rejected,"invalid write rejected");
        } finally {before.Write();Expect(before.Same(Settings.Read()),"original settings restored");}
        List<Device> devices=Device.List();Expect(devices.Count>0,"real raw device enumeration");
        using(RawCapture capture=new RawCapture(devices[0])) {capture.Collect(0,false);}
        using(RawCapture capture=new RawCapture(devices[0])) {
            rejected=false;try {capture.Collect(0,true);}catch(InvalidOperationException) {rejected=true;}
            Expect(rejected,"unfinished calibration timeout rejected");
        }
        Console.WriteLine("  passed / analysis, profiles, native settings readback, restore, raw input registration\n  physical mouse movement measurements require a manual pass");
    }
}
}
