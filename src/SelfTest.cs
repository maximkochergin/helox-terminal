using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Helox {
internal static class SelfTest {
    private static void Expect(bool ok,string name) {if(!ok) throw new Exception("selftest failed: "+name);}
    internal static void Run(bool native=true) {
        ConfigGuardRegression();
        AimRegression();
        CurveRegression();
        FilterRegression();
        GameRegression();
        DeviceStackRegression();
        UndoRegression();
        SnapshotRegression();
        MaintenanceRegression();
        List<string> mouseArgs=new List<string>{"status","--mouse","0"};
        Expect(Program.MouseOption(mouseArgs)==0 && mouseArgs.Count==1 && mouseArgs[0]=="status","mouse option leaves command intact");
        foreach(string[] invalid in new string[][] {new string[]{"--mouse"},new string[]{"--mouse","-1"},new string[]{"--mouse","2147483648"},new string[]{"--mouse","0","--mouse","1"}}) {
            bool mouseRejected=false;try {Program.MouseOption(new List<string>(invalid));}catch(ArgumentException) {mouseRejected=true;}
            Expect(mouseRejected,"invalid mouse option rejected before selection");
        }
        Expect(ServiceProbe.Failure("vgk",5,false).Installed==null,"service access denied is not absence");
        Expect(ServiceProbe.Failure("vgk",1060,false).Installed==false,"service missing is explicit");
        Expect(ServiceProbe.Failure("vgk",5,true).Installed==true,"failed query preserves known service existence");
        Program.RequireDpiInput(false);
        foreach(bool? transformed in new bool?[]{true,null}) {
            bool blocked=false;try {Program.RequireDpiInput(transformed);}catch(InvalidOperationException) {blocked=true;}
            Expect(blocked,"dpi refuses active and unknown filter states");
        }
        Device first=new Device {Path="first"},second=new Device {Path="second"};
        List<Device> displayed=new List<Device>{first,second};
        Expect(Program.DefaultMouse(new List<Device>{first})==first,"single non-trust mouse is usable without selection");
        Device trust=new Device {Path="trust",TrustCandidate=true};
        Expect(Program.DefaultMouse(new List<Device>{first,trust})==trust,"existing trust default selection remains stable");
        foreach(List<Device> list in new List<Device>[] {new List<Device>(),displayed,new List<Device>{trust,new Device {Path="other-trust",TrustCandidate=true}}}) {
            bool ambiguous=false;try {Program.DefaultMouse(list);}catch(InvalidOperationException) {ambiguous=true;}
            Expect(ambiguous,"absent or ambiguous mice require explicit selection");
        }
        Program.RequirePresetMouse("FIRST",first);
        foreach(Device replacement in new Device[]{second,null}) {
            bool blocked=false;try {Program.RequirePresetMouse(first.Path,replacement);}catch(InvalidOperationException) {blocked=true;}
            Expect(blocked,"preset draft refuses a replacement or disconnected mouse");
        }
        Expect(Program.ResolveChoice(displayed,1,new List<Device>{second,first})==first,"first mouse remains selectable after enumeration reorder");
        bool choiceRejected=false;
        try {Program.ResolveChoice(displayed,1,new List<Device>{second});}catch(InvalidOperationException) {choiceRejected=true;}
        Expect(choiceRejected,"disconnected menu mouse must not select a replacement");
        foreach(int number in new int[]{0,3}) {
            choiceRejected=false;try {Program.ResolveChoice(displayed,number,displayed);}catch(ArgumentException) {choiceRejected=true;}
            Expect(choiceRejected,"mouse menu range validation");
        }
        if(native) Aim.TestEngine();
        List<Sample> samples=new List<Sample>();
        for(int i=0;i<1001;i++) samples.Add(new Sample(i,1,0));
        RateResult rate=Analysis.Rate(samples); Expect(Math.Abs(rate.ObservedHz-1000)<.01,"1000 hz analysis");
        rate.DevicePath="mouse";
        RateResult previous=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(rate));previous.ActiveHz=900;previous.P95IntervalMs=2;previous.P99IntervalMs=2;
        RateComparison compared=Analysis.CompareRates(previous,rate);
        Expect(compared!=null && compared.ActiveHzDifference==100 && compared.P95IntervalDifferenceMs==-1,"rate comparison reports signed same-device deltas");
        RateResult longRun=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(rate));
        longRun.Reports=2001;longRun.Intervals=2000;longRun.SlowIntervals=40;longRun.SpanMs=2000;longRun.P99IntervalMs=2;
        RateResult shortRun=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(rate));shortRun.SlowIntervals=20;
        RateComparison normalized=Analysis.CompareRates(longRun,shortRun);
        Expect(normalized.SlowIntervalPercentDifference==0 && normalized.P99IntervalDifferenceMs==-1,"comparison normalizes slow intervals across different run sizes");
        previous.DevicePath="other";Expect(Analysis.CompareRates(previous,rate)==null,"rate comparison rejects another device");
        previous.DevicePath="mouse";previous.ActiveHz=double.NaN;
        Expect(!Analysis.ValidHistory(previous) && Analysis.CompareRates(previous,rate)==null,"damaged rate history is not compared");
        Expect(!Analysis.ValidHistory(new DpiResult {EstimatedDpi=800,DevicePath="mouse"}),"partial dpi history rejected");
        foreach(Action<RateResult> corrupt in new Action<RateResult>[] {
            delegate(RateResult value) {value.MeasuredUtc="12:34";},
            delegate(RateResult value) {value.MeasuredUtc="2026-10-06T12:34:00.0000000";},
            delegate(RateResult value) {value.P99IntervalMs=double.NaN;},
            delegate(RateResult value) {value.P99IntervalMs=.5;},
            delegate(RateResult value) {value.SlowIntervals=-1;},
            delegate(RateResult value) {value.SameTimestampReports=value.Intervals+1;},
            delegate(RateResult value) {value.MaxGapMs=double.PositiveInfinity;},
            delegate(RateResult value) {value.Comparison=new RateComparison {PreviousMeasuredUtc=value.MeasuredUtc,ActiveHzDifference=double.NaN};}
        }) {
            RateResult damaged=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(rate));corrupt(damaged);
            Expect(!Analysis.ValidHistory(damaged),"invalid optional rate metrics or timestamps rejected");
        }
        RateResult legacy=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(rate));
        legacy.P99IntervalMs=null;legacy.MaxGapMs=null;legacy.SlowIntervals=null;legacy.SameTimestampReports=null;
        legacy.SpanMs=null;legacy.GapDurationMs=null;legacy.DeliveredHz=null;legacy.GapPercent=null;legacy.SlowThresholdMs=null;
        Expect(Analysis.ValidHistory(legacy),"legacy history without optional metrics remains valid");
        normalized=Analysis.CompareRates(legacy,rate);
        Expect(normalized!=null && normalized.GapPercentDifference==null && normalized.SlowIntervalPercentDifference==null && normalized.P99IntervalDifferenceMs==null,"legacy comparison never invents missing metrics");
        legacy.MeasuredUtc=DateTime.UtcNow.AddDays(1).ToString("o");
        Expect(Analysis.CompareRates(legacy,rate)==null,"future baseline is not compared with an older test");
        for(int i=0;i<1001;i++) samples[i].Ms=i*8;
        Expect(Math.Abs(Analysis.Rate(samples).ObservedHz-125)<.01,"125 hz analysis");
        samples.Add(new Sample(10000,1,0));Expect(Analysis.Rate(samples).IdleGaps==1 && Analysis.Rate(samples).MaxGapMs==2000 && Analysis.Rate(samples).Quality.Contains("long gaps"),"long gaps remain visible");
        RateResult paused=Analysis.Rate(samples);
        Expect(paused.GapDurationMs==2000 && paused.SpanMs==10000 && paused.GapPercent==20 && paused.DeliveredHz<paused.ActiveHz,"span frequency exposes gaps excluded from active hz");
        paused.DevicePath="mouse";paused.MeasuredUtc=rate.MeasuredUtc;
        normalized=Analysis.CompareRates(paused,rate);
        Expect(normalized.GapPercentDifference==-20 && normalized.ContextNote!=null && normalized.DeliveredHzDifference>0,"comparison exposes pause changes and warns about context");
        List<Sample> delayed125=new List<Sample>();double elapsed=0;
        for(int i=0;i<=1000;i++) {delayed125.Add(new Sample(elapsed,1,0));elapsed+=i==500 ? 16 : 8;}
        RateResult delayed=Analysis.Rate(delayed125);
        Expect(delayed.SlowIntervals==1 && delayed.SlowThresholdMs==14 && delayed.IdleGaps==0,"125 hz test detects a doubled delivery interval");
        foreach(Action<RateResult> corrupt in new Action<RateResult>[] {
            delegate(RateResult value) {value.GapPercent=101;},
            delegate(RateResult value) {value.GapDurationMs=value.SpanMs+1;},
            delegate(RateResult value) {value.DeliveredHz=double.NaN;},
            delegate(RateResult value) {value.SlowThresholdMs=0;}
        }) {
            RateResult damaged=Store.Json.Deserialize<RateResult>(Store.Json.Serialize(delayed));corrupt(damaged);
            Expect(!Analysis.ValidHistory(damaged),"invalid span metrics rejected");
        }
        List<Sample> batched=new List<Sample>();
        for(int i=0;i<1001;i++) batched.Add(new Sample((i/8)*8+(i%8)*.01,1,0));
        RateResult batch=Analysis.Rate(batched);
        Expect(Math.Abs(batch.ObservedHz-1000)<.01 && batch.MedianHz>90000,"batched input must not inflate headline hz");
        Expect(batch.Quality.Contains("uneven"),"batched delivery quality warning");
        batch.DevicePath="mouse";batch.MeasuredUtc=rate.MeasuredUtc;
        Expect(Analysis.CompareRates(batch,rate).ContextNote.Contains("batched"),"batched comparison includes a timing caveat");
        List<Sample> equalTimes=new List<Sample>();
        for(int i=0;i<=1000;i++) equalTimes.Add(new Sample((i/2)*2,1,0));
        RateResult simultaneous=Analysis.Rate(equalTimes);
        Expect(Math.Abs(simultaneous.ActiveHz-1000)<.01 && simultaneous.Intervals==1000 && simultaneous.SameTimestampReports==500,"same timestamp reports count towards delivered hz");
        Expect(Analysis.Distance("10,5")==10.5 && Analysis.Distance("10.5")==10.5,"distance decimal separators");
        bool rejected=false;try {Analysis.Rate(new List<Sample>());} catch(InvalidOperationException){rejected=true;}Expect(rejected,"empty rate rejection");
        List<Sample> shortBurst=new List<Sample>();
        for(int i=0;i<101;i++) shortBurst.Add(new Sample(i*.001,1,0));
        rejected=false;try {Analysis.Rate(shortBurst);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"short delivery burst rejected");
        samples[5].Ms=double.NaN;
        rejected=false;try {Analysis.Rate(samples);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"nonfinite timestamps rejected");
        List<Sample> stroke=new List<Sample>{new Sample(0,3150,0)};
        Expect(Math.Abs(Analysis.Dpi(stroke,10).EstimatedDpi-800.1)<.01,"distance dpi analysis");
        DpiResult dpiHistory=Analysis.Dpi(stroke,10);Expect(Analysis.ValidHistory(dpiHistory),"legacy dpi history without trial details remains valid");
        dpiHistory.SpreadPercent=double.NaN;Expect(!Analysis.ValidHistory(dpiHistory),"nonfinite dpi spread rejected");
        dpiHistory.SpreadPercent=null;dpiHistory.TrialDpi=new List<double>{double.PositiveInfinity};
        Expect(!Analysis.ValidHistory(dpiHistory),"nonfinite dpi trial history rejected");
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
        rejected=false;try {Store.Profile("valid\n");}catch(ArgumentException){rejected=true;}Expect(rejected,"profile names reject trailing line breaks");
        foreach(string reserved in new string[]{"con","prn","aux","nul","com1","lpt9"}) {
            rejected=false;try {Store.Profile(reserved);}catch(ArgumentException) {rejected=true;}Expect(rejected,"reserved windows profile name rejected");
        }
        if(!native) {Console.WriteLine("  passed / analysis, aim recovery and profile validation / no settings changed");return;}
        Settings before=Settings.Read();
        AimStatus unselectedAim=Aim.Read(null);
        Expect(unselectedAim.State!="ready" && !String.IsNullOrEmpty(unselectedAim.Note),"aim status remains readable without a selected mouse");
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
            foreach(string invalid in new string[]{"[]","42","\"profile\"","null","{"}) {
                File.WriteAllText(testProfile,invalid);
                rejected=false;try {Store.Load<Settings>(testProfile);}catch(ArgumentException) {rejected=true;}
                Expect(rejected,"nonobject or malformed profile rejected with a usable error");
            }
            string imported=Path.Combine(tempDir,"Imported.JSON"),unusable=Path.Combine(tempDir,"bad name.json");
            try {
                Store.Save(imported,before);File.WriteAllText(unusable,"{}");
                List<string> listed=Store.Profiles(tempDir);
                Expect(listed.Count==2 && listed[0]=="imported" && listed[1]=="settings","profile list normalizes imported names and excludes unusable names");
                Expect(before.Same(Store.Load<Settings>(Path.Combine(tempDir,listed[0]+".json"))),"listed imported profile is loadable on windows");
            }finally {if(File.Exists(imported)) File.Delete(imported);if(File.Exists(unusable)) File.Delete(unusable);}
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
    private static void UndoRegression() {
        Settings before=new Settings {Speed=10,DoubleClickMs=500,WheelLines=3};
        Settings after=new Settings {Speed=11,DoubleClickMs=500,WheelLines=3};
        Settings live=before,saved=null;int writes=0;
        Action<Settings> apply=delegate(Settings value) {live=value;writes++;};
        Store.CommitChange(before,after,apply,delegate(Settings value) {saved=value;});
        Expect(live==after && saved==before && writes==1,"last change saves previous snapshot");
        Store.CommitChange(after,after,apply,delegate(Settings value) {throw new Exception("no-op must not replace undo");});
        Expect(writes==1,"unchanged settings preserve undo and avoid writes");
        bool rejected=false;
        try {Store.CommitChange(before,after,apply,delegate(Settings value) {throw new IOException("disk full");});}
        catch(IOException e) {rejected=e.Message.Contains("previous windows settings restored");}
        Expect(rejected && live==before,"failed undo persistence restores windows settings");
        rejected=false;
        try {Store.CommitChange(before,after,delegate(Settings value) {if(value==before) throw new IOException("restore blocked");},delegate(Settings value) {throw new IOException("disk full");});}
        catch(IOException e) {rejected=e.Message.Contains("disk full") && e.Message.Contains("rollback failed") && e.Message.Contains("restore blocked");}
        Expect(rejected,"undo persistence and rollback failures both reported");
        saved=null;rejected=false;
        try {Store.CommitChange(before,after,delegate(Settings value) {throw new IOException("apply failed");},delegate(Settings value) {saved=value;});}
        catch(IOException) {rejected=true;}
        Expect(rejected && saved==null,"failed apply leaves previous undo untouched");
    }
    private static void MaintenanceRegression() {
        foreach(string key in new string[]{"BackendPrepared","PackageVerified","BackendVerified"})
            foreach(object unsafeValue in new object[]{false,null,"true"}) {
                Dictionary<string,object> report=new Dictionary<string,object>{{"BackendPrepared",true},{"PackageVerified",true},{"BackendVerified",true}};
                report[key]=unsafeValue;int reads=0;
                Maintenance.AddKernelCheck(report,delegate {reads++;return "1.7.0";});
                Expect(reads==0 && report["KernelReadable"]==null && report["KernelVersion"]==null,"unverified backend never reaches native readback");
            }
        Dictionary<string,object> good=new Dictionary<string,object>{{"BackendPrepared",true},{"PackageVerified",true},{"BackendVerified",true}};
        Maintenance.AddKernelCheck(good,delegate {return "1.7.0";});
        Expect(Object.Equals(good["KernelReadable"],true) && (string)good["KernelVersion"]=="1.7.0","verified backend records live protocol");
        Maintenance.AddKernelCheck(good,delegate {throw new IOException("kernel unavailable");});
        Expect(Object.Equals(good["KernelReadable"],false) && (string)good["KernelError"]=="kernel unavailable","kernel failure remains separate from unverified backend");
        string root=Path.Combine(Path.GetTempPath(),"helox-reset-"+Guid.NewGuid().ToString("n"));Directory.CreateDirectory(root);
        string original=Path.Combine(root,"original.json"),aim=Path.Combine(root,"aim-before.json");
        try {
            Store.Save(original,new Settings {Speed=10,WheelLines=3,DoubleClickMs=500});
            File.WriteAllText(aim,"{\"version\":\"1.7.0\"}");
            bool rejected=false;int validations=0;
            try {Maintenance.ValidateResetBackups(root,true,delegate(Dictionary<string,object> cfg) {validations++;AimConfigGuard.Check(cfg);});}
            catch(ArgumentException) {rejected=true;}
            Expect(rejected && validations==1 && Store.Load<Settings>(original).Speed==10,"invalid aim backup rejected without modifying windows backup");
            validations=0;Maintenance.ValidateResetBackups(root,false,delegate(Dictionary<string,object> cfg) {validations++;});
            Expect(validations==0,"unloaded driver does not require unused aim backup");
            rejected=false;try {Maintenance.ValidateResetBackups(root,null,delegate(Dictionary<string,object> cfg) {validations++;});}catch(InvalidOperationException) {rejected=true;}
            Expect(rejected && validations==0,"unknown endpoint blocks reset before native conversion");
            File.WriteAllText(original,"{\"Speed\":10}");
            rejected=false;try {Maintenance.ValidateResetBackups(root,true,delegate(Dictionary<string,object> cfg) {validations++;});}catch(ArgumentException) {rejected=true;}
            Expect(rejected && validations==0,"invalid windows backup blocks aim validation too");
        }finally {if(File.Exists(original)) File.Delete(original);if(File.Exists(aim)) File.Delete(aim);Directory.Delete(root);}
    }
    private static void SnapshotRegression() {
        string path=Path.Combine(Path.GetTempPath(),"helox-snapshot-"+Guid.NewGuid().ToString("n")+".json");
        Settings original=new Settings {Speed=10,Threshold1=6,Threshold2=10,Acceleration=1,WheelLines=3,DoubleClickMs=500,SwapButtons=0};
        Expect(original.PreviewChanges(original).Length==0,"matching profile has no pending changes");
        Settings different=new Settings {Speed=12,Threshold1=7,Threshold2=11,Acceleration=2,WheelLines=-1,DoubleClickMs=600,SwapButtons=1};
        string[] changes=different.PreviewChanges(original);
        Expect(changes.Length==6 && changes[0]=="speed / 10 -> 12 / 20" && changes[1]=="acceleration / on (mode 1) -> on (mode 2)" &&
            changes[2]=="thresholds / 6, 10 -> 7, 11" && changes[3]=="wheel / 3 lines -> page" &&
            changes[4]=="doubleclick / 500 ms -> 600 ms" && changes[5]=="buttons / normal -> swapped","profile preview covers every field and preserves direction");
        Settings wheelOnly=new Settings {Speed=10,Threshold1=6,Threshold2=10,Acceleration=1,WheelLines=0,DoubleClickMs=500,SwapButtons=0};
        Expect(wheelOnly.PreviewChanges(original).Length==1 && wheelOnly.PreviewChanges(original)[0]=="wheel / 3 lines -> 0 lines","profile preview hides unchanged values and handles zero scrolling");
        try {
            Store.Save(path,original);Expect(original.Same(Store.Load<Settings>(path)),"validated snapshot roundtrip");
            string valid=File.ReadAllText(path);
            foreach(string alias in new string[]{"\"speed\":\"20\"","\"SWAPBUTTONS\":1","\"acceleration\":\"0\""})
                foreach(bool first in new bool[]{true,false}) {
                    File.WriteAllText(path,first ? "{"+alias+","+valid.Substring(1) : valid.Substring(0,valid.Length-1)+","+alias+"}");
                    bool rejected=false;try {Store.Load<Settings>(path);}catch(ArgumentException) {rejected=true;}
                    Expect(rejected,"ambiguous snapshot rejected regardless of field order");
                }
        } finally {if(File.Exists(path)) File.Delete(path);}
    }
    private static void CurveRegression() {
        Dictionary<string,object> defaults=Aim.Parse(File.ReadAllText(Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..")),"tests","fixtures","rawaccel-default.json")));
        AimCurve curve=new AimCurve {Base=.8,Start=2,End=24,Limit=1.8,Shape=1.5};
        object[] table=curve.Table();Expect(table.Length<=514 && table.Length%2==0,"curve fits the official table ABI");
        float last=-1;double previous=0;
        for(int i=0;i<table.Length;i+=2) {
            float speed=(float)Convert.ToDouble(table[i]);double scale=Convert.ToDouble(table[i+1]);
            Expect(speed>last && scale>=previous && scale>0 && scale<=curve.Base*curve.Limit+1e-6,"curve is positive, bounded and native-monotonic");last=speed;previous=scale;
        }
        Expect(curve.Sensitivity(1)==.8 && Math.Abs(curve.Sensitivity(30)-1.44)<1e-10,"curve has independent base and capped fast sensitivity");
        Expect(Math.Abs(curve.Sensitivity(curve.Start+1e-6)-curve.Base)<1e-9 && Math.Abs(curve.Sensitivity(curve.End-1e-6)-curve.Base*curve.Limit)<1e-9,"curve joins both plateaus continuously");
        foreach(Action<AimCurve> change in new Action<AimCurve>[] {
            delegate(AimCurve c){c.Base=0;},delegate(AimCurve c){c.Base=double.NaN;},delegate(AimCurve c){c.End=c.Start;},delegate(AimCurve c){c.End=c.Start+.01;},
            delegate(AimCurve c){c.Limit=4;},delegate(AimCurve c){c.Shape=0;},delegate(AimCurve c){c.Start=double.PositiveInfinity;}
        }) {AimCurve bad=curve.Copy();change(bad);bool blocked=false;try {bad.Table();}catch(ArgumentException) {blocked=true;}Expect(blocked,"invalid curve is rejected before driver access");}
        AimPreset preset=new AimPreset {Precision=true,Curve=curve,SnapDegrees=1,Stability=true,StabilityMs=10};
        AimPreset saved=Aim.ReadPreset(Aim.Parse(Store.Json.Serialize(preset.ToMap())));
        Expect(Aim.SameValue(saved.Curve.ToMap(),curve.ToMap()) && saved.SnapDegrees==1,"curve and optional snapping persist");
        Dictionary<string,object> configured=Aim.ConfigurePreset(defaults,defaults,"HID\\CURVE",preset,true);AimConfigGuard.Check(configured);
        AimStatus status=Aim.Describe(configured,"HID\\CURVE");
        Expect(status.Mode=="lut" && status.GainLimit==null && status.LookupIsSensitivity==true && status.StabilityEnabled==true && status.SnapDegrees==1 && curve.Matches(status.LookupData),"custom curve and independent filters have live readback without an unused natural limit");
        foreach(string feature in new string[]{"smooth","stability","snap","precision","tracking"}) {
            AimPreset next=Aim.Resolve(status,preset.ToMap(),feature,feature=="tracking");
            Expect(next.Curve!=null && Aim.SameValue(next.Curve.ToMap(),curve.ToMap()),"component switches preserve the personal curve");
            Dictionary<string,object> changed=Aim.ConfigurePreset(configured,defaults,"HID\\CURVE",next);
            Expect(Aim.SameValue(defaults["defaultDeviceConfig"],changed["defaultDeviceConfig"]),"curve changes preserve the default device");
        }
        AimPreset off=Aim.Resolve(status,preset.ToMap(),"precision",false);
        AimStatus offStatus=Aim.Describe(Aim.ConfigurePreset(configured,defaults,"HID\\CURVE",off),"HID\\CURVE");
        AimPreset on=Aim.Resolve(offStatus,off.ToMap(),"precision",true);
        Expect(on.Curve!=null && on.SnapDegrees==1,"precision off/on remembers the custom curve and independent snapping");
        AimPreset natural=Aim.Resolve(status,preset.ToMap(),"curve",true);Expect(natural.Curve==null && natural.SnapDegrees==1,"natural reset is explicit and preserves snapping");
        AimPreset gain=Aim.Resolve(status,preset.ToMap(),"precision",true,null,1.6);Expect(gain.Curve==null && gain.GainLimit==1.6,"explicit natural gain switches away from LUT");
        status.LookupIsSensitivity=false;bool semanticMismatch=false;try {Aim.Resolve(status,preset.ToMap(),"snap",false);}catch(InvalidOperationException) {semanticMismatch=true;}
        Expect(semanticMismatch,"external LUT interpretation changes are not silently replaced");status.LookupIsSensitivity=true;
        status.LookupData[3]+=.1;bool mismatch=false;try {Aim.Resolve(status,preset.ToMap(),"smooth",false);}catch(InvalidOperationException) {mismatch=true;}
        Expect(mismatch,"external LUT changes are not silently overwritten by component switches");
        foreach(object bad in new object[]{"1",true,-1,6,double.NaN,double.PositiveInfinity}) {
            Dictionary<string,object> values=preset.ToMap();values["snapDegrees"]=bad;bool blocked=false;try {Aim.ReadPreset(values);}catch(ArgumentException) {blocked=true;}Expect(blocked,"invalid saved snapping is blocked");
        }
        foreach(object bad in new object[]{"curve",true,new Dictionary<string,object>{{"base",1}},new Dictionary<string,object>{{"base",1},{"start",0},{"end",1},{"limit",2},{"shape",1},{"extra",1}}}) {
            Dictionary<string,object> values=preset.ToMap();values["curve"]=bad;bool blocked=false;try {Aim.ReadPreset(values);}catch(ArgumentException) {blocked=true;}Expect(blocked,"invalid saved curves are blocked");
        }
        Expect(!ShutdownDiagnostics.IsAim("DeviceDriver.exe","ntdll.dll") && ShutdownDiagnostics.IsAim("HELOX.EXE","ntdll.dll"),"shutdown diagnostics do not confuse unrelated device software with helox");
        DateTime now=DateTime.UtcNow;List<DateTime> times=new List<DateTime>{now};
        Expect(ShutdownDiagnostics.Near(now.AddSeconds(120),times) && !ShutdownDiagnostics.Near(now.AddSeconds(121),times),"shutdown proximity is bounded");
        Expect(ShutdownDiagnostics.Proximity(now.AddSeconds(121),times,false)==null && ShutdownDiagnostics.Proximity(now,times,false)==true && ShutdownDiagnostics.Proximity(now.AddSeconds(121),times,true)==false,"incomplete shutdown journal leaves unmatched timing unknown");
        Program.RequireCurveContext("mouse","counts/ms",new Device {Path="MOUSE"},new AimStatus {State="ready",CurveSpeedUnit="counts/ms"});
        foreach(bool otherMouse in new bool[]{true,false}) {
            bool blocked=false;try {Program.RequireCurveContext("mouse","counts/ms",new Device {Path=otherMouse ? "other" : "mouse"},new AimStatus {State="ready",CurveSpeedUnit=otherMouse ? "counts/ms" : "in/s"});}catch(InvalidOperationException) {blocked=true;}
            Expect(blocked,"builder refuses changed mouse identity or curve units");
        }
        AimConfigGuard.Check(Aim.ConfigurePreset(defaults,defaults,"HID\\TINY",new AimPreset {Precision=true,Curve=new AimCurve {Start=double.Epsilon}},true));
    }
    private static void FilterRegression() {
        Dictionary<string,object> defaults=Aim.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","fixtures","rawaccel-default.json")));
        AimDirections weights=new AimDirections {Left=.3,Right=.7,Up=.4,Down=.9};
        AimPreset preset=new AimPreset {Precision=true,Curve=new AimCurve(),SnapDegrees=2,SnapStrength=2,SpeedUnit="counts/ms",Directions=weights,Damping=new AimDamping {Enabled=true,LowScale=.75,RecoverySpeed=1}};
        Dictionary<string,object> cfg=Aim.ConfigurePreset(defaults,defaults,"HID\\FILTER",preset,true);AimConfigGuard.Check(cfg);AimStatus status=Aim.Describe(cfg,"HID\\FILTER");
        AimPreset restored=Aim.ReadPreset(Aim.Parse(Store.Json.Serialize(preset.ToMap())));
        Expect(Aim.SameValue(weights.ToMap(),restored.Directions.ToMap()) && restored.Damping.Enabled && restored.SpeedUnit=="counts/ms","filters and units roundtrip");
        Expect(AimLookup.Matches(restored,status.LookupData),"combined table has ownership match");
        foreach(double recover in new double[]{.1,1,20}) foreach(AimCurve curve in new AimCurve[]{null,new AimCurve(),new AimCurve {Start=0,End=.1,Base=.25,Limit=3,Shape=.5},new AimCurve {Start=1000,End=1000.1,Base=2,Limit=3,Shape=3}}) {
            AimDamping damping=new AimDamping {Enabled=true,LowScale=.25,RecoverySpeed=recover};object[] table=AimLookup.Table(curve,1.4,damping);float previous=-1;
            Expect(table.Length<=514 && table.Length%2==0,"combined filters fit native ABI");
            for(int i=0;i<table.Length;i+=2) {float speed=(float)Convert.ToDouble(table[i]);double scale=Convert.ToDouble(table[i+1]);Expect(speed>previous && scale>0 && scale<=6,"combined table strictly sorted, finite and positive");previous=speed;}
            Expect(Convert.ToDouble(table[table.Length-1])==Convert.ToDouble(table[table.Length-3]),"combined tail cannot extrapolate additional gain");
            Expect(damping.Scale(0)==.25 && damping.Scale(recover)==1,"micro damping fully recovers without a hard deadzone");
        }
        foreach(double gain in new double[]{1.1,1.4,1.8}) foreach(double recover in new double[]{.1,1,20}) {
            AimDamping damping=new AimDamping {Enabled=true,LowScale=.25,RecoverySpeed=recover};object[] sampled=AimLookup.Table(null,gain,damping);
            for(int i=0;i<=1024;i++) {double velocity=.00001*Math.Pow(1e10,i/1024.0);Expect(Math.Abs(AimLookup.Interpolate(sampled,velocity)-AimLookup.Natural(velocity,gain)*damping.Scale(velocity))<.001,"natural micro sampling error stays below 0.001x on a logarithmic sweep");}
        }
        foreach(string feature in new string[]{"smooth","stability","tracking","snap","precision"}) {
            AimPreset next=Aim.Resolve(status,preset.ToMap(),feature,feature=="tracking");
            Expect(next.Damping.Enabled && Aim.SameValue(weights.ToMap(),next.Directions.ToMap()),"component toggles retain independent filters");AimConfigGuard.Check(Aim.ConfigurePreset(cfg,defaults,"HID\\FILTER",next));
        }
        AimPreset snapOff=Aim.Resolve(status,preset.ToMap(),"snap",false);AimStatus snapOffStatus=Aim.Describe(Aim.ConfigurePreset(cfg,defaults,"HID\\FILTER",snapOff),"HID\\FILTER");
        Expect(Aim.Resolve(snapOffStatus,snapOff.ToMap(),"snap",true).SnapDegrees==2,"snap on restores the chosen angle after off");
        Expect(Aim.Resolve(status,preset.ToMap(),"snap",true).SnapDegrees==2,"snap on without argument retains active angle");
        AimPreset precisionOff=Aim.Resolve(status,preset.ToMap(),"precision",false);AimStatus off=Aim.Describe(Aim.ConfigurePreset(cfg,defaults,"HID\\FILTER",precisionOff),"HID\\FILTER");
        Expect(off.Mode=="noaccel" && Aim.Resolve(off,precisionOff.ToMap(),"precision",true).Damping.Enabled,"precision off/on retains micro settings");
        bool blocked=false;try {Aim.Resolve(off,precisionOff.ToMap(),"damp",true);}catch(InvalidOperationException) {blocked=true;}Expect(blocked,"damping cannot claim an effect with precision off");
        off.CurveSpeedUnit="in/s";blocked=false;try {Aim.Resolve(off,precisionOff.ToMap(),"precision",true);}catch(InvalidOperationException) {blocked=true;}Expect(blocked,"remembered curves reject changed units even after precision off");
        AimPreset independent=Aim.Resolve(off,precisionOff.ToMap(),"damp",false);Expect(independent.SpeedUnit=="counts/ms","disabling dormant micro does not reinterpret remembered curve units");
        AimPreset independentDirections=Aim.Resolve(off,precisionOff.ToMap(),"directions",false);Expect(independentDirections.SpeedUnit=="counts/ms" && independentDirections.Directions.Neutral,"independent directions off does not require dormant curve units to match");
        AimPreset rebuilt=Aim.Resolve(off,preset.ToMap(),"curve",true,null,null,8,new AimCurve());Expect(!rebuilt.Damping.Enabled && rebuilt.SpeedUnit=="in/s","explicit new-unit curve rebuild resets incompatible micro thresholds off");
        blocked=false;try {Aim.Resolve(off,preset.ToMap(),"resume",false);}catch(InvalidOperationException) {blocked=true;}Expect(blocked,"resume cannot reinterpret stored speeds after normalization change");
        Dictionary<string,object> peer=Aim.Configure(cfg,defaults,"HID\\PEER",true,true);
        Dictionary<string,object> bypass=Aim.ConfigureBypass(peer,"HID\\FILTER",true);
        Expect(Aim.SameValue(peer["profiles"],bypass["profiles"]) && Aim.SameValue(peer["defaultDeviceConfig"],bypass["defaultDeviceConfig"]) && Aim.SameValue(Aim.Items(peer["devices"])[1],Aim.Items(bypass["devices"])[1]),"bypass changes only selected device disable flag");
        Expect(Aim.SameValue(peer,Aim.ConfigureBypass(bypass,"HID\\FILTER",false)),"bypass roundtrip preserves the full original configuration");
        AimConfigGuard.Check(Aim.ConfigureBypass(defaults,"HID\\NEW",true));
        Dictionary<string,object> alias=Aim.Configure(defaults,defaults,"hid\\filter",true,true);
        Aim.Map(Aim.Map(Aim.Items(alias["devices"])[0])["config"])["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        Dictionary<string,object> canonical=Aim.ConfigureBypass(alias,"HID\\FILTER",true);AimConfigGuard.Check(canonical);
        Expect(Aim.Items(canonical["devices"]).Count==1 && Aim.Describe(canonical,"HID\\FILTER").CurveSpeedUnit=="counts/ms" && Aim.Describe(canonical,"HID\\FILTER").Enabled==false,"bypass replaces ignored case alias using the effective default calibration");
        foreach(string field in new string[]{"Whole/combined accel (set false for 'by component' mode)","lpNorm","rotation","domain","range","cap"}) {
            Dictionary<string,object> external=Aim.Parse(Store.Json.Serialize(cfg)),profile=Aim.Map(Aim.Items(external["profiles"])[1]);
            if(field=="rotation") profile["Degrees of rotation"]=1;
            else if(field=="domain") Aim.Map(profile["Stretches domain for horizontal vs vertical inputs"])["x"]=2;
            else if(field=="range") Aim.Map(profile["Stretches accel range for horizontal vs vertical inputs"])["y"]=2;
            else if(field=="cap") profile["Input Speed Cap"]=5;
            else Aim.Map(profile["Input speed calculation parameters"])[field]=field=="lpNorm" ? (object)3 : false;
            blocked=false;try {Aim.Resolve(Aim.Describe(external,"HID\\FILTER"),preset.ToMap(),"smooth",false);}catch(InvalidOperationException) {blocked=true;}Expect(blocked,"external LUT processing changes cannot be silently replaced");
            Expect(Aim.Describe(Aim.ConfigureBypass(external,"HID\\FILTER",true),"HID\\FILTER").Enabled==false,"bypass remains available with unrecognized LUT processing");
        }
        foreach(object bad in new object[]{null,true,new Dictionary<string,object>{{"left",1},{"right",1},{"up",1},{"down",0}},new Dictionary<string,object>{{"left","1"},{"right",1},{"up",1},{"down",1}}}) {
            Dictionary<string,object> invalid=preset.ToMap();invalid["directions"]=bad;blocked=false;try {Aim.ReadPreset(invalid);}catch(ArgumentException) {blocked=true;}Expect(blocked,"invalid saved directions rejected");
        }
        foreach(object bad in new object[]{null,"true",new Dictionary<string,object>{{"enabled",true},{"lowScale",double.NaN},{"recoverySpeed",1}},new Dictionary<string,object>{{"enabled",true},{"lowScale",.75},{"recoverySpeed",0}}}) {
            Dictionary<string,object> invalid=preset.ToMap();invalid["damping"]=bad;blocked=false;try {Aim.ReadPreset(invalid);}catch(ArgumentException) {blocked=true;}Expect(blocked,"invalid saved damping rejected");
        }
        AimPreset legacy=Aim.ReadPreset(new Dictionary<string,object>{{"precision",true},{"smooth",false}});Expect(legacy.Directions.Neutral && !legacy.Damping.Enabled && legacy.SnapStrength==1,"legacy presets keep new filters off");
    }
    private static void GameRegression() {
        Dictionary<string,object> defaults=Aim.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","fixtures","rawaccel-default.json")));
        Settings original=new Settings {Speed=15,Acceleration=2,Threshold1=7,Threshold2=13,WheelLines=4,DoubleClickMs=450,SwapButtons=1};
        Settings windows=GamePresets.Windows(original);
        Expect(windows.Speed==10 && windows.Acceleration==0 && windows.WheelLines==4 && windows.DoubleClickMs==450 && windows.SwapButtons==1 && original.Speed==15,"game policy changes only desktop speed and acceleration");
        Dictionary<string,object> decorated=Aim.Configure(defaults,defaults,"HID\\GAME",true,true,8,1.6,true,10,true,new AimCurve(),5,new AimDirections {Left=.25,Up=.25},new AimDamping {Enabled=true});
        Dictionary<string,object> oldConfig=Aim.Map(Aim.DeviceEntry(decorated,"HID\\GAME")["config"]);oldConfig["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;oldConfig["Polling rate Hz (keep at 0 for automatic adjustment)"]=250;oldConfig["Use constant time interval based on polling rate"]=true;
        Dictionary<string,object> peer=Aim.Configure(decorated,defaults,"HID\\PEER",true,true,4);
        foreach(GameRecipe recipe in GamePresets.List()) {
            Dictionary<string,object> cfg=GamePresets.Configure(peer,defaults,"HID\\GAME",recipe);AimConfigGuard.Check(cfg);AimStatus state=Aim.Describe(cfg,"HID\\GAME");
            Expect(state.Mode=="lut" && state.Enabled==true && state.SnapDegrees==0 && state.OutputHalfLifeMs==0 && state.Directions.Neutral && state.CurveSpeedUnit=="counts/ms","complete game recipe clears old snap, output smoothing and directional bias");
            Dictionary<string,object> dev=Aim.Map(Aim.DeviceEntry(cfg,"HID\\GAME")["config"]);
            Expect((int)dev["Polling rate Hz (keep at 0 for automatic adjustment)"]==0 && !(bool)dev["Use constant time interval based on polling rate"],"game recipe resets software timing overrides without hardware writes");
            Expect(Aim.SameValue(cfg["defaultDeviceConfig"],peer["defaultDeviceConfig"]) && Aim.SameValue(Aim.DeviceEntry(cfg,"HID\\PEER"),Aim.DeviceEntry(peer,"HID\\PEER")),"game recipe preserves defaults and peer mice");
            Aim.DescribeDamping(state,recipe.Controls());Expect(state.DampingEnabled==recipe.MicroDamping,"game recipe damping readback matches composed native table");
            Expect(Aim.SameValue(recipe.Controls().ToMap(),Aim.ReadPreset(Aim.Parse(Store.Json.Serialize(recipe.Controls().ToMap()))).ToMap()),"complete game controls roundtrip for resume");
            Dictionary<string,object> renamed=Aim.Parse(Store.Json.Serialize(cfg));
            Aim.Map(Aim.Items(renamed["profiles"])[1])["name"]="renamed curve";Aim.DeviceEntry(renamed,"HID\\GAME")["profile"]="renamed curve";
            Aim.Map(Aim.Items(renamed["profiles"])[0])["Input Speed Cap"]=10.0;
            Expect(GamePresets.SameSelected(cfg,renamed,"HID\\GAME"),"equivalent renamed profile and unrelated default edits keep the selected recipe match");
            Aim.Map(Aim.DeviceEntry(renamed,"HID\\GAME")["config"])["disable"]=true;
            Expect(!GamePresets.SameSelected(cfg,renamed,"HID\\GAME"),"bypass breaks selected recipe matching");
        }
        Expect(Aim.SameValue(GamePresets.Get("valorant").Controls().ToMap(),GamePresets.Get("kovaaks-valorant").Controls().ToMap()) && Aim.SameValue(GamePresets.Get("cs2").Controls().ToMap(),GamePresets.Get("kovaaks-cs2").Controls().ToMap()),"matched training recipes keep exactly the target game's processing");
        AimPreset trackingRecipe=GamePresets.Get("kovaaks-tracking").Controls();
        Dictionary<string,object> modern=GamePresets.Configure(defaults,defaults,"HID\\GAME",GamePresets.Get("kovaaks-tracking"));
        Dictionary<string,object> outputOnly=Aim.Parse(Store.Json.Serialize(modern));
        Dictionary<string,object> outputProfile=Aim.Map(Aim.Items(outputOnly["profiles"])[1]);
        Aim.Map(outputProfile["Input speed calculation parameters"])["Time in ms after which an output is weighted at half its original value."]=8.0;
        Aim.RequireSmoothingOnly(modern,outputOnly,"HID\\GAME");
        outputProfile["Input Speed Cap"]=10.0;
        bool smoothingBlocked=false;try {Aim.RequireSmoothingOnly(modern,outputOnly,"HID\\GAME");}catch(InvalidOperationException) {smoothingBlocked=true;}
        Expect(smoothingBlocked,"smoothing cannot silently replace an imported input cap");
        AimPreset retained=Aim.Resolve(Aim.Describe(modern,"HID\\GAME"),trackingRecipe.ToMap(),"smooth",false);
        Expect(retained.Stability && retained.StabilityMs==8,"LUT scale-only stability survives unrelated switches");
        Dictionary<string,object> legacy=Aim.Parse(Store.Json.Serialize(modern));
        Dictionary<string,object> speed=Aim.Map(Aim.Map(Aim.Items(legacy["profiles"])[1])["Input speed calculation parameters"]);
        speed["Time in ms after which an input is weighted at half its original value."]=10.0;speed["Time in ms after which scale is weighted at half its original value."]=5.0;
        AimPreset migrated=Aim.Resolve(Aim.Describe(legacy,"HID\\GAME"),trackingRecipe.ToMap(),"smooth",false);
        AimStatus migration=Aim.Describe(Aim.ConfigurePreset(legacy,defaults,"HID\\GAME",migrated,true),"HID\\GAME");
        Expect(migrated.StabilityMs==10 && migration.InputHalfLifeMs==0 && migration.ScaleHalfLifeMs==5 && migration.LookupInputSmoothingRisk==false,"legacy LUT stability migrates to safe scale-only settings on explicit edits");
        bool rejected=false;try {GamePresets.Get("unknown");}catch(ArgumentException) {rejected=true;}Expect(rejected,"unknown game cannot select fallback settings");
        Dictionary<string,object> before=Aim.Parse("{\"v\":1}"),after=Aim.Parse("{\"v\":2}");
        foreach(string failAt in new string[]{"windows","driver","files","rollback"}) {
            Settings liveWindows=original;Dictionary<string,object> liveAim=before;int windowsCalls=0,driverCalls=0;bool restored=false;string error=null;
            try {GamePresets.CommitPair(original,before,windows,after,delegate(Settings value) {
                windowsCalls++;if(failAt=="windows" && windowsCalls==1) throw new IOException("windows refused");liveWindows=value;
            },delegate(Dictionary<string,object> value) {
                driverCalls++;liveAim=value;if(driverCalls==1 && (failAt=="driver" || failAt=="rollback")) throw new IOException("driver refused");if(driverCalls==2 && failAt=="rollback") throw new IOException("driver recovery refused");return value;
            },delegate(Dictionary<string,object> value) {if(failAt=="files") throw new IOException("disk full");},delegate {restored=true;});}catch(IOException e) {error=e.Message;}
            Expect(error!=null && original.Same(liveWindows) && Aim.SameValue(before,liveAim) && restored,"combined failure restores both components and files independently");
            if(failAt=="windows") Expect(driverCalls==0,"windows failure before activation must not reset the driver");
            if(failAt=="rollback") Expect(error.Contains("driver refused") && error.Contains("driver recovery refused") && windowsCalls==2,"failed driver rollback retains both errors without skipping windows recovery");
        }
        int writes=0,saves=0;GamePresets.CommitPair(original,before,original,before,delegate(Settings s){writes++;},delegate(Dictionary<string,object> c){writes++;return c;},delegate(Dictionary<string,object> c){saves++;},delegate {});
        Expect(writes==0 && saves==1,"identical complete preset avoids all native writes");
        writes=0;bool filesRestored=false;
        try {GamePresets.CommitPair(original,before,original,before,delegate(Settings s){writes++;},delegate(Dictionary<string,object> c){writes++;return c;},delegate(Dictionary<string,object> c){throw new IOException("disk full");},delegate {filesRestored=true;});}catch(IOException) {}
        Expect(writes==0 && filesRestored,"failed no-op persistence must not reset either native component");
        RecoveryRegression(original,defaults);
        string directory=Path.Combine(Path.GetTempPath(),"helox-game-files-"+Guid.NewGuid().ToString("n"));Directory.CreateDirectory(directory);string present=Path.Combine(directory,"present.json"),missing=Path.Combine(directory,"missing.json");
        try {
            byte[] bytes={0xef,0xbb,0xbf,0x7b,0x7d};File.WriteAllBytes(present,bytes);SavedFiles files=new SavedFiles(new string[]{present,missing});
            File.WriteAllText(present,"changed");File.WriteAllText(missing,"new");files.Restore();Expect(Convert.ToBase64String(File.ReadAllBytes(present))==Convert.ToBase64String(bytes) && !File.Exists(missing),"file recovery retains exact bytes and previous absence");
            File.SetAttributes(present,FileAttributes.ReadOnly);files.Restore();Expect(File.Exists(present),"unchanged readonly recovery file is not rewritten");File.SetAttributes(present,FileAttributes.Normal);
        }finally {if(File.Exists(present)) {File.SetAttributes(present,FileAttributes.Normal);File.Delete(present);}if(File.Exists(missing)) File.Delete(missing);Directory.Delete(directory);}
        Dictionary<string,object> tooShort=Aim.Parse(Store.Json.Serialize(defaults));Aim.Map(tooShort["defaultDeviceConfig"])["minimumTime"]=5e-306;Aim.Map(tooShort["defaultDeviceConfig"])["maximumTime"]=5e-306;
        Expect(!double.IsInfinity(800/5e-306) && double.IsInfinity(1001/5e-306),"direction overflow reproducer escapes the old 800-count guard");
        rejected=false;try {AimResponseTest.Run(tooShort,"HID\\GAME",8);}catch(InvalidOperationException e) {rejected=e.Message=="response interval too small for finite speed examples";}Expect(rejected,"direction example rejects overflow before native calculation");
        Expect(LiveVerify.Cases(defaults,defaults,"HID\\GAME").Count==13,"live verification includes every new filter and built-in game recipe");
    }
    private static void RecoveryRegression(Settings windows,Dictionary<string,object> aim) {
        Dictionary<string,object> files=new Dictionary<string,object>{{"aim-presets.json",Convert.ToBase64String(new byte[]{0xef,0xbb,0xbf,0x7b,0x7d})},{"game-undo.json",null},{"undo.json",null}};
        Dictionary<string,object> snapshot=GameRecovery.Snapshot(windows,aim,files);
        foreach(string failure in new string[]{"driver","windows","files","checkpoint"}) {
            int driverCalls=0,windowsCalls=0,fileCalls=0,completionCalls=0;bool rejected=false;
            try {GameRecovery.Recover(snapshot,delegate(Dictionary<string,object> c){driverCalls++;if(failure=="driver") throw new IOException("offline");},delegate(Settings s){windowsCalls++;if(failure=="windows") throw new IOException("denied");},delegate(Dictionary<string,object> f){fileCalls++;if(failure=="files") throw new IOException("locked");},delegate {completionCalls++;if(failure=="checkpoint") throw new IOException("locked checkpoint");});}catch(IOException e) {rejected=e.Message.Contains("snapshot retained");}
            Expect(rejected && driverCalls==1 && windowsCalls==1 && fileCalls==1 && completionCalls==(failure=="checkpoint" ? 1 : 0),"recovery attempts independent components and retains the snapshot after any failure");
        }
        int completed=0;GameRecovery.Recover(snapshot,delegate(Dictionary<string,object> c){Expect(Aim.SameValue(aim,c),"recovery uses the complete native snapshot");},delegate(Settings s){Expect(windows.Same(s),"recovery uses all windows values");},delegate(Dictionary<string,object> f){Expect(f["game-undo.json"]==null,"recovery retains prior file absence");},delegate {completed++;});
        Expect(completed==1,"checkpoint cleared only after successful complete recovery");
        Dictionary<string,object> invalidFiles=new Dictionary<string,object>(files);invalidFiles["undo.json"]=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"Speed\":\"10\"}"));
        bool invalid=false;try {GameRecovery.Snapshot(windows,aim,invalidFiles);}catch(ArgumentException) {invalid=true;}Expect(invalid,"corrupt prior windows undo blocks a preset before any mutation");
        invalidFiles=new Dictionary<string,object>(files);invalidFiles.Remove("undo.json");invalidFiles.Add("../outside.json",null);
        invalid=false;try {GameRecovery.Snapshot(windows,aim,invalidFiles);}catch(ArgumentException) {invalid=true;}Expect(invalid,"recovery accepts only fixed tool files");
        Dictionary<string,object> undo=new Dictionary<string,object>{{"game","valorant"},{"beforeWindows",Aim.Parse(Store.Json.Serialize(windows))},{"afterWindows",Aim.Parse(Store.Json.Serialize(windows))},{"beforeAim",aim},{"afterAim",aim},{"afterPresets",new Dictionary<string,object>{{"HID\\GAME",new Dictionary<string,object>{{"precision","yes"},{"smooth",false}}}}},{"previousPresets",null},{"previousWindowsUndo",null}};
        invalid=false;try {GamePresets.ValidateUndo(undo);}catch(ArgumentException) {invalid=true;}Expect(invalid,"malformed saved-controls checkpoint is rejected in preflight");
    }
    private static void ConfigGuardRegression() {
        string path=Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..")),"tests","fixtures","rawaccel-default.json");
        string template=File.ReadAllText(path);AimConfigGuard.Check(Aim.Parse(template));
        List<Action<Dictionary<string,object>>> bad=new List<Action<Dictionary<string,object>>> {
            delegate(Dictionary<string,object> cfg) {cfg["Profiles"]=cfg["profiles"];},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["output dpi"]=2000;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Map(Aim.Items(cfg["profiles"])[0])["Whole or horizontal accel parameters"])["DATA"]=new object[]{1e100,1};},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Map(Aim.Items(cfg["profiles"])[0])["Stretches domain for horizontal vs vertical inputs"])["X"]=2;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["MINIMUMTIME"]=0;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["MAXIMUMTIME"]="NaN";},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["SETEXTRAINFO"]="true";},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"]).Remove("Use constant time interval based on polling rate");Aim.Map(cfg["defaultDeviceConfig"])["use constant time interval based on polling rate"]="true";},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["Output DPI"]="NaN";},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["Output DPI"]=Double.NaN;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["Degrees of rotation"]=Double.PositiveInfinity;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["name"]=new string('x',256);},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Items(cfg["profiles"])[0])["name"]="default\0hidden";},
            delegate(Dictionary<string,object> cfg) {object profile=Aim.Items(cfg["profiles"])[0];cfg["profiles"]=new object[]{profile,profile};},
            delegate(Dictionary<string,object> cfg) {cfg["profiles"]=new object[0];},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["disable"]="false";},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["minimumTime"]=0.0;},
            delegate(Dictionary<string,object> cfg) {Aim.Map(cfg["defaultDeviceConfig"])["Polling rate Hz (keep at 0 for automatic adjustment)"]=125.5;},
            delegate(Dictionary<string,object> cfg) {Dictionary<string,object> dev=new Dictionary<string,object>{{"id",new string('x',200)},{"name","mouse"},{"profile","default"},{"config",cfg["defaultDeviceConfig"]}};cfg["devices"]=new object[]{dev};},
            delegate(Dictionary<string,object> cfg) {Dictionary<string,object> dev=new Dictionary<string,object>{{"id","HID\\MOUSE"},{"name","mouse"},{"profile","missing"},{"config",cfg["defaultDeviceConfig"]}};cfg["devices"]=new object[]{dev};},
            delegate(Dictionary<string,object> cfg) {Dictionary<string,object> dev=new Dictionary<string,object>{{"id","HID\\MOUSE"},{"name","mouse"},{"profile","default"},{"config",cfg["defaultDeviceConfig"]}};cfg["devices"]=new object[]{dev,dev};},
            delegate(Dictionary<string,object> cfg) {Aim.Map(Aim.Map(Aim.Items(cfg["profiles"])[0])["Whole or horizontal accel parameters"])["data"]=new object[]{1e100,1.0};},
            delegate(Dictionary<string,object> cfg) {Dictionary<string,object> args=Aim.Map(Aim.Map(Aim.Items(cfg["profiles"])[0])["Whole or horizontal accel parameters"]);args["mode"]="lut";args["data"]=new object[]{0,1,0,1};},
            delegate(Dictionary<string,object> cfg) {Dictionary<string,object> args=Aim.Map(Aim.Map(Aim.Items(cfg["profiles"])[0])["Whole or horizontal accel parameters"]);args["mode"]="lut";args["data"]=new object[]{1.0,1,1.00000001,2};}
        };
        foreach(Action<Dictionary<string,object>> corrupt in bad) {
            Dictionary<string,object> cfg=Aim.Parse(template);corrupt(cfg);bool rejected=false;
            // Validate must reject before it attempts to load any native backend, also on hosted ci.
            try {Aim.Validate(cfg);}catch(ArgumentException) {rejected=true;}
            Expect(rejected,"unsafe driver configuration rejected before native conversion");
        }
        Dictionary<string,object> validLut=Aim.Parse(template);Dictionary<string,object> validArgs=Aim.Map(Aim.Map(Aim.Items(validLut["profiles"])[0])["Whole or horizontal accel parameters"]);
        validArgs["mode"]="lut";validArgs["data"]=new object[]{0,0,1,1,2,2};AimConfigGuard.Check(validLut);
        Dictionary<string,object> optional=Aim.Parse(template),deviceConfig=Aim.Map(optional["defaultDeviceConfig"]);
        deviceConfig["minimumTime"]=.0625;deviceConfig["maximumTime"]=100;deviceConfig["setExtraInfo"]=true;
        AimConfigGuard.Check(optional);
    }
    private static void AimRegression() {
        Dictionary<string,object> defaults=Aim.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","fixtures","rawaccel-default.json")));
        Dictionary<string,object> selected=Aim.Configure(defaults,defaults,"HID\\FIRST",true,true,8);
        Dictionary<string,object> peers=Aim.Configure(selected,defaults,"HID\\SECOND",false,true,2);
        Dictionary<string,object> repeated=Aim.Configure(peers,defaults,"HID\\FIRST",true,true,8);
        Expect(Aim.SameValue(peers,repeated),"unchanged aim update preserves device ordering and smoothing strength");
        Dictionary<string,object> changed=Aim.Configure(peers,defaults,"HID\\FIRST",false,true,8);
        Expect(Aim.SameValue(Aim.Items(peers["devices"])[1],Aim.Items(changed["devices"])[1]) && Aim.Describe(changed,"HID\\FIRST").OutputHalfLifeMs==8,"aim toggle preserves peer entries and selected strength");
        AimConfigGuard.Check(changed);
        Dictionary<string,object> calibrated=Aim.Parse(Store.Json.Serialize(selected));
        Dictionary<string,object> calibration=Aim.Map(Aim.Map(Aim.Items(calibrated["devices"])[0])["config"]);
        calibration["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        calibration["Polling rate Hz (keep at 0 for automatic adjustment)"]=125;
        calibration["Use constant time interval based on polling rate"]=true;
        calibration["minimumTime"]=.5;calibration["maximumTime"]=40.0;calibration["setExtraInfo"]=true;
        Dictionary<string,object> calibratedOff=Aim.Configure(calibrated,defaults,"HID\\FIRST",true,false,8);
        Expect(Aim.SameValue(calibration,Aim.Map(Aim.Items(calibratedOff["devices"])[0])["config"]),"smooth off preserves active device normalization and timing");
        calibration["disable"]=true;
        string calibratedBefore=Store.Json.Serialize(calibrated);
        Dictionary<string,object> bypassedOff=Aim.Configure(calibrated,defaults,"HID\\FIRST",false,true,8);
        Expect(Aim.SameValue(calibration,Aim.Map(Aim.Items(bypassedOff["devices"])[0])["config"]) && Aim.Describe(bypassedOff,"HID\\FIRST").InputTransformed==false,"turning an effect off cannot activate a bypassed device");
        Dictionary<string,object> inherited=Aim.Parse(Store.Json.Serialize(defaults));inherited["defaultDeviceConfig"]=calibration;
        Dictionary<string,object> inheritedOff=Aim.Configure(inherited,defaults,"HID\\NEW",false,false);
        Expect(Aim.SameValue(calibration,Aim.Map(Aim.Items(inheritedOff["devices"])[0])["config"]),"first override preserves effective default device settings");
        foreach(string feature in new string[]{"precision","smooth","stability"}) {
            AimPreset offPreset=Aim.Resolve(Aim.Describe(calibrated,"HID\\FIRST"),null,feature,false);
            Dictionary<string,object> offChange=Aim.Configure(calibrated,defaults,"HID\\FIRST",offPreset.Precision,offPreset.Smooth,offPreset.SmoothMs,offPreset.GainLimit,offPreset.Stability,offPreset.StabilityMs,false);
            Expect(Aim.Describe(offChange,"HID\\FIRST").Enabled==false,"each off switch preserves device bypass");
        }
        Dictionary<string,object> enabled=Aim.Configure(calibrated,defaults,"HID\\FIRST",true,false,8,1.4,false,8,true);
        Dictionary<string,object> enabledConfig=Aim.Map(Aim.Map(Aim.Items(enabled["devices"])[0])["config"]);
        Expect(Aim.Describe(enabled,"HID\\FIRST").Enabled==true,"explicit on or resume enables a bypassed device");
        enabledConfig["disable"]=true;
        Expect(Aim.SameValue(calibration,enabledConfig),"explicit enable preserves all other device options");
        Expect(Aim.SameValue(calibrated,Aim.Parse(calibratedBefore)),"effect changes do not mutate the previous configuration");
        AimConfigGuard.Check(calibratedOff);AimConfigGuard.Check(bypassedOff);AimConfigGuard.Check(inheritedOff);
        Dictionary<string,object> caseAlias=Aim.Configure(defaults,defaults,"hid\\first",true,true,8);
        Expect(Aim.Describe(caseAlias,"HID\\FIRST").Mode=="noaccel" && Aim.Describe(caseAlias,"HID\\FIRST").OutputHalfLifeMs==0,"readback uses the kernel's exact device id matching");
        Dictionary<string,object> tuned=Aim.Configure(peers,defaults,"HID\\FIRST",true,true,8,1.6,true,10);
        AimStatus live=Aim.Describe(tuned,"HID\\FIRST");
        Expect(live.GainLimit==1.6 && live.InputHalfLifeMs==10 && live.ScaleHalfLifeMs==5 && live.StabilityEnabled==true && live.OutputHalfLifeMs==8,"independent acceleration and output filters appear in readback");
        Expect(Aim.SameValue(peers["defaultDeviceConfig"],tuned["defaultDeviceConfig"]) && Aim.SameValue(Aim.Items(peers["devices"])[1],Aim.Items(tuned["devices"])[1]),"stability preserves default dpi/polling and peers");
        AimPreset options=Aim.Resolve(live,null,"smooth",false);
        Expect(options.GainLimit==1.6 && options.Stability && options.StabilityMs==10 && !options.Smooth && options.SmoothMs==8,"live strength and stability survive smooth off");
        AimPreset disabled=Aim.Resolve(live,options.ToMap(),"precision",false);
        Dictionary<string,object> off=Aim.Configure(tuned,defaults,"HID\\FIRST",disabled.Precision,disabled.Smooth,disabled.SmoothMs,disabled.GainLimit,disabled.Stability,disabled.StabilityMs);
        AimStatus offStatus=Aim.Describe(off,"HID\\FIRST");
        Expect(offStatus.InputHalfLifeMs==0 && offStatus.ScaleHalfLifeMs==0 && offStatus.StabilityEnabled==false,"precision off bypasses acceleration filters even with remembered stability");
        options=Aim.Resolve(offStatus,disabled.ToMap(),"precision",true);
        Expect(options.Precision && options.Stability && options.StabilityMs==10 && options.GainLimit==1.6,"precision off/on remembers tuned acceleration");
        options=Aim.Resolve(live,disabled.ToMap(),"resume",false);
        Expect(!options.Precision && options.Stability && options.StabilityMs==10 && options.GainLimit==1.6,"resume uses saved choices instead of unrelated live state");
        options=Aim.Resolve(live,null,"stability",false);
        Expect(!options.Stability && options.Precision && options.Smooth && options.GainLimit==1.6 && options.SmoothMs==8,"stability off preserves precision and output smoothing");
        Dictionary<string,object> steady=Aim.Configure(tuned,defaults,"HID\\FIRST",options.Precision,options.Smooth,options.SmoothMs,options.GainLimit,options.Stability,options.StabilityMs);
        AimStatus steadyStatus=Aim.Describe(steady,"HID\\FIRST");
        Expect(steadyStatus.InputHalfLifeMs==4 && steadyStatus.ScaleHalfLifeMs==2 && steadyStatus.OutputHalfLifeMs==8,"stability off restores baseline acceleration filters only");
        options=Aim.Resolve(steadyStatus,null,"stability",true,null,null,12);
        Expect(options.Stability && options.StabilityMs==12 && options.GainLimit==1.6,"stability on keeps live gain and accepts bounded timing");
        options=Aim.Resolve(live,null,"tracking",true,null,null,8);
        Expect(options.Precision && options.Stability && !options.Smooth && options.GainLimit==1.6 && options.SmoothMs==8 && options.StabilityMs==8,"tracking preserves gain and remembered output strength while bypassing output averaging");
        Dictionary<string,object> tracking=Aim.Configure(peers,defaults,"HID\\FIRST",options.Precision,options.Smooth,options.SmoothMs,options.GainLimit,options.Stability,options.StabilityMs);
        AimConfigGuard.Check(tracking);
        AimStatus trackingStatus=Aim.Describe(tracking,"HID\\FIRST");
        Expect(trackingStatus.StabilityEnabled==true && trackingStatus.OutputHalfLifeMs==0 && Aim.SameValue(Aim.Items(peers["devices"])[1],Aim.Items(tracking["devices"])[1]),"tracking config isolates the selected device");
        AimPreset trackingResume=Aim.Resolve(offStatus,options.ToMap(),"resume",false);
        Expect(trackingResume.Precision && trackingResume.Stability && !trackingResume.Smooth && trackingResume.GainLimit==1.6 && trackingResume.SmoothMs==8,"resume retains all tracking components");
        AimPreset smoothAgain=Aim.Resolve(trackingStatus,options.ToMap(),"smooth",true);
        Expect(smoothAgain.Smooth && smoothAgain.SmoothMs==8 && smoothAgain.Stability && smoothAgain.GainLimit==1.6,"smooth on restores remembered strength after tracking");
        Dictionary<string,object> timing=Aim.Parse(Store.Json.Serialize(defaults["defaultDeviceConfig"]));
        Expect(AimResponseTest.Time(timing,.01)==.0625 && AimResponseTest.Time(timing,500)==100,"response model mirrors default kernel time clamps");
        timing["Polling rate Hz (keep at 0 for automatic adjustment)"]=2;
        Expect(AimResponseTest.Time(timing,8)==100,"response uses upstream clamp order with polling-derived minimum above maximum");
        timing["Use constant time interval based on polling rate"]=true;
        Expect(AimResponseTest.Time(timing,8)==500,"response honors the driver's constant interval when configured");
        timing.Remove("Use constant time interval based on polling rate");
        Expect(AimResponseTest.Time(timing,8)==100,"missing optional constant-time flag uses automatic timing");
        Dictionary<string,object> tiny=Aim.Parse(Store.Json.Serialize(defaults));
        Aim.Map(tiny["defaultDeviceConfig"])["minimumTime"]=double.Epsilon;Aim.Map(tiny["defaultDeviceConfig"])["maximumTime"]=double.Epsilon;
        bool tinyRejected=false;
        try {AimResponseTest.Run(tiny,"HID\\FIRST",8);}catch(InvalidOperationException e) {tinyRejected=e.Message=="response interval too small for finite speed examples";}
        Expect(tinyRejected,"response rejects unrepresentable sample speeds before native conversion");
        AimCarry carry=new AimCarry();int sx=0,sy=0;
        for(int i=0;i<12;i++) {int[] packet=carry.Emit(.25,-.5);sx+=packet[0];sy+=packet[1];}
        Expect(sx==3 && sy==-6 && carry.X==0 && carry.Y==0,"response fractional carry retains small counts on both axes");
        foreach(double invalid in new double[]{double.NaN,double.PositiveInfinity,Int32.MaxValue+1.0}) {
            bool rejected=false;try {carry.Emit(invalid,0);}catch(InvalidOperationException) {rejected=true;}
            Expect(rejected,"response model rejects nonfinite or overflowing packets");
        }
        bool blocked=false;try {Aim.Resolve(offStatus,disabled.ToMap(),"stability",true);}catch(InvalidOperationException) {blocked=true;}
        Expect(blocked,"stability cannot claim an effect with precision off");
        blocked=false;try {Aim.Resolve(live,null,"resume",false);}catch(InvalidOperationException) {blocked=true;}
        Expect(blocked,"resume requires a saved preset");
        DateTime now=DateTime.UtcNow;
        RateResult history=new RateResult {Reports=1001,Intervals=1000,ActiveHz=100,MedianIntervalMs=10,P95IntervalMs=10,DevicePath="mouse",MeasuredUtc=now.ToString("o")};
        Expect(Aim.StabilityHalfLife(history,"MOUSE",now)==10,"stability uses matching valid median interval");
        history.MedianIntervalMs=8.0007;Expect(Aim.StabilityHalfLife(history,"mouse",now)==8,"tiny delivery variation does not retune the driver");
        history.P95IntervalMs=12;history.MedianIntervalMs=10.26;Expect(Aim.StabilityHalfLife(history,"mouse",now)==10.5,"stability tuning uses half-millisecond steps");
        history.MedianIntervalMs=1;Expect(Aim.StabilityHalfLife(history,"mouse",now)==8,"fast streams use the stability minimum");
        history.MedianIntervalMs=20;history.P95IntervalMs=20;Expect(Aim.StabilityHalfLife(history,"mouse",now)==12,"slow streams cannot exceed stability maximum");
        Expect(Aim.StabilityHalfLife(history,"other",now)==8 && Aim.StabilityHalfLife(null,"mouse",now)==8,"missing or unrelated history uses fallback");
        history.MeasuredUtc=now.AddHours(-25).ToString("o");Expect(Aim.StabilityHalfLife(history,"mouse",now)==8,"stale stability timing ignored");
        history.MeasuredUtc=now.AddMinutes(6).ToString("o");Expect(Aim.StabilityHalfLife(history,"mouse",now)==8,"future stability timing ignored");
        history.MeasuredUtc=now.ToString("o");history.MedianIntervalMs=double.NaN;Expect(Aim.StabilityHalfLife(history,"mouse",now)==8,"invalid stability timing ignored");
        Expect(Aim.EndpointMayExist(true,0),"loaded driver remains present without a service record");
        Expect(Aim.EndpointMayExist(false,5),"inaccessible driver is not treated as absent");
        Expect(!Aim.EndpointMayExist(false,2) && !Aim.EndpointMayExist(false,3),"only missing driver paths prove absence");
        string presetPath=Path.Combine(Path.GetTempPath(),"helox-preset-test-"+Guid.NewGuid().ToString("n")+".json");
        try {
            Expect(Aim.Saved(presetPath).Count==0,"missing aim presets are empty");
            File.WriteAllText(presetPath,"{\"mouse\":{\"precision\":true,\"smooth\":false}}");
            Dictionary<string,object> saved=Aim.Saved(presetPath);
            Expect((bool)Aim.Map(saved["mouse"])["precision"] && !(bool)Aim.Map(saved["mouse"])["smooth"],"saved aim toggles roundtrip");
            Expect(saved.ContainsKey("MOUSE"),"saved aim preferences retain case-insensitive lookup");
            Expect(Aim.SavedHalfLife(Aim.Map(saved["mouse"]))==4,"legacy smoothing presets keep 4 ms");
            AimPreset legacy=Aim.ReadPreset(Aim.Map(saved["mouse"]));
            Expect(legacy.GainLimit==1.4 && !legacy.Stability && legacy.StabilityMs==8,"legacy presets keep the original curve and filters");
            saved["mouse"]=new AimPreset {Precision=true,Smooth=true,SmoothMs=8,GainLimit=1.6,Stability=true,StabilityMs=10}.ToMap();Store.Save(presetPath,saved);
            AimPreset restored=Aim.ReadPreset(Aim.Map(Aim.Saved(presetPath)["MOUSE"]));
            Expect(restored.GainLimit==1.6 && restored.Stability && restored.StabilityMs==10,"extended aim presets roundtrip");
            foreach(double ms in new double[]{2,4,8}) {
                Aim.Map(saved["mouse"])["smoothMs"]=ms;Store.Save(presetPath,saved);
                Expect(Aim.SavedHalfLife(Aim.Map(Aim.Saved(presetPath)["MOUSE"]))==ms,"smoothing strength persists by device identity");
            }
            foreach(object invalid in new object[]{0,13,"4",true,double.NaN,double.PositiveInfinity}) {
                bool rejected=false;try {Aim.SavedHalfLife(new Dictionary<string,object>{{"smoothMs",invalid}});}catch(ArgumentException) {rejected=true;}
                Expect(rejected,"unsafe smoothing strength rejected");
            }
            foreach(string key in new string[]{"gainLimit","stabilityMs","stability"}) {
                foreach(object invalid in key=="stability" ? new object[]{1,"true",null} : new object[]{0,100,"8",true,null,double.NaN,double.PositiveInfinity}) {
                    Dictionary<string,object> invalidPreset=legacy.ToMap();invalidPreset[key]=invalid;Store.Save(presetPath,new Dictionary<string,object>{{"mouse",invalidPreset}});
                    bool rejected=false;try {Aim.Saved(presetPath);}catch(ArgumentException) {rejected=true;}
                    Expect(rejected,"invalid extended aim fields blocked before activation");
                }
            }
            saved["MOUSE"]=new Dictionary<string,object>{{"precision",false},{"smooth",true}};
            Expect(saved.Count==1 && (bool)Aim.Map(saved["mouse"])["smooth"],"aim updates do not create case aliases");
            foreach(string invalid in new string[]{"{\"mouse\":{\"precision\":true,\"smooth\":false},\"MOUSE\":{\"precision\":false,\"smooth\":true}}","{\"\":{\"precision\":true,\"smooth\":false}}"}) {
                File.WriteAllText(presetPath,invalid);bool rejected=false;
                try {Aim.Saved(presetPath);}catch(ArgumentException) {rejected=true;}
                Expect(rejected,"ambiguous or empty aim identities rejected");
            }
            foreach(string invalid in new string[]{"null","{\"mouse\":null}","{\"mouse\":{\"precision\":true}}","{\"mouse\":{\"precision\":\"true\",\"smooth\":false}}"}) {
                File.WriteAllText(presetPath,invalid);bool rejected=false;
                try {Aim.Saved(presetPath);}catch(ArgumentException) {rejected=true;}
                Expect(rejected,"incomplete or mistyped saved aim preset rejected");
            }
        }finally {if(File.Exists(presetPath)) File.Delete(presetPath);}
        Expect(Aim.SameValue(Aim.Parse("{\"a\":1,\"b\":[2,3]}"),Aim.Parse("{\"b\":[2.0,3],\"a\":1.0}")),"aim readback ignores key order and numeric representation");
        Expect(!Aim.SameValue(Aim.Parse("{\"a\":1}"),Aim.Parse("{\"a\":2}")),"aim readback detects changed values");
        Expect(!Aim.SameValue(Aim.Parse("{\"a\":[1,2]}"),Aim.Parse("{\"a\":[2,1]}")),"aim readback preserves array order");
        Dictionary<string,object> before=Aim.Parse("{\"value\":1}"),after=Aim.Parse("{\"value\":2}");
        int writes=0;bool caught=false;
        try {Aim.Transaction(before,after,delegate(Dictionary<string,object> cfg) {writes++;if(writes==1) throw new IOException("write rejected");Expect(Object.ReferenceEquals(cfg,before),"aim rollback targets original snapshot");return cfg;});}
        catch(IOException e) {caught=e.Message.Contains("previous driver settings restored") && e.Message.Contains("write rejected");}
        Expect(caught && writes==2,"aim apply failure reports verified rollback");
        writes=0;caught=false;
        try {Aim.Transaction(before,after,delegate(Dictionary<string,object> cfg) {writes++;throw new IOException(writes==1 ? "first error" : "second error");});}
        catch(IOException e) {caught=e.Message.Contains("first error") && e.Message.Contains("rollback failed") && e.Message.Contains("second error");}
        Expect(caught && writes==2,"aim failed rollback preserves both errors");
        writes=0;int saves=0;
        Dictionary<string,object> identical=Aim.Parse("{\"value\":1.0}");
        Dictionary<string,object> same=Aim.Commit(before,identical,delegate(Dictionary<string,object> cfg) {writes++;return cfg;},delegate {saves++;});
        Expect(writes==0 && saves==1 && Object.ReferenceEquals(same,before),"unchanged aim settings save preferences without driver activation");
        caught=false;
        try {Aim.Commit(before,identical,delegate(Dictionary<string,object> cfg) {writes++;return cfg;},delegate {throw new IOException("preset save failed");});}
        catch(IOException e) {caught=e.Message=="preset save failed";}
        Expect(caught && writes==0,"unchanged aim save failure does not write or roll back driver");
        bool choicesCleared=false;
        Aim.Commit(before,identical,delegate(Dictionary<string,object> cfg) {writes++;return cfg;},delegate {choicesCleared=true;});
        Expect(choicesCleared && writes==0,"already restored aim clears saved choices without resetting the driver filters");
        writes=0;saves=0;caught=false;
        try {Aim.Commit(before,after,delegate(Dictionary<string,object> cfg) {writes++;return cfg;},delegate {saves++;throw new IOException("disk full");});}
        catch(IOException e) {caught=e.Message.Contains("previous driver settings restored") && e.Message.Contains("disk full");}
        Expect(caught && writes==2 && saves==1,"aim persistence failure rolls back once without saving again");
    }
    private static void DeviceStackRegression() {
        Expect(Marshal.SizeOf(typeof(Native.DevPropertyKey))==20,"pnp property key matches the native abi");
        byte[] instance=Encoding.Unicode.GetBytes("ROOT\\MOUSE\\0001\0");
        Expect(DeviceStack.Decode(0x12,instance,instance.Length,false)[0]=="ROOT\\MOUSE\\0001","pnp ids retain their actual enumerator instead of assuming hid");
        byte[] stack=Encoding.Unicode.GetBytes("\\Driver\\mouclass\0\\Driver\\rawaccel\0\\Driver\\mouhid\0\0");
        string[] decoded=DeviceStack.Decode(0x2012,stack,stack.Length,true);
        Expect(decoded.Length==3 && DeviceStack.IncludesRawAccel(decoded)==true,"pnp driver-object names identify the raw accel stack entry");
        Expect(DeviceStack.IncludesRawAccel(new string[]{"RAWACCEL"})==true,"plain service names are accepted too");
        Expect(DeviceStack.IncludesRawAccel(new string[]{"rawaccel2","\\Other\\rawaccel"})==false,"similarly named services do not prove filter presence");
        Expect(DeviceStack.IncludesRawAccel(null)==null && DeviceStack.IncludesRawAccel(new string[0])==null,"missing or empty stack evidence remains unknown");
        foreach(Action invalid in new Action[]{
            delegate {DeviceStack.Decode(0x12,stack,stack.Length,true);},
            delegate {DeviceStack.Decode(0x2012,stack,stack.Length-2,true);},
            delegate {DeviceStack.Decode(0x2012,stack,3,true);},
            delegate {DeviceStack.Decode(0x2012,stack,stack.Length+2,true);},
            delegate {DeviceStack.Decode(0x12,Encoding.Unicode.GetBytes("a\0b\0"),8,false);},
            delegate {DeviceStack.Decode(0x12,new byte[]{0,0},2,false);},
            delegate {DeviceStack.Decode(0x2012,Encoding.Unicode.GetBytes("a\0\0b\0\0"),12,true);}
        }) {
            bool blocked=false;try {invalid();}catch(IOException) {blocked=true;}
            Expect(blocked,"malformed pnp properties cannot produce filter evidence");
        }
        int calls=0;
        string[] retry=DeviceStack.Property(delegate(out uint type,byte[] data,ref uint size) {
            calls++;type=0x2012;size=(uint)stack.Length;
            if(data==null || calls==2) return 26;
            Array.Copy(stack,data,stack.Length);return 0;
        },true);
        Expect(calls==4 && DeviceStack.IncludesRawAccel(retry)==true,"pnp buffer growth retries instead of reporting a missing driver");
        foreach(uint sizeValue in new uint[]{0,3,65538}) {
            bool blocked=false;
            try {DeviceStack.Property(delegate(out uint type,byte[] data,ref uint size) {type=0x2012;size=sizeValue;return 26;},true);}catch(IOException) {blocked=true;}
            Expect(blocked,"invalid pnp sizes are rejected before allocation");
        }
        bool denied=false;
        try {DeviceStack.Property(delegate(out uint type,byte[] data,ref uint size) {type=0;size=0;return 5;},true);}catch(IOException) {denied=true;}
        Expect(denied,"pnp access errors cannot be interpreted as filter absence");
        calls=0;denied=false;
        try {DeviceStack.Property(delegate(out uint type,byte[] data,ref uint size) {calls++;type=0x2012;size=(uint)stack.Length;return 26;},true);}catch(IOException) {denied=true;}
        Expect(denied && calls==6,"continuously changing pnp properties have a bounded retry");
    }
}
}
