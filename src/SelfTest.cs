using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
internal static class SelfTest {
    private static void Expect(bool ok,string name) {if(!ok) throw new Exception("selftest failed: "+name);}
    internal static void Run(bool native=true) {
        ConfigGuardRegression();
        AimRegression();
        UndoRegression();
        SnapshotRegression();
        Program.RequireDpiInput(false);
        foreach(bool? transformed in new bool?[]{true,null}) {
            bool blocked=false;try {Program.RequireDpiInput(transformed);}catch(InvalidOperationException) {blocked=true;}
            Expect(blocked,"dpi refuses active and unknown filter states");
        }
        Device first=new Device {Path="first"},second=new Device {Path="second"};
        List<Device> displayed=new List<Device>{first,second};
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
        Expect(Aim.EndpointMayExist(true,0),"loaded driver remains present without a service record");
        Expect(Aim.EndpointMayExist(false,5),"inaccessible driver is not treated as absent");
        Expect(!Aim.EndpointMayExist(false,2) && !Aim.EndpointMayExist(false,3),"only missing driver paths prove absence");
        string presetPath=Path.Combine(Path.GetTempPath(),"helox-preset-test-"+Guid.NewGuid().ToString("n")+".json");
        try {
            Expect(Aim.Saved(presetPath).Count==0,"missing aim presets are empty");
            File.WriteAllText(presetPath,"{\"mouse\":{\"precision\":true,\"smooth\":false}}");
            Dictionary<string,object> saved=Aim.Saved(presetPath);
            Expect((bool)Aim.Map(saved["mouse"])["precision"] && !(bool)Aim.Map(saved["mouse"])["smooth"],"saved aim toggles roundtrip");
            Expect(saved.ContainsKey("MOUSE"),"saved aim identity ignores case like driver identity");
            Expect(Aim.SavedHalfLife(Aim.Map(saved["mouse"]))==4,"legacy smoothing presets keep 4 ms");
            foreach(double ms in new double[]{2,4,8}) {
                Aim.Map(saved["mouse"])["smoothMs"]=ms;Store.Save(presetPath,saved);
                Expect(Aim.SavedHalfLife(Aim.Map(Aim.Saved(presetPath)["MOUSE"]))==ms,"smoothing strength persists by device identity");
            }
            foreach(object invalid in new object[]{0,13,"4",true,double.NaN,double.PositiveInfinity}) {
                bool rejected=false;try {Aim.SavedHalfLife(new Dictionary<string,object>{{"smoothMs",invalid}});}catch(ArgumentException) {rejected=true;}
                Expect(rejected,"unsafe smoothing strength rejected");
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
        writes=0;saves=0;caught=false;
        try {Aim.Commit(before,after,delegate(Dictionary<string,object> cfg) {writes++;return cfg;},delegate {saves++;throw new IOException("disk full");});}
        catch(IOException e) {caught=e.Message.Contains("previous driver settings restored") && e.Message.Contains("disk full");}
        Expect(caught && writes==2 && saves==1,"aim persistence failure rolls back once without saving again");
    }
}
}
