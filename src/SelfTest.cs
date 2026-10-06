using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
internal static class SelfTest {
    private static void Expect(bool ok,string name) {if(!ok) throw new Exception("selftest failed: "+name);}
    internal static void Run() {
        Aim.TestEngine();
        List<Sample> samples=new List<Sample>();
        for(int i=0;i<1001;i++) samples.Add(new Sample(i,1,0));
        RateResult rate=Analysis.Rate(samples); Expect(Math.Abs(rate.ObservedHz-1000)<.01,"1000 hz analysis");
        for(int i=0;i<1001;i++) samples[i].Ms=i*8;
        Expect(Math.Abs(Analysis.Rate(samples).ObservedHz-125)<.01,"125 hz analysis");
        samples.Add(new Sample(10000,1,0));Expect(Analysis.Rate(samples).IdleGaps==1 && Analysis.Rate(samples).MaxGapMs==2000 && Analysis.Rate(samples).Quality.Contains("long gaps"),"long gaps remain visible");
        List<Sample> batched=new List<Sample>();
        for(int i=0;i<1001;i++) batched.Add(new Sample((i/8)*8+(i%8)*.01,1,0));
        RateResult batch=Analysis.Rate(batched);
        Expect(Math.Abs(batch.ObservedHz-1000)<.01 && batch.MedianHz>90000,"batched input must not inflate headline hz");
        Expect(batch.Quality.Contains("uneven"),"batched delivery quality warning");
        Expect(Analysis.Distance("10,5")==10.5 && Analysis.Distance("10.5")==10.5,"distance decimal separators");
        bool rejected=false;try {Analysis.Rate(new List<Sample>());} catch(InvalidOperationException){rejected=true;}Expect(rejected,"empty rate rejection");
        List<Sample> shortBurst=new List<Sample>();
        for(int i=0;i<101;i++) shortBurst.Add(new Sample(i*.001,1,0));
        rejected=false;try {Analysis.Rate(shortBurst);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"short delivery burst rejected");
        samples[5].Ms=double.NaN;
        rejected=false;try {Analysis.Rate(samples);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"nonfinite timestamps rejected");
        List<Sample> stroke=new List<Sample>{new Sample(0,3150,0)};
        Expect(Math.Abs(Analysis.Dpi(stroke,10).EstimatedDpi-800.1)<.01,"distance dpi analysis");
        List<DpiResult> passes=new List<DpiResult>{Analysis.Dpi(new List<Sample>{new Sample(0,3900,0)},12.5),
            Analysis.Dpi(new List<Sample>{new Sample(0,4000,0)},12.5),Analysis.Dpi(new List<Sample>{new Sample(0,3950,0)},12.5)};
        DpiResult combined=Analysis.CombineDpi(passes,"test");
        Expect(combined.Trials==3 && combined.Counts==3950 && Math.Abs(combined.EstimatedDpi-802.64)<.01,"body-length three-pass median");
        passes[2]=Analysis.Dpi(new List<Sample>{new Sample(0,7000,0)},12.5);
        rejected=false;try {Analysis.CombineDpi(passes,"test");}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"inconsistent dpi passes rejected");
        stroke.Add(new Sample(1,-2000,0));rejected=false;try {Analysis.Dpi(stroke,10);}catch(InvalidOperationException){rejected=true;}Expect(rejected,"backtrack rejection");
        stroke=new List<Sample>{new Sample(0,1600,3000),new Sample(1,1550,-3000)};
        rejected=false;try {Analysis.Dpi(stroke,10);}catch(InvalidOperationException){rejected=true;}Expect(rejected,"off-axis zigzag rejected");
        rejected=false;try {Store.Profile("../bad");}catch(ArgumentException){rejected=true;}Expect(rejected,"profile traversal rejection");
        Settings before=Settings.Read();
        Settings clone=Store.Json.Deserialize<Settings>(Store.Json.Serialize(before));Expect(before.Same(clone),"profile roundtrip");
        string tempDir=Path.Combine(Path.GetTempPath(),"helox-test-"+Guid.NewGuid().ToString("n"));
        string testProfile=Path.Combine(tempDir,"settings.json");
        try {
            Store.Save(testProfile,before);Expect(before.Same(Store.Load<Settings>(testProfile)),"file profile roundtrip");
            Store.Save(testProfile,clone);Expect(clone.Same(Store.Load<Settings>(testProfile)),"atomic profile overwrite");
            File.WriteAllText(testProfile,"{\"Speed\":10,\"DoubleClickMs\":500}");
            rejected=false;try {Store.Load<Settings>(testProfile);}catch(ArgumentException) {rejected=true;}Expect(rejected,"partial profile rejected");
            string invalidType=Store.Json.Serialize(before).Replace("\"Acceleration\":"+before.Acceleration,"\"Acceleration\":true");
            File.WriteAllText(testProfile,invalidType);
            rejected=false;try {Store.Load<Settings>(testProfile);}catch(ArgumentException) {rejected=true;}Expect(rejected,"noninteger profile field rejected");
            Expect(Directory.GetFiles(tempDir,"*.tmp").Length==0,"no leaked atomic save temporary files");
        }finally {if(File.Exists(testProfile)) File.Delete(testProfile);if(Directory.Exists(tempDir)) Directory.Delete(tempDir);}
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
        foreach(Device device in devices) if(device.TrustCandidate) {
            MouseDossier dossier=MouseDossier.Read(device);
            Expect(dossier.Model!=null && dossier.Model.LengthMm==125,"known model facts");
            Expect(dossier.Hid!=null && dossier.Hid.Count>=1,"selected-device descriptor scope");
            foreach(HidCapability hid in dossier.Hid) Expect(HidProbe.Family(hid.Path)==HidProbe.Family(device.Path),"no unrelated hid collections in dossier");
        }
        using(RawCapture capture=new RawCapture(devices[0])) {capture.Collect(0,false);}
        using(RawCapture capture=new RawCapture(devices[0])) {
            rejected=false;try {capture.Collect(0,true);}catch(InvalidOperationException) {rejected=true;}
            Expect(rejected,"unfinished calibration timeout rejected");
        }
        Console.WriteLine("  passed / analysis, profiles, native settings readback, restore, raw input registration\n  physical mouse movement measurements require a manual pass");
    }
}
}
