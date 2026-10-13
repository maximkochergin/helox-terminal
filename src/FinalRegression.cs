using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
internal static class FinalRegression {
    private static void Expect(bool value,string name) {if(!value) throw new Exception("selftest failed: "+name);}
    private static Settings Windows(int speed) {return new Settings {Speed=speed,Threshold1=6,Threshold2=10,Acceleration=0,WheelLines=3,DoubleClickMs=500,SwapButtons=0};}
    internal static void Run() {
        Settings before=Windows(10),after=Windows(11);byte[] brokenUndo=new byte[]{0xff,0x80,0};
        Dictionary<string,object> snapshot=WindowsRecovery.Snapshot(before,brokenUndo);
        bool windows=false,undo=false,complete=false;
        WindowsRecovery.Recover(snapshot,delegate(Settings value) {windows=value.Same(before);},delegate(byte[] bytes) {undo=Convert.ToBase64String(bytes)==Convert.ToBase64String(brokenUndo);},delegate {complete=true;});
        Expect(windows && undo && complete,"desktop recovery restores Windows and exact prior undo bytes");
        foreach(string failure in new string[]{"windows","undo","checkpoint"}) {
            windows=false;undo=false;complete=false;bool rejected=false;
            try {WindowsRecovery.Recover(snapshot,delegate(Settings value) {windows=true;if(failure=="windows") throw new IOException("windows failure");},delegate(byte[] bytes) {undo=true;if(failure=="undo") throw new IOException("undo failure");},delegate {complete=true;if(failure=="checkpoint") throw new IOException("checkpoint failure");});}
            catch(IOException e) {rejected=e.Message.Contains("snapshot retained");}
            Expect(rejected && windows && undo && (complete==(failure=="checkpoint")),"every recovery component runs and checkpoint survives any failure");
        }
        foreach(string failure in new string[]{"begin","apply","save","rollback","undo restore","complete","none","noop"}) {
            int writes=0,saves=0,begins=0,restores=0,completes=0;bool rejected=false;
            try {WindowsRecovery.Commit(before,failure=="noop" ? before : after,delegate(Settings value) {writes++;if((writes==1 && (failure=="apply" || failure=="rollback" || failure=="undo restore")) || (writes==2 && failure=="rollback")) throw new IOException("write failed");},
                delegate(Settings value) {saves++;if(failure=="save") throw new IOException("save failed");},delegate {begins++;if(failure=="begin") throw new IOException("begin failed");},delegate {restores++;if(failure=="undo restore") throw new IOException("undo failed");},delegate {completes++;if(failure=="complete") throw new IOException("complete failed");});}
            catch(IOException) {rejected=true;}
            if(failure=="noop") Expect(!rejected && writes+saves+begins+restores+completes==0,"identical desktop apply retains useful undo without any checkpoint");
            else if(failure=="none") Expect(!rejected && writes==1 && saves==1 && begins==1 && completes==1 && restores==0,"successful desktop commit persists before writing and completes after saving undo");
            else if(failure=="begin") Expect(rejected && writes==0 && saves==0 && restores==0,"checkpoint failure prevents all desktop mutation");
            else Expect(rejected && writes==2 && restores==1 && ((failure=="rollback" || failure=="undo restore") ? completes==0 : completes>0),"desktop failure attempts independent rollback and preserves incomplete checkpoint");
        }
        foreach(Action invalid in new Action[]{
            delegate {Dictionary<string,object> value=Aim.Parse(Store.Json.Serialize(snapshot));value["version"]=2;WindowsRecovery.Read(value);},
            delegate {Dictionary<string,object> value=Aim.Parse(Store.Json.Serialize(snapshot));value["extra"]=null;WindowsRecovery.Read(value);},
            delegate {Dictionary<string,object> value=Aim.Parse(Store.Json.Serialize(snapshot));value["undo"]="invalid";WindowsRecovery.Read(value);},
            delegate {Dictionary<string,object> value=Aim.Parse(Store.Json.Serialize(snapshot));Aim.Map(value["windows"])["Speed"]="10";WindowsRecovery.Read(value);}
        }) {bool rejected=false;try {invalid();}catch(ArgumentException) {rejected=true;}Expect(rejected,"malformed desktop checkpoint cannot apply settings");}
        Dictionary<string,object> defaults=Aim.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tests","fixtures","rawaccel-default.json")));
        string id="HID\\STARTUP";
        Dictionary<string,object> live=Aim.ConfigurePreset(defaults,defaults,id,GamePresets.Get("valorant-steady").Controls(),true);
        Aim.Map(Aim.DeviceEntry(live,id)["config"])["DPI (normalizes input speed unit: counts/ms -> in/s)"]=1600;
        Aim.Map(Aim.DeviceEntry(live,id)["config"])["Polling rate Hz (keep at 0 for automatic adjustment)"]=1000;
        Dictionary<string,object> seed=Startup.Seed(live,defaults,id);
        Dictionary<string,object> config=new Dictionary<string,object>{{"version",1},{"devicePath","mouse"},{"deviceId",id},{"helper",@"C:\release folder\bin\helox-startup.exe"},{"seed",seed}};
        Startup.Read(config);
        Startup.CheckSeed(defaults,config);
        Expect(Aim.Items(seed["devices"]).Count==1 && Aim.Items(seed["profiles"]).Count==1 && Aim.Describe(seed,id).CurveSpeedUnit=="in/s","startup retains selected calibration without restoring peer profiles");
        Expect(Startup.Policy(defaults,defaults,live,id)=="restore cold driver","default driver is eligible for startup restore");
        Expect(Startup.Policy(live,defaults,live,id)=="already matched","startup matching setup is a read-only no-op");
        Expect(Startup.Policy(Aim.ConfigureBypass(live,id,true),defaults,live,id)=="external settings or bypass retained","startup respects intentional bypass");
        Dictionary<string,object> foreign=Aim.ConfigurePreset(defaults,defaults,"HID\\PEER",GamePresets.Get("cs2").Controls(),true);
        Expect(Startup.Policy(foreign,defaults,live,id)=="external settings or bypass retained","startup preserves peer and external setups");
        Dictionary<string,object> changed=Aim.Parse(Store.Json.Serialize(defaults));Aim.Map(changed["defaultDeviceConfig"])["disable"]=true;
        Expect(Startup.Policy(changed,defaults,live,id)=="external settings or bypass retained","startup refuses a changed global default");
        foreach(string key in new string[]{"profiles","defaultDeviceConfig"}) {
            Dictionary<string,object> damaged=Aim.Parse(Store.Json.Serialize(config)),damagedSeed=Aim.Map(damaged["seed"]);
            if(key=="profiles") Aim.Map(Aim.Items(damagedSeed[key])[0])["Output DPI"]=1100;
            else Aim.Map(damagedSeed[key])["disable"]=true;
            bool rejected=false;try {Startup.CheckSeed(defaults,damaged);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"edited startup baseline cannot restore arbitrary default or peer settings");
        }
        foreach(string key in new string[]{"helper","deviceId","version","seed"}) {
            Dictionary<string,object> invalid=Aim.Parse(Store.Json.Serialize(config));invalid[key]=key=="version" ? (object)2 : key=="seed" ? null : (object)"\"invalid";
            bool rejected=false;try {Startup.Read(invalid);}catch(ArgumentException) {rejected=true;}Expect(rejected,"invalid startup identity, path, version or calibration rejected");
        }
        string folder=Path.Combine(Path.GetTempPath(),"helox-final-"+Guid.NewGuid().ToString("n"));Directory.CreateDirectory(folder);
        try {
            string path=Path.Combine(folder,"selection.json");Expect(MouseSelection.Read(path)==null,"missing saved selection uses normal device resolution");
            Store.Save(path,new Dictionary<string,object>{{"path","mouse-A"}});Expect(MouseSelection.Read(path)=="mouse-A","saved mouse identity round-trips");
            Store.Save(path,new Dictionary<string,object>{{"path",false}});bool rejected=false;try {MouseSelection.Read(path);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"broken selection is refused without silently switching mouse");
            foreach(string broken in new string[]{"null","{invalid"}) {File.WriteAllText(path,broken);rejected=false;try {MouseSelection.Read(path);}catch(InvalidOperationException) {rejected=true;}Expect(rejected,"malformed selection keeps diagnostics usable and requires explicit new choice");}
        }finally {File.Delete(Path.Combine(folder,"selection.json"));Directory.Delete(folder);}
        Console.WriteLine("  passed / durable desktop recovery, startup scope and saved mouse selection / no settings changed");
    }
}
}
