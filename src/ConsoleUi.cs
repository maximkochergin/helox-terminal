using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
internal static partial class Program {
    private static void Screen(string title) {
        if(!Console.IsOutputRedirected) Console.Clear();
        Header();Console.WriteLine("  "+title+"\n");
    }
    private static void Browse(string title,string items,Action<string> action) {
        while(true) {
            Screen(title);Console.WriteLine(items+"\n  0  back");
            string choice;try {choice=Ask("choose");}catch(OperationCanceledException) {return;}
            try {action(choice);}
            catch(OperationCanceledException) {}
            catch(Exception e) {Console.WriteLine("  "+Error(e));Finish();}
        }
    }
    private static void Menu(string choice) {
        try {
            if(choice=="1" || choice=="9") GameMenu();
            else if(choice=="2" || choice=="8") AimMenu();
            else if(choice=="3") TestMenu();
            else if(choice=="4") Browse("windows pointer / desktop only","  1  acceleration\n  2  pointer speed",delegate(string c) {if(c!="1" && c!="2") throw new ArgumentException("choose 1, 2 or 0");LegacyMenu(c);});
            else if(choice=="5" || choice=="6") LegacyMenu(choice);
            else if(choice=="7") {Status();Finish();}
        }catch(OperationCanceledException) {}
        catch(Exception e) {Console.WriteLine("  "+Error(e));Finish();}
        finally {Home();}
    }
    private static void AimMenu() {
        Browse("tune mouse / driver effects","  1  acceleration     curve and fast-turn response\n  2  motion filters   smoothing, snapping and damping\n  3  driver           install, restore and bypass\n  4  current setup    full live readback",delegate(string c) {
            if(c=="1") Browse("acceleration / slow sensitivity stays 1x","  1  curve builder\n  2  simple acceleration\n  3  acceleration off\n  4  gain stability on\n  5  gain stability off\n  6  tracking starting point",delegate(string n) {AimAction(MapChoice(n,new string[]{"15","1","2","11","12","13"}));});
            else if(c=="2") Browse("motion filters / optional / preview where offered","  1  output smoothing on\n  2  output smoothing off\n  3  angle snapping\n  4  direction scales\n  5  micro damping\n  6  remove flick tail",delegate(string n) {AimAction(MapChoice(n,new string[]{"3","4","16","18","19","22"}));});
            else if(c=="3") Browse("driver / selected mouse","  1  install driver\n  2  check driver\n  3  resume saved settings\n  4  undo last aim change\n  5  bypass or enable effects\n  6  uninstall shared driver",delegate(string n) {AimAction(MapChoice(n,new string[]{"5","9","8","6","20","10"}));});
            else if(c=="4") {PrintAim(Aim.Read(OptionalSelected()));Finish();}
            else throw new ArgumentException("choose 1..4 or 0");
        });
    }
    private static string MapChoice(string choice,string[] values) {
        int number=Integer(choice);if(number<1 || number>values.Length) throw new ArgumentException("choose 1.."+values.Length+" or 0");return values[number-1];
    }
    private static void TestMenu() {
        Browse("test mouse / choose what to check","  1  health overview\n  2  hz and input gaps\n  3  dpi estimate\n  4  current response model\n  5  live driver verification\n  6  shutdown errors",delegate(string c) {
            if(c=="1") HealthCheck();
            else if(c=="2") Measure(new string[]{"measure"});
            else if(c=="3") DpiCheck();
            else if(c=="4") AimCommand(new string[]{"aim","response"});
            else if(c=="5") {AimAction("21");return;}
            else if(c=="6") PrintShutdown(ShutdownDiagnostics.Read());
            else throw new ArgumentException("choose 1..6 or 0");Finish();
        });
    }
    private static void TuneMovement() {
        if(json) throw new ArgumentException("preset tune is interactive / run without --json");
        if(Console.IsInputRedirected) throw new InvalidOperationException("movement tuning needs an interactive console");
        Device device=Selected();CheckTuningInput(device);
        Screen("movement tuning / two 8-second captures");
        Console.WriteLine("  use the same physical dpi stage as in your games\n  effects must be bypassed; no settings are changed\n  do not lift the mouse; escape cancels\n\n  first / small slow corrections, continuously");
        if(Ask("1 start / 0 back")!="1") throw new ArgumentException("choose 1 or 0");
        List<Sample> slow,fast;
        using(RawCapture capture=new RawCapture(device)) {
            RequirePresetMouse(device.Path,Selected());CheckTuningInput(device);capture.Collect(8,false);
            if(capture.AbsoluteReports!=0) throw new InvalidOperationException("relative mouse required");slow=new List<Sample>(capture.Samples);
            Console.WriteLine("\n  second / fast sweeps at your normal flick speed, continuously");
            if(Ask("1 start / 0 back")!="1") throw new ArgumentException("choose 1 or 0");
            RequirePresetMouse(device.Path,Selected());CheckTuningInput(device);capture.Collect(8,false);
            if(capture.AbsoluteReports!=0) throw new InvalidOperationException("relative mouse required");fast=new List<Sample>(capture.Samples);
        }
        RequirePresetMouse(device.Path,Selected());CheckTuningInput(device);
        MovementTuning tuning=PresetTuning.Analyze(device.Path,slow,fast);PresetTuning.Save(tuning);
        Console.WriteLine("  saved / slow "+N(tuning.SlowP90)+" / fast "+N(tuning.FastP75)+" counts/ms\n  choose personal range in game setup / expires after 7 days\n  repeat after changing physical dpi / stage cannot be detected");Finish();
    }
    private static void CheckTuningInput(Device device) {
        AimStatus status=Aim.Read(device);
        if(status.InputTransformed!=false) throw new InvalidOperationException(status.State=="ready" ? "bypass effects first / tune mouse > driver > bypass / settings are not changed automatically" : "input transform state unknown / check driver before movement tuning");
    }
}
}
