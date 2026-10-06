using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Helox {
internal static class Program {
    private static string selectedPath;
    private static bool json;
    [STAThread] private static int Main(string[] args) {
        try {
            if(args.Length>0) {
                List<string> words=new List<string>(args);
                json=words.Remove("--json");
                if(words.Count==0) throw new ArgumentException("provide a command before --json");
                Run(words.ToArray()); return 0;
            }
            if(!Console.IsOutputRedirected) { Console.ForegroundColor=ConsoleColor.White; Console.Title="helox terminal"; }
            Status();
            Console.WriteLine("\n  performance / setup | set | profile | restore\n  diagnostics / devices | select | probe | measure | calibrate\n  reference   / help | faq | exit");
            while(true) {
                Console.Write("\n  helox / "); string line=Console.ReadLine(); if(line==null) break;
                string[] words=line.Trim().Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if(words.Length==0) continue;
                if(words[0]=="exit" || words[0]=="quit") break;
                try {Run(words);} catch(Exception e) {Console.WriteLine("\n  error / "+e.Message.ToLowerInvariant());}
            }
            return 0;
        } catch(Exception e) {
            if(json) Console.WriteLine(Store.Json.Serialize(new {error=e.Message.ToLowerInvariant()}));
            else Console.Error.WriteLine("  error / "+e.Message.ToLowerInvariant());
            return 1;
        }
    }
    private static void Header() {
        Console.WriteLine("\n  helox terminal                         v0.1.0\n  trust gxt 929 / windows mouse utility\n  ------------------------------------------------");
    }
    private static void Help() {
        Console.WriteLine("\n  status                 device + live windows settings\n  devices                list raw input mouse devices\n  select <index>         choose a device for measurements\n  probe                  read trust hid capabilities\n  setup                  speed 10/20, acceleration off\n  set speed <1..20>       windows pointer speed\n  set acceleration <on|off>\n  set wheel <0..100|page>\n  set doubleclick <200..900>\n  set swap <on|off>\n  measure [3..30]        observed input hz; move continuously\n  calibrate <cm>         dpi estimate from one straight stroke\n  profile save <name>    save windows settings\n  profile apply <name>   apply and verify windows settings\n  profile list           saved profiles\n  restore                restore settings before first change\n  faq                    capabilities and limits\n  help / clear / exit\n\n  command mode: launch.bat status --json\n  settings affect all windows pointers; hardware dpi/hz are separate");
    }
    private static Device Selected() {
        List<Device> devices=Device.List();
        if(selectedPath!=null) {
            foreach(Device d in devices) if(d.Path==selectedPath) return d;
            throw new InvalidOperationException("selected receiver disconnected; run devices and select again");
        }
        List<Device> candidates=devices.FindAll(delegate(Device d){return d.TrustCandidate;});
        if(candidates.Count==1) return candidates[0];
        throw new InvalidOperationException(candidates.Count==0 ? "no matching trust receiver; run devices and select <index>" : "multiple matching receivers; run devices and select <index>");
    }
    private static void Devices() {
        List<Device> devices=Device.List();
        if(json) {Console.WriteLine(Store.Json.Serialize(devices));return;}
        for(int i=0;i<devices.Count;i++) {
            Device d=devices[i];
            Console.WriteLine("\n  "+i+" / "+(d.Product ?? "mouse device").ToLowerInvariant()+(d.TrustCandidate ? " / trust receiver candidate" : ""));
            Console.WriteLine("      "+d.Path.ToLowerInvariant());
        }
        if(devices.Count==0) Console.WriteLine("  no raw input mouse devices");
    }
    private static object StatusData() {
        Device d=null; string note=null;
        try {d=Selected();} catch(Exception e) {note=e.Message.ToLowerInvariant();}
        return new {version="0.1.0",receiver=d,receiverStatus=d==null ? "unselected or disconnected" : "enumerated; mouse power and link not confirmed",
            selectionNote=note,windows=Settings.Read(),hardwareDpi=(int?)null,hardwarePollingHz=(int?)null,batteryPercent=(int?)null,
            hardwareControl="unsupported: no verified vendor protocol",lastDpi=Last<DpiResult>("dpi.json",d),lastRate=Last<RateResult>("rate.json",d)};
    }
    private static T Last<T>(string filename,Device d) where T:class {
        string path=Path.Combine(Store.Root,filename); if(!File.Exists(path) || d==null) return null;
        try {
            T result=Store.Load<T>(path);
            DpiResult dpi=result as DpiResult; RateResult rate=result as RateResult;
            string device=dpi!=null ? dpi.DevicePath : rate!=null ? rate.DevicePath : null;
            return device==d.Path ? result : null;
        } catch {return null;}
    }
    private static void Status() {
        if(json) {Console.WriteLine(Store.Json.Serialize(StatusData()));return;}
        Header();
        Device d=null;
        try {
            d=Selected();
            Console.WriteLine("\n  receiver      enumerated / "+(d.Product ?? "trust candidate").ToLowerInvariant());
            bool confirmed=d.Product!=null && d.Product.IndexOf("gxt 929",StringComparison.OrdinalIgnoreCase)>=0;
            Console.WriteLine("  model         "+(confirmed ? "gxt 929 helox / usb product string" : d.TrustCandidate ? "145f:0326 / model unverified" : "manually selected"));
            Console.WriteLine("  mouse link    not reported; move mouse to verify input");
        } catch(Exception e) {Console.WriteLine("\n  receiver      "+e.Message.ToLowerInvariant());}
        Console.WriteLine("  hardware dpi  unavailable / use physical dpi button\n  hardware hz   unavailable / measure observed input\n  battery       unavailable");
        Settings s=Settings.Read();
        Console.WriteLine("\n  windows / live readback\n  speed         "+s.Speed+" / 20\n  acceleration  "+(s.Acceleration==0 ? "off" : "on / mode "+s.Acceleration)+
            "\n  thresholds    "+s.Threshold1+" / "+s.Threshold2+"\n  wheel         "+(s.WheelLines==-1 ? "page" : s.WheelLines+" lines")+
            "\n  doubleclick   "+s.DoubleClickMs+" ms\n  buttons       "+(s.SwapButtons==0 ? "normal" : "swapped"));
        DpiResult dpi=Last<DpiResult>("dpi.json",d); RateResult rate=Last<RateResult>("rate.json",d);
        if(dpi!=null) Console.WriteLine("\n  last dpi      ~"+F(dpi.EstimatedDpi)+" / distance estimate / "+dpi.MeasuredUtc.ToLowerInvariant());
        if(rate!=null) Console.WriteLine("  last input    ~"+F(rate.ObservedHz)+" hz / observed delivery / "+rate.MeasuredUtc.ToLowerInvariant());
        if(dpi!=null || rate!=null) Console.WriteLine("  history       previous measurement; current hardware values unknown");
        Console.WriteLine("\n  hardware writes / unsupported\n  windows settings / supported + verified\n  raw input measurement / supported\n  backup / "+(File.Exists(Path.Combine(Store.Root,"original.json")) ? "available" : "created before first change"));
    }
    private static string F(double n) {return n.ToString("0.0",CultureInfo.InvariantCulture);}
    private static int Integer(string value) {return int.Parse(value,NumberStyles.Integer,CultureInfo.InvariantCulture);}
    private static int Toggle(string value) {if(value=="on") return 1;if(value=="off") return 0;throw new ArgumentException("use on or off");}
    private static void Apply(Settings s) {
        s.Validate(); Store.Backup(); s.Apply();
        if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=Settings.Read()}));
        else Console.WriteLine("  applied / verified with windows readback / restore is available");
    }
    private static void Measure(string[] words) {
        if(words.Length>2) throw new ArgumentException("usage: measure [seconds]");
        int seconds=words.Length==2 ? Integer(words[1]) : 10;
        if(seconds<3 || seconds>30) throw new ArgumentException("measurement duration: 3..30 seconds");
        Device d=Selected();
        if(!json) Console.WriteLine("  move selected mouse continuously for "+seconds+" seconds; escape cancels");
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(seconds,false); RateResult result=Analysis.Rate(capture.Samples); result.DevicePath=d.Path;
            Store.Save(Path.Combine(Store.Root,"rate.json"),result);
            if(json) Console.WriteLine(Store.Json.Serialize(result));
            else Console.WriteLine("\n  observed      ~"+F(result.ObservedHz)+" hz / median delivery interval\n  active mean   "+F(result.ActiveHz)+" hz\n  interval      median "+F(result.MedianIntervalMs)+" ms / p95 "+F(result.P95IntervalMs)+" ms\n  reports       "+result.Reports+" / idle gaps "+result.IdleGaps+"\n  quality       "+result.Quality+"\n  source        raw input arrival times; includes windows scheduling\n  hardware hz   cannot infer configured usb rate exactly");
        }
    }
    private static void Calibrate(string[] words) {
        if(words.Length!=2 || json || Console.IsInputRedirected) throw new ArgumentException("interactive usage: calibrate <distance in cm>");
        double cm=double.Parse(words[1],CultureInfo.InvariantCulture); Analysis.Dpi(new List<Sample>{new Sample(0,100,0)},cm);
        Device d=Selected();
        Console.WriteLine("  mark two points "+F(cm)+" cm apart; align the mouse horizontally\n  place mouse at the first mark; press enter to start\n  then move to second mark in one straight stroke, without lifting\n  stop at the mark and press enter; escape cancels");
        Console.ReadLine();
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(60,true); DpiResult result=Analysis.Dpi(capture.Samples,cm); result.DevicePath=d.Path;
            Store.Save(Path.Combine(Store.Root,"dpi.json"),result);
            Console.WriteLine("\n  estimated dpi ~"+F(result.EstimatedDpi)+" / "+result.Counts+" counts over "+F(cm)+" cm\n  repeat 3 times to compare; depends on distance and alignment\n  this is an estimate, not sensor dpi readback");
        }
    }
    private static void Profile(string[] words) {
        if(words.Length==2 && words[1]=="list") {
            string dir=Path.Combine(Store.Root,"profiles"); List<string> names=new List<string>();
            if(Directory.Exists(dir)) foreach(string path in Directory.GetFiles(dir,"*.json")) names.Add(Path.GetFileNameWithoutExtension(path));
            names.Sort(StringComparer.Ordinal);
            if(json) Console.WriteLine(Store.Json.Serialize(names)); else Console.WriteLine("  profiles / "+(names.Count==0 ? "none" : String.Join(" / ",names.ToArray())));
        } else if(words.Length==3 && words[1]=="save") {
            Store.Save(Store.Profile(words[2]),Settings.Read());
            if(json) Console.WriteLine(Store.Json.Serialize(new {saved=words[2]})); else Console.WriteLine("  saved / "+words[2]+" / windows settings only");
        } else if(words.Length==3 && words[1]=="apply") Apply(Store.Load<Settings>(Store.Profile(words[2])));
        else throw new ArgumentException("usage: profile list | profile save <name> | profile apply <name>");
    }
    private static void Faq() {
        Console.WriteLine("\n  what changes for real?\n  pointer speed, windows acceleration, scrolling, double-click timing\n  and primary button swap. every apply is checked by reading windows.\n\n  does setup change gaming sensitivity?\n  setup sets speed 10/20 and disables windows pointer acceleration.\n  games using raw input bypass these settings; adjust in-game sens.\n\n  why no dpi or hz slider?\n  standard mouse hid does not define a universal dpi/polling command.\n  no verified protocol exists in this project for the trust receiver.\n  use the physical dpi button. no arbitrary usb writes are sent.\n\n  is measured hz the configured polling rate?\n  no. it measures motion report arrival at this app for one device.\n  windows scheduling, message queues, movement and usb delivery affect it.\n  median and mean can differ. slow movement underestimates reporting.\n\n  can dpi be measured?\n  calibrate 10 counts raw motion across 10 measured cm. repeat 3 times.\n  it estimates counts per inch; pressing dpi later makes it stale.\n\n  does connected mean mouse is on?\n  no. the receiver can remain enumerated while the mouse is off.\n  use measure to confirm that this device sends motion.\n\n  battery, rgb, macros, lod, debounce?\n  unavailable until their vendor protocol is independently verified.\n\n  is this a kernel driver?\n  no. it uses the existing windows hid driver; no service or startup task.\n  close the terminal after applying settings; windows retains them.\n\n  how do i undo changes?\n  restore loads the first pre-change snapshot. profiles save windows settings.\n  local data lives in %localappdata%\\helox-terminal.\n\n  why the wallhack inspiration?\n  concise sections, monochrome text and direct controls.\n  this project is independent of trust and wallhack.");
    }
    private static void Run(string[] words) {
        switch(words[0]) {
            case "status": if(words.Length!=1) break; Status();return;
            case "devices": if(words.Length!=1) break; Devices();return;
            case "select":
                if(words.Length!=2) break;
                List<Device> devices=Device.List(); int index=Integer(words[1]);
                if(index<0 || index>=devices.Count) throw new ArgumentException("device index out of range");
                selectedPath=devices[index].Path;
                if(json) Console.WriteLine(Store.Json.Serialize(new {selected=devices[index]})); else Console.WriteLine("  selected / "+index);return;
            case "setup":
                if(words.Length!=1) break;
                Settings setup=Settings.Read(); setup.Speed=10;setup.Acceleration=0; Apply(setup);return;
            case "set":
                if(words.Length!=3) break;
                Settings s=Settings.Read();
                switch(words[1]) {
                    case "speed": s.Speed=Integer(words[2]);break;
                    case "acceleration": s.Acceleration=Toggle(words[2]); if(s.Acceleration!=0){s.Threshold1=6;s.Threshold2=10;} break;
                    case "wheel": s.WheelLines=words[2]=="page" ? -1 : Integer(words[2]);if(s.WheelLines<-1 || s.WheelLines>100) throw new ArgumentException("wheel: 0..100 or page");break;
                    case "doubleclick": s.DoubleClickMs=Integer(words[2]);if(s.DoubleClickMs<200 || s.DoubleClickMs>900) throw new ArgumentException("doubleclick: 200..900 ms");break;
                    case "swap": s.SwapButtons=Toggle(words[2]);break;
                    default:throw new ArgumentException("unknown setting; use help");
                }
                Apply(s);return;
            case "measure": Measure(words);return;
            case "calibrate": Calibrate(words);return;
            case "profile": Profile(words);return;
            case "restore":
                if(words.Length!=1) break;
                Settings original=Store.Load<Settings>(Path.Combine(Store.Root,"original.json"));original.Apply();
                if(json) Console.WriteLine(Store.Json.Serialize(new {restored=true,readback=Settings.Read()})); else Console.WriteLine("  restored / verified with windows readback");return;
            case "faq":if(words.Length!=1) break;Faq();return;
            case "probe":
                if(words.Length!=1) break;
                List<HidCapability> caps=HidProbe.Read();
                if(json) Console.WriteLine(Store.Json.Serialize(caps));
                else {
                    foreach(HidCapability cap in caps) {
                        Console.WriteLine("\n  hid / "+(cap.Product ?? "trust candidate").ToLowerInvariant()+" / "+cap.UsagePage+":"+cap.Usage);
                        Console.WriteLine("  reports / input "+cap.InputReportBytes+" b / output "+cap.OutputReportBytes+" b / feature "+cap.FeatureReportBytes+" b");
                        if(cap.Error!=null) Console.WriteLine("  error / "+cap.Error);
                    }
                    if(caps.Count==0) Console.WriteLine("  no matching trust hid interfaces");
                    Console.WriteLine("\n  descriptor capabilities only; no feature queries or writes sent\n  report lengths do not establish a dpi/hz protocol");
                } return;
            case "help":if(words.Length!=1) break;Help();return;
            case "clear":if(words.Length!=1) break;if(!Console.IsOutputRedirected) Console.Clear();Header();return;
            case "selftest":if(words.Length!=1) break;SelfTest.Run();return;
        }
        throw new ArgumentException("unknown command or arguments; use help");
    }
}
}
