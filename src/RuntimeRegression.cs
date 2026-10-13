using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Helox {
// Pure managed regressions: no driver writes, Windows changes or user-data files.
internal static class RuntimeRegression {
    private static void Expect(bool ok,string name) {if(!ok) throw new Exception("selftest failed: "+name);}
    internal static void Run() {
        Dictionary<string,object> aim=Aim.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","fixtures","rawaccel-default.json")));
        Settings windows=new Settings {Speed=10,Threshold1=6,Threshold2=10,Acceleration=0,WheelLines=3,DoubleClickMs=500,SwapButtons=0};
        byte[] broken=new byte[]{0xff,0xfe,0x00,0x80};
        Dictionary<string,object> files=new Dictionary<string,object>{{"aim-presets.json",Convert.ToBase64String(broken)},{"game-undo.json",Convert.ToBase64String(Encoding.UTF8.GetBytes("broken"))},{"undo.json",null}};
        Dictionary<string,object> snapshot=GameRecovery.Snapshot(windows,aim,files,true);
        Expect((int)snapshot["version"]==2,"control recovery uses the byte-preserving snapshot format");
        bool restored=false,completed=false;
        GameRecovery.Recover(snapshot,delegate(Dictionary<string,object> cfg) {Expect(Aim.SameValue(cfg,aim),"control recovery retains the complete driver state");},
            delegate(Settings value) {Expect(value.Same(windows),"control recovery retains Windows preferences");},
            delegate(Dictionary<string,object> saved) {restored=Convert.ToBase64String(GamePresets.Decode(saved["aim-presets.json"]))==Convert.ToBase64String(broken) && saved["undo.json"]==null;},delegate {completed=true;});
        Expect(restored && completed,"broken saved controls remain recoverable as exact bytes without importing them");
        foreach(Action invalid in new Action[]{
            delegate {GameRecovery.Snapshot(windows,aim,files);},
            delegate {Dictionary<string,object> wrong=new Dictionary<string,object>(files);wrong["aim-presets.json"]="not base64";GameRecovery.Snapshot(windows,aim,wrong,true);},
            delegate {Dictionary<string,object> wrong=new Dictionary<string,object>(files);wrong["aim-presets.json"]=new string('A',1400004);GameRecovery.Snapshot(windows,aim,wrong,true);},
            delegate {Dictionary<string,object> wrong=new Dictionary<string,object>(files);wrong.Remove("undo.json");wrong.Add("../escape.json",null);GameRecovery.Snapshot(windows,aim,wrong,true);},
            delegate {Dictionary<string,object> wrong=Aim.Parse(Store.Json.Serialize(snapshot));wrong["version"]=3;GameRecovery.Read(wrong);},
            delegate {Dictionary<string,object> wrong=Aim.Parse(Store.Json.Serialize(snapshot));Aim.Map(wrong["windows"])["Speed"]=0;GameRecovery.Read(wrong);},
            delegate {Dictionary<string,object> wrong=Aim.Parse(Store.Json.Serialize(snapshot));Aim.Map(Aim.Map(wrong["aim"])["defaultDeviceConfig"])["disable"]="yes";GameRecovery.Read(wrong);}
        }) {
            bool rejected=false;try {invalid();}catch(ArgumentException) {rejected=true;}
            Expect(rejected,"opaque recovery still rejects unsupported versions, paths, bounds and native settings");
        }
        List<Sample> samples=new List<Sample>();for(int i=0;i<=1000;i++) samples.Add(new Sample(i,1,0));
        RateResult rate=Analysis.Rate(samples);
        Expect(Analysis.ValidHistory(rate),"continuous delivery history remains valid");
        rate.IdleGaps=1;Expect(!Analysis.ValidHistory(rate),"impossible gap counts cannot become tuning history");
        samples.Add(new Sample(2000,1,0));rate=Analysis.Rate(samples);
        Expect(Analysis.ValidHistory(rate) && rate.IdleGaps==1,"real delivery gaps remain valid history");
        foreach(List<Sample> invalid in new List<Sample>[] {null,new List<Sample>{null},new List<Sample>{new Sample(-1,1,0)},new List<Sample>{new Sample(double.NaN,1,0)},new List<Sample>{new Sample(1,1,0),new Sample(0,1,0)}}) {
            bool rejected=false;try {Analysis.Rate(invalid);}catch(InvalidOperationException) {rejected=true;}
            Expect(rejected,"malformed delivery captures fail before generating a measurement");
        }
    }
}
}
