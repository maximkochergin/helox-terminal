using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Helox {
internal static class Program {
    internal const string Version="0.3.0";
    private static string selectedPath;
    private static bool json;
    [STAThread] private static int Main(string[] args) {
        try {
            if(args.Length>0) {
                List<string> words=new List<string>();
                foreach(string arg in args) words.Add(arg.ToLowerInvariant());
                json=words.Remove("--json");
                if(words.Contains("--json")) throw new ArgumentException("use --json once");
                if(words.Count==0) throw new ArgumentException("choose a command");
                Run(words.ToArray());return 0;
            }
            if(!Console.IsOutputRedirected) {Console.ForegroundColor=ConsoleColor.White;Console.Title="helox terminal";}
            Home();
            while(true) {
                Console.Write("\n  > ");string line=Console.ReadLine();if(line==null) break;
                string[] words=line.Trim().ToLowerInvariant().Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if(words.Length==0) continue;
                if(words[0]=="exit" || words[0]=="quit" || (words.Length==1 && words[0]=="0")) break;
                bool menuChoice=words.Length==1 && words[0].Length==1 && words[0][0]>='1' && words[0][0]<='7';
                try {
                    if(menuChoice) Menu(words[0]);
                    else Run(words);
                } catch(OperationCanceledException) {if(menuChoice) Home();Console.WriteLine("  cancelled");}
                catch(Exception e) {if(menuChoice) Home();Console.WriteLine("  "+Error(e));}
            }
            return 0;
        } catch(Exception e) {
            if(json) Console.WriteLine(Store.Json.Serialize(new {error=Error(e)}));else Console.Error.WriteLine("  "+Error(e));
            return 1;
        }
    }
    private static string Error(Exception e) {
        if(e is FileNotFoundException) return "file not found; save a profile or change a setting first";
        if(e is FormatException || e is OverflowException) return "enter a valid number";
        return e.Message.ToLowerInvariant();
    }
    private static void Header() {Console.WriteLine("\n  helox / "+Version+"\n  ----------------------------------------");}
    private static void Home() {
        if(!Console.IsOutputRedirected) Console.Clear();
        CompactStatus();
        Console.WriteLine("\n  1  acceleration     2  pointer speed\n  3  test hz          4  check dpi\n  5  profiles         6  more\n  7  mouse status     0  exit\n\n  choose a number / enter");
    }
    private static string Ask(string prompt) {
        Console.Write("\n  "+prompt+" > ");string value=Console.ReadLine();
        if(String.IsNullOrWhiteSpace(value) || value.Trim()=="0" || value.Trim().Equals("back",StringComparison.OrdinalIgnoreCase)) throw new OperationCanceledException();
        return value.Trim().ToLowerInvariant();
    }
    private static void Menu(string choice) {
        switch(choice) {
            case "1":
                Console.WriteLine("\n  windows acceleration / raw input games bypass it\n  1  on     2  off     0  back");
                string accel=Ask("choose");if(accel=="0") {Home();return;}
                if(accel!="1" && accel!="2") throw new ArgumentException("choose 1, 2 or 0");
                Run(new string[]{"set","acceleration",accel=="1" ? "on" : "off"});Home();return;
            case "2":Run(new string[]{"set","speed",Ask("speed 1..20 / current "+Settings.Read().Speed)});Home();return;
            case "3":Measure(new string[]{"measure"});Finish();return;
            case "4":DpiCheck();Finish();return;
            case "7":Status();Finish();return;
            case "5":
                Profile(new string[]{"profile","list"});
                Console.WriteLine("\n  1  save     2  load     3  restore original     0  back");
                string profile=Ask("choose");
                if(profile=="0") {Home();return;}
                if(profile=="3") Run(new string[]{"restore"});
                else if(profile=="1") Profile(new string[]{"profile","save",Ask("profile name / 0 back")});
                else if(profile=="2") {
                    List<string> names=Store.Profiles();if(names.Count==0) throw new ArgumentException("no saved profiles yet");
                    for(int i=0;i<names.Count;i++) Console.WriteLine("  "+(i+1)+"  "+names[i]);
                    int saved=Integer(Ask("profile number / 0 back"));if(saved<1 || saved>names.Count) throw new ArgumentException("profile number out of range");
                    Profile(new string[]{"profile","apply",names[saved-1]});
                }
                else throw new ArgumentException("choose 1, 2, 3 or 0");
                Home();return;
            case "6":
                Console.WriteLine("\n  1  choose mouse     2  advanced commands     3  faq     0  back");
                string more=Ask("choose");
                if(more=="0") Home();
                else if(more=="1") {Devices();Run(new string[]{"select",Ask("mouse number")});Home();}
                else if(more=="2") {Help();Finish();}
                else if(more=="3") {Faq();Finish();}
                else throw new ArgumentException("choose 1, 2, 3 or 0");return;
        }
    }
    private static void Finish() {
        if(Console.IsInputRedirected || Console.IsOutputRedirected) return;
        Console.Write("\n  enter / back ");
        while(true) {ConsoleKey key=Console.ReadKey(true).Key;if(key==ConsoleKey.Enter || key==ConsoleKey.Escape) break;}
        Home();
    }
    private static void Help() {
        Console.WriteLine("\n  setup                  speed 10/20 + windows accel on\n  set acceleration on|off\n  set speed 1..20\n  set wheel 0..100|page\n  set doubleclick 200..900\n  set swap on|off\n  measure 3..30          observed input hz\n  dpi                    three-pass check, no ruler\n  calibrate <cm>         known-distance dpi estimate\n  profile save|apply <name>\n  profile list / restore\n  devices / select <index> / probe\n  status / home / faq / exit\n\n  export: launch.bat status --json");
    }
    private static Device Selected() {
        List<Device> devices=Device.List();
        if(selectedPath!=null) {
            foreach(Device d in devices) if(String.Equals(d.Path,selectedPath,StringComparison.OrdinalIgnoreCase)) return d;
            throw new InvalidOperationException("selected mouse disconnected; choose mouse again");
        }
        List<Device> candidates=devices.FindAll(delegate(Device d){return d.TrustCandidate;});
        if(candidates.Count==1) return candidates[0];
        throw new InvalidOperationException(candidates.Count==0 ? "trust receiver not found; choose mouse in more" : "multiple receivers; choose mouse in more");
    }
    private static void Devices() {
        List<Device> devices=Device.List();
        if(json) {Console.WriteLine(Store.Json.Serialize(devices));return;}
        for(int i=0;i<devices.Count;i++) Console.WriteLine("  "+i+"  "+(devices[i].Product ?? "mouse device").ToLowerInvariant()+(devices[i].TrustCandidate ? " / trust" : ""));
        if(devices.Count==0) Console.WriteLine("  no mice detected");
    }
    private static object StatusData() {
        Device d=null;string note=null;try {d=Selected();}catch(Exception e) {note=Error(e);}
        return new {version=Version,receiver=d,receiverStatus=d==null ? "unselected or disconnected" : "enumerated; mouse power and link not confirmed",
            selectionNote=note,windows=Settings.Read(),dossier=MouseDossier.Read(d),hardwareDpi=(int?)null,hardwarePollingHz=(int?)null,batteryPercent=(int?)null,
            hardwareControl="unsupported: no verified vendor protocol",gameAcceleration="not managed; windows acceleration is bypassed by raw input",
            lastDpi=Last<DpiResult>("dpi.json",d),lastRate=Last<RateResult>("rate.json",d)};
    }
    private static T Last<T>(string filename,Device d) where T:class {
        string path=Path.Combine(Store.Root,filename);if(!File.Exists(path) || d==null) return null;
        try {
            T result=Store.Load<T>(path);DpiResult dpi=result as DpiResult;RateResult rate=result as RateResult;
            string device=dpi!=null ? dpi.DevicePath : rate!=null ? rate.DevicePath : null;
            return String.Equals(device,d.Path,StringComparison.OrdinalIgnoreCase) ? result : null;
        }catch {return null;}
    }
    private static void CompactStatus() {
        Header();Device d=null;
        try {d=Selected();Console.WriteLine("  receiver  "+(d.Product ?? "mouse device").ToLowerInvariant());}
        catch {Console.WriteLine("  receiver  not selected / use more");}
        Settings s=Settings.Read();
        Console.WriteLine("  windows   speed "+s.Speed+"/20 / accel "+(s.Acceleration==0 ? "off" : "on")+"\n  hardware  dpi ? / hz ?");
        DpiResult dpi=Last<DpiResult>("dpi.json",d);RateResult rate=Last<RateResult>("rate.json",d);
        if(dpi!=null || rate!=null) Console.WriteLine("  last test "+(dpi!=null ? "~"+F(dpi.EstimatedDpi)+" dpi" : "")+(dpi!=null && rate!=null ? " / " : "")+(rate!=null ? "~"+F(rate.ActiveHz)+" hz" : "")+" / history");
    }
    private static void Status() {
        if(json) {Console.WriteLine(Store.Json.Serialize(StatusData()));return;}
        CompactStatus();
        Device device=null;try {device=Selected();}catch {}
        MouseDossier dossier=MouseDossier.Read(device);
        if(device!=null) {
            Console.WriteLine("\n  device / read from windows");
            Console.WriteLine("  driver buttons  "+(device.DriverButtonCount.HasValue ? device.DriverButtonCount.Value.ToString() : "unknown")+" / advertised, not physical count");
            Console.WriteLine("  driver hz       "+(device.DriverSampleRate>0 ? device.DriverSampleRate+" / declared, not measured" : "not reported"));
            if(dossier.Driver!=null) Console.WriteLine("  driver          "+(dossier.Driver.Provider ?? "unknown").ToLowerInvariant()+" / "+(dossier.Driver.Version ?? "unknown")+" / "+(dossier.Driver.Inf ?? "unknown"));
            if(dossier.Hid!=null) foreach(HidCapability hid in dossier.Hid)
                Console.WriteLine("  hid             "+(hid.VendorId ?? "?")+":"+(hid.ProductId ?? "?")+" / rev "+(hid.DeviceRevision ?? "?")+" / "+hid.UsagePage+":"+hid.Usage+" / in "+hid.InputReportBytes+"b / out "+hid.OutputReportBytes+"b");
        }
        if(dossier.Model!=null) {
            Console.WriteLine("\n  model / manufacturer specifications");
            Console.WriteLine("  dpi range       800..4800 / 4 stages; active stage unknown\n  body            125 x 64 x 38 mm\n  connection      2.4 ghz wireless\n  listed buttons  5 / dpi button present");
        }
        Settings settings=Settings.Read();
        Console.WriteLine("\n  windows / live\n  wheel           "+(settings.WheelLines==-1 ? "page" : settings.WheelLines+" lines")+" / doubleclick "+settings.DoubleClickMs+" ms\n  buttons         "+(settings.SwapButtons==0 ? "normal" : "swapped"));
        DpiResult lastDpi=Last<DpiResult>("dpi.json",device);RateResult lastRate=Last<RateResult>("rate.json",device);
        if(lastDpi!=null) Console.WriteLine("\n  dpi history / "+lastDpi.MeasuredUtc+" / "+lastDpi.Trials+" pass(es)"+(lastDpi.SpreadPercent.HasValue ? " / spread "+F(lastDpi.SpreadPercent.Value)+"%" : ""));
        if(lastRate!=null) Console.WriteLine("  hz history / "+lastRate.MeasuredUtc+" / "+lastRate.Reports+" reports / "+lastRate.Quality);
        Console.WriteLine("\n  unavailable / sensor dpi, configured hz, battery, link power\n  dpi check / uses mouse body length; estimate, not readback");
    }
    private static void StartPass(string prompt) {
        Console.WriteLine("  "+prompt+" / enter starts / esc cancels");
        while(true) {
            ConsoleKey key=Console.ReadKey(true).Key;
            if(key==ConsoleKey.Escape) throw new OperationCanceledException();
            if(key==ConsoleKey.Enter) return;
        }
    }
    private static void DpiCheck() {
        if(json || Console.IsInputRedirected) throw new ArgumentException("dpi check requires an interactive terminal");
        Device device=Selected();ModelFacts facts=ModelFacts.For(device);
        if(facts==null) throw new ArgumentException("mouse-body dpi check supports gxt 929 only / use calibrate <cm>");
        Console.WriteLine("\n  dpi check / no ruler or marks / approximate\n  keep one fingertip beside the mouse front edge\n  slide forward until the rear edge reaches that finger\n  keep the finger still; do not rotate or lift\n  repeat 3 times / mouse length 125 mm");
        List<DpiResult> trials=new List<DpiResult>();
        for(int pass=1;pass<=3;pass++) {
            StartPass("pass "+pass+"/3 / position mouse and finger");
            Device fresh=Selected();
            if(!String.Equals(fresh.Path,device.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / restart check");
            using(RawCapture capture=new RawCapture(fresh)) {
                Console.WriteLine("  slide one body length / enter finishes");capture.Collect(45,true);
                DpiResult trial=Analysis.Dpi(capture.Samples,facts.LengthMm/10.0);trials.Add(trial);
                Console.WriteLine("  pass "+pass+" / ~"+F(trial.EstimatedDpi)+" dpi");
            }
        }
        DpiResult result=Analysis.CombineDpi(trials,"mouse-body-length estimate; distance set from manufacturer length; not hardware readback");result.DevicePath=device.Path;
        Store.Save(Path.Combine(Store.Root,"dpi.json"),result);
        Console.WriteLine("\n  ~"+F(result.EstimatedDpi)+" dpi / median of 3 passes\n  repeat spread "+F(result.SpreadPercent.Value)+"% / repeatability, not accuracy");
    }
    private static string F(double n) {return n.ToString("0.0",CultureInfo.InvariantCulture);}
    private static int Integer(string value) {return int.Parse(value,NumberStyles.Integer,CultureInfo.InvariantCulture);}
    private static int Toggle(string value) {if(value=="on") return 1;if(value=="off") return 0;throw new ArgumentException("use on or off");}
    private static void Apply(Settings s) {
        if(s==null) throw new ArgumentException("empty settings file");
        s.Validate();Store.Apply(s,true);
        Applied();
    }
    private static void Applied() {
        if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=Settings.Read()}));else Console.WriteLine("  applied / verified");
    }
    private static void Measure(string[] words) {
        if(words.Length>2) throw new ArgumentException("use measure [seconds]");
        int seconds=words.Length==2 ? Integer(words[1]) : 10;
        if(seconds<3 || seconds>30) throw new ArgumentException("duration: 3..30 seconds");
        Device d=Selected();if(!json) Console.WriteLine("\n  move mouse in circles / "+seconds+"s / esc cancels");
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(seconds,false);RateResult result=Analysis.Rate(capture.Samples);result.DevicePath=d.Path;
            Store.Save(Path.Combine(Store.Root,"rate.json"),result);
            if(json) Console.WriteLine(Store.Json.Serialize(result));
            else Console.WriteLine("  ~"+F(result.ActiveHz)+" hz / observed input\n  "+result.Quality+"\n  home / return to menu");
        }
    }
    private static void Calibrate(string[] words) {
        if(words.Length!=2 || json || Console.IsInputRedirected) throw new ArgumentException("use calibrate <cm> in a terminal");
        double cm=Analysis.Distance(words[1]);Device d=Selected();
        Console.WriteLine("\n  mark "+F(cm)+" cm on pad; place mouse at first mark\n  press enter, move straight to second mark, press enter\n  do not lift or return / esc cancels");
        StartPass("ready");
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(60,true);DpiResult result=Analysis.Dpi(capture.Samples,cm);result.DevicePath=d.Path;
            Store.Save(Path.Combine(Store.Root,"dpi.json"),result);
            Console.WriteLine("  ~"+F(result.EstimatedDpi)+" dpi / estimate\n  home / return to menu");
        }
    }
    private static void Profile(string[] words) {
        if(words.Length==2 && words[1]=="list") {
            List<string> names=Store.Profiles();
            if(json) Console.WriteLine(Store.Json.Serialize(names));else Console.WriteLine("  profiles / "+(names.Count==0 ? "none yet" : String.Join(" / ",names.ToArray())));
        }else if(words.Length==3 && words[1]=="save") {
            Store.Save(Store.Profile(words[2]),Settings.Read());if(json) Console.WriteLine(Store.Json.Serialize(new {saved=words[2]}));else Console.WriteLine("  saved / "+words[2]);
        }else if(words.Length==3 && words[1]=="apply") Apply(Store.Load<Settings>(Store.Profile(words[2])));
        else throw new ArgumentException("use profile save|apply <name> or profile list");
    }
    private static void Faq() {
        Console.WriteLine("\n  game acceleration?\n  windows accel does not affect raw input games. game-wide accel\n  needs a separate input driver; helox does not install one.\n\n  dpi / hz show ?\n  hardware values cannot be read yet. use the physical dpi button.\n  test hz measures delivered input; dpi uses three mouse-body passes.\n\n  undo settings?\n  profiles > restore original. profiles store windows settings.\n\n  receiver detected but no input?\n  receiver presence does not confirm mouse power. check the switch.\n\n  home / return to menu");
    }
    private static void Run(string[] words) {
        if(json && (words[0]=="help" || words[0]=="faq" || words[0]=="home" || words[0]=="clear" || words[0]=="selftest"))
            throw new ArgumentException("json is not supported for this command");
        switch(words[0].ToLowerInvariant()) {
            case "status":if(words.Length!=1) break;Status();return;
            case "devices":if(words.Length!=1) break;Devices();return;
            case "select":
                if(words.Length!=2) break;List<Device> devices=Device.List();int index=Integer(words[1]);
                if(index<0 || index>=devices.Count) throw new ArgumentException("mouse number out of range");
                selectedPath=devices[index].Path;
                if(json) Console.WriteLine(Store.Json.Serialize(new {selected=devices[index]}));else Console.WriteLine("  selected / "+index);return;
            case "setup":if(words.Length!=1) break;Store.Update(delegate(Settings current) {current.Setup();});Applied();return;
            case "set":
                if(words.Length!=3) break;
                Store.Update(delegate(Settings s) {
                switch(words[1]) {
                    case "speed":s.Speed=Integer(words[2]);if(s.Speed<1 || s.Speed>20) throw new ArgumentException("speed: 1..20");break;
                    case "acceleration":s.SetAcceleration(Toggle(words[2])!=0);break;
                    case "wheel":s.WheelLines=words[2]=="page" ? -1 : Integer(words[2]);if((s.WheelLines<0 && words[2]!="page") || s.WheelLines>100) throw new ArgumentException("wheel: 0..100 or page");break;
                    case "doubleclick":s.DoubleClickMs=Integer(words[2]);if(s.DoubleClickMs<200 || s.DoubleClickMs>900) throw new ArgumentException("doubleclick: 200..900 ms");break;
                    case "swap":s.SwapButtons=Toggle(words[2]);break;
                    default:throw new ArgumentException("unknown setting / use help");
                }});Applied();return;
            case "measure":Measure(words);return;
            case "dpi":if(words.Length!=1) break;DpiCheck();return;
            case "calibrate":Calibrate(words);return;
            case "profile":Profile(words);return;
            case "restore":
                if(words.Length!=1) break;string path=Path.Combine(Store.Root,"original.json");
                if(!File.Exists(path)) throw new InvalidOperationException("nothing to restore yet");
                Settings original=Store.Load<Settings>(path);if(original==null) throw new ArgumentException("empty backup file");Store.Apply(original,false);
                if(json) Console.WriteLine(Store.Json.Serialize(new {restored=true,readback=Settings.Read()}));else Console.WriteLine("  restored / verified");return;
            case "faq":if(words.Length!=1) break;Faq();return;
            case "probe":
                if(words.Length!=1) break;List<HidCapability> caps=HidProbe.Read(Selected().Path);
                if(json) Console.WriteLine(Store.Json.Serialize(caps));
                else {
                    foreach(HidCapability cap in caps) Console.WriteLine("  hid "+cap.UsagePage+":"+cap.Usage+" / in "+cap.InputReportBytes+" / out "+cap.OutputReportBytes+" / feature "+cap.FeatureReportBytes+(cap.Error!=null ? " / "+cap.Error : ""));
                    if(caps.Count==0) Console.WriteLine("  trust receiver not found");
                }return;
            case "help":if(words.Length!=1) break;Help();return;
            case "home":case "clear":if(words.Length!=1 || json) break;Home();return;
            case "selftest":if(words.Length!=1) break;SelfTest.Run();return;
        }throw new ArgumentException("unknown choice / type home or help");
    }
}
}
