using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Helox {
internal static partial class Program {
    internal const string Version="0.17.1";
    private static string selectedPath;
    private static string commandMousePath;
    private static bool json;
    [STAThread] private static int Main(string[] args) {
        try {
            if(args.Length>0) {
                string[] words=Arguments(args);
                if(!(words.Length==1 && (words[0]=="maintenance-validate" || words[0]=="maintenance-restore" || words[0]=="maintenance-aim-restore"))) Maintenance.CheckStartup();
                Run(words);return 0;
            }
            Maintenance.CheckStartup();
            if(!Console.IsOutputRedirected) {Console.ForegroundColor=ConsoleColor.White;Console.Title="helox terminal";}
            Home();
            while(true) {
                Console.Write("\n  > ");string line=Console.ReadLine();if(line==null) break;
                string[] words=line.Trim().ToLowerInvariant().Split(new char[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if(words.Length==0) continue;
                if(words.Length==1 && (words[0]=="exit" || words[0]=="quit" || words[0]=="0")) break;
                bool menuChoice=words.Length==1 && words[0].Length==1 && words[0][0]>='1' && words[0][0]<='9';
                try {
                    if(menuChoice) Menu(words[0]);
                    else Run(Arguments(words));
                } catch(OperationCanceledException e) {if(menuChoice) Home();if(json) Console.WriteLine(Store.Json.Serialize(new {error=Error(e)}));else Console.WriteLine("  cancelled");}
                catch(Exception e) {if(menuChoice) Home();if(json) Console.WriteLine(Store.Json.Serialize(new {error=Error(e)}));else Console.WriteLine("  "+Error(e));}
                finally {json=false;commandMousePath=null;}
            }
            return 0;
        } catch(Exception e) {
            if(json) Console.WriteLine(Store.Json.Serialize(new {error=Error(e)}));else Console.Error.WriteLine("  "+Error(e));
            return 1;
        }
    }
    private static string[] Arguments(string[] args) {
        List<string> words=new List<string>();
        foreach(string arg in args) words.Add(arg.ToLowerInvariant());
        json=words.Remove("--json");
        if(words.Contains("--json")) throw new ArgumentException("use --json once");
        int? mouse=MouseOption(words);
        if(words.Count==0) throw new ArgumentException("choose a command");
        if(mouse.HasValue) {
            List<Device> devices=Device.List();
            if(mouse.Value>=devices.Count) throw new ArgumentException("mouse index out of range / use devices");
            commandMousePath=ResolveChoice(devices,mouse.Value+1,Device.List()).Path;
        }
        return words.ToArray();
    }
    internal static int? MouseOption(List<string> words) {
        int position=words.IndexOf("--mouse");if(position<0) return null;
        int index;
        if(position+1>=words.Count || !Int32.TryParse(words[position+1],NumberStyles.None,CultureInfo.InvariantCulture,out index)) throw new ArgumentException("use --mouse <index> / indices from devices");
        words.RemoveRange(position,2);
        if(words.Contains("--mouse")) throw new ArgumentException("use --mouse once");return index;
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
        Console.WriteLine("\n  1  game setup       choose a game and apply a recipe\n  2  tune mouse       acceleration and motion filters\n  3  test mouse       health, hz, dpi and driver checks\n  4  windows pointer  desktop settings\n  5  saved profiles   windows settings and undo\n  6  more             mouse selection, help and cleanup\n  0  exit\n\n  choose a number / enter");
    }
    private static string Ask(string prompt) {
        Console.Write("\n  "+prompt+" > ");string value=Console.ReadLine();
        if(String.IsNullOrWhiteSpace(value) || value.Trim()=="0" || value.Trim().Equals("back",StringComparison.OrdinalIgnoreCase)) throw new OperationCanceledException();
        return value.Trim().ToLowerInvariant();
    }
    private static void LegacyMenu(string choice) {
        switch(choice) {
            case "1":
                Console.WriteLine("\n  windows acceleration / raw input games bypass it\n  1  on     2  off     0  back");
                string accel=Ask("choose");if(accel=="0") {Home();return;}
                if(accel!="1" && accel!="2") throw new ArgumentException("choose 1, 2 or 0");
                Run(new string[]{"set","acceleration",accel=="1" ? "on" : "off"});Finish();return;
            case "2":Run(new string[]{"set","speed",Ask("speed 1..20 / current "+Settings.Read().Speed)});Finish();return;
            case "3":Measure(new string[]{"measure"});Finish();return;
            case "4":DpiCheck();Finish();return;
            case "7":Status();Finish();return;
            case "8":AimMenu();return;
            case "9":GameMenu();return;
            case "5":
                Profile(new string[]{"profile","list"});
                Console.WriteLine("\n  1  save     2  load     3  restore original\n  4  undo last change    5  preview     0  back");
                string profile=Ask("choose");
                if(profile=="0") {Home();return;}
                if(profile=="3") Run(new string[]{"restore"});
                else if(profile=="4") Run(new string[]{"undo"});
                else if(profile=="1") Profile(new string[]{"profile","save",Ask("profile name / 0 back")});
                else if(profile=="2" || profile=="5") {
                    List<string> names=Store.Profiles();if(names.Count==0) throw new ArgumentException("no saved profiles yet");
                    for(int i=0;i<names.Count;i++) Console.WriteLine("  "+(i+1)+"  "+names[i]);
                    int saved=Integer(Ask("profile number / 0 back"));if(saved<1 || saved>names.Count) throw new ArgumentException("profile number out of range");
                    Profile(new string[]{"profile",profile=="5" ? "show" : "apply",names[saved-1]});
                }
                else throw new ArgumentException("choose 1..5 or 0");
                Finish();return;
            case "6":
                Console.WriteLine("\n  1  choose mouse     2  advanced commands     3  faq\n  4  check driver     5  uninstall driver\n  6  reset all helox data + driver\n  7  shutdown errors  8  health check\n  0  back");
                string more=Ask("choose");
                if(more=="0") Home();
                else if(more=="1") {ChooseMouse();Finish();}
                else if(more=="2") {Help();Finish();}
                else if(more=="3") {Faq();Finish();}
                else if(more=="4") {AimCommand(new string[]{"aim","doctor"});Finish();}
                else if(more=="5") {ConfirmUninstall();Finish();}
                else if(more=="6") {
                    Console.WriteLine("  restores original windows settings if backed up\n  removes the shared raw accel driver, all profiles, backups and cache\n  backups cannot be recovered / restart required if driver installed");
                    if(Ask("type reset / 0 back")!="reset") throw new OperationCanceledException();
                    Maintenance.Reset();
                }
                else if(more=="7") {PrintShutdown(ShutdownDiagnostics.Read());Finish();}
                else if(more=="8") {HealthCheck();Finish();}
                else throw new ArgumentException("choose 1..8 or 0");return;
        }
    }
    private static void AimAction(string choice) {
        if(choice=="1") {
            Console.WriteLine("  precision / slow corrections 1x / fast-motion limit\n  1  steady 1.2x    2  balanced 1.4x    3  flick 1.6x    0  back");
            string strength=Ask("choose");
            if(strength!="1" && strength!="2" && strength!="3") throw new ArgumentException("choose 1..3 or 0");
            AimCommand(new string[]{"aim","precision","on",strength=="1" ? "1.2" : strength=="2" ? "1.4" : "1.6"});
        }else if(choice=="2") AimCommand(new string[]{"aim","precision","off"});
        else if(choice=="3" || choice=="4") {
            if(choice=="3") {
                Console.WriteLine("  smooth / averages magnitude / can overshoot after flicks\n  1  light 2 ms     2  balanced 4 ms     3  strong 8 ms     0  back");
                string strength=Ask("choose");
                if(strength!="1" && strength!="2" && strength!="3") throw new ArgumentException("choose 1..3 or 0");
                AimCommand(new string[]{"aim","smooth","on",strength=="1" ? "2" : strength=="2" ? "4" : "8"});
            }else AimCommand(new string[]{"aim","smooth","off"});
        } else if(choice=="5") AimCommand(new string[]{"aim","install"});
        else if(choice=="6") AimCommand(new string[]{"aim","restore"});
        else if(choice=="7") Measure(new string[]{"measure"});
        else if(choice=="8") AimCommand(new string[]{"aim","resume"});
        else if(choice=="9") AimCommand(new string[]{"aim","doctor"});
        else if(choice=="10") ConfirmUninstall();
        else if(choice=="11" || choice=="12") AimCommand(new string[]{"aim","stability",choice=="11" ? "on" : "off"});
        else if(choice=="13") AimCommand(new string[]{"aim","tracking"});
        else if(choice=="14") AimCommand(new string[]{"aim","response"});
        else if(choice=="15") {CurveBuilder();return;}
        else if(choice=="16") {
            Console.WriteLine("  snap / axis direction filter / optional, not riot certified\n  1  off    2  1 degree    3  2 degrees    4  custom 0..5    0  back");
            string snap=Ask("choose");
            if(snap=="1") AimCommand(new string[]{"aim","snap","off"});
            else if(snap=="2" || snap=="3") AimCommand(new string[]{"aim","snap","on",snap=="2" ? "1" : "2"});
            else if(snap=="4") AimCommand(new string[]{"aim","snap","on",ReadNumber("angle 0..5 degrees",1).ToString(CultureInfo.InvariantCulture)});
            else throw new ArgumentException("choose 1..4 or 0");
        }
        else if(choice=="17") PrintShutdown(ShutdownDiagnostics.Read());
        else if(choice=="18") DirectionMenu();
        else if(choice=="19") DampingMenu();
        else if(choice=="20") {
            Console.WriteLine("  selected mouse / 1 bypass all    2 enable current profile    0 back");
            string action=Ask("choose");if(action!="1" && action!="2") throw new ArgumentException("choose 1..2 or 0");
            AimCommand(new string[]{"aim","bypass",action=="1" ? "on" : "off"});
        }
        else if(choice=="21") {
            if(File.Exists(LiveVerify.RecoveryPath)) AimCommand(new string[]{"aim","verify","restore"});
            else {Console.WriteLine("  temporary driver writes / original state restored / close games first");AimCommand(new string[]{"aim","verify"});}
        }
        else if(choice=="22") {
            Console.WriteLine("  remove output averaging / keep curve, dpi and game sensitivity\n  reduces the filter's flick tail / cannot reconstruct sensor tracking\n  1 apply    0 back");
            if(Ask("choose")!="1") throw new ArgumentException("choose 1 or 0");
            AimCommand(new string[]{"aim","smooth","off"});
        }
        else throw new ArgumentException("choose 1..22 or 0");
        Finish();
    }
    private static void DirectionMenu() {
        Console.WriteLine("  direction scales / 1x keeps / lower attenuates\n  1 off    2 vertical 0.85x    3 custom    0 back");string choice=Ask("choose");
        if(choice=="1") {AimCommand(new string[]{"aim","directions","off"});return;}
        if(choice!="2" && choice!="3") throw new ArgumentException("choose 1..3 or 0");
        AimDirections draft=new AimDirections();
        if(choice=="2") draft.Up=draft.Down=.85;
        else {draft.Left=ReadNumber("left 0.25..1x",1);draft.Right=ReadNumber("right 0.25..1x",1);draft.Up=ReadNumber("up 0.25..1x",1);draft.Down=ReadNumber("down 0.25..1x",1);}
        draft.Check();FilterDraft("directions",draft,null,null);
    }
    private static void GameMenu() {
        Browse("game setup / complete windows + aim recipes","  1  valorant\n  2  counter-strike 2\n  3  kovaak's\n  4  measure personal speed range\n  5  undo or recover last game setup\n  6  check active recipe",delegate(string c) {
            if(c=="1" || c=="2") GameActions(c=="1" ? "valorant" : "cs2");
            else if(c=="3") Browse("kovaak's / match your game or train tracking","  1  match valorant\n  2  match counter-strike 2\n  3  tracking practice",delegate(string n) {GameActions(MapChoice(n,new string[]{"kovaaks-valorant","kovaaks-cs2","kovaaks-tracking"}));});
            else if(c=="4") TuneMovement();
            else if(c=="5") {PresetCommand(new string[]{"preset",GameRecovery.Pending ? "recover" : "undo"});Finish();}
            else if(c=="6") {PrintPresetStatus(GamePresets.Status(Selected()));Finish();}
            else throw new ArgumentException("choose 1..6 or 0");
        });
    }
    private static void GameActions(string game) {
        string style="balanced";bool personal=false;Device device=Selected();
        while(true) {
            string name=game+(style=="balanced" ? "" : "-"+style)+(personal ? "-personal" : "");
            Screen("game setup / "+game);GameRecipe recipe;
            try {recipe=GamePresets.Build(device,name);}
            catch(Exception e) {
                if(!personal) throw;
                Console.WriteLine("  "+Error(e)+"\n  returning to built-in range / nothing applied");personal=false;Finish();continue;
            }
            PrintRecipe(recipe);
            AimStatus current=Aim.Read(device);
            Console.WriteLine(current.State=="ready" ? "  currently / "+(current.Enabled==true ? "effects enabled" : "effects bypassed / apply enables this recipe") : "  driver / "+current.State+" / install through tune mouse > driver");
            if(current.State=="ready") Console.WriteLine("  this recipe / "+(GamePresets.IsCurrent(device,recipe) ? "matches live driver" : "differs from live driver"));
            Console.WriteLine("\n  1  apply this setup\n  2  change style\n  3  preview response checks\n  4  in-game checklist\n  5  built-in or personal speed range\n  6  health check\n  0  back");
            try {
                string action=Ask("choose");Device fresh=Selected();RequirePresetMouse(device.Path,fresh);
                if(action=="1" || action=="3") {PresetCommand(new string[]{"preset",action=="1" ? "apply" : "preview",name},fresh);Finish();}
                else if(action=="2") {
                    try {
                        Console.WriteLine("\n  1  balanced / bounded fast turns\n  2  steady / smaller ramp + gain stability\n  3  linear / flat 1x reference\n  0  keep current");
                        style=MapChoice(Ask("choose"),new string[]{"balanced","steady","linear"});if(style=="linear") personal=false;
                    }catch(OperationCanceledException) {}
                }else if(action=="4") {Screen("in-game checklist / manual");PrintGameSteps(GamePresets.Get(name));Finish();}
                else if(action=="5") {
                    try {
                        Console.WriteLine("\n  1  built-in range\n  2  use measured personal range\n  3  record two motion captures\n  0  keep current");string range=Ask("choose");
                        if(range=="1") personal=false;
                        else if(range=="2") {if(style=="linear") throw new ArgumentException("choose balanced or steady first");PresetTuning.Read(fresh);personal=true;}
                        else if(range=="3") TuneMovement();
                        else throw new ArgumentException("choose 1..3 or 0");
                    }catch(OperationCanceledException) {}
                }else if(action=="6") {HealthCheck();Finish();}
                else throw new ArgumentException("choose 1..6 or 0");
            }catch(OperationCanceledException) {return;}
            catch(Exception e) {Console.WriteLine("  "+Error(e));Finish();}
        }
    }
    internal static void RequirePresetMouse(string path,Device current) {
        if(current==null || !String.Equals(path,current.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / reopen game presets");
    }
    private static void PrintPresetStatus(GamePresetStatus status) {
        Console.WriteLine("  driver matches / "+(status.DriverMatches.Length==0 ? "custom setup / no exact recipe match" : String.Join(" + ",status.DriverMatches)));
        Console.WriteLine("  windows setup / "+(status.WindowsMatch ? "matches" : "different / apply sets pointer 10 and acceleration off"));
        Console.WriteLine("  checked now / game sensitivity and fov stay manual");
        if(status.LastAppliedRecipe!=null) Console.WriteLine("  last applied / "+status.LastAppliedRecipe+" / "+(status.LastAppliedMatches==true ? "selected mouse still matches" : status.LastAppliedMatches==false ? "changed since apply" : "not confirmed"));
    }
    private static void PrintRecipe(GameRecipe recipe) {
        Console.WriteLine("  style / "+recipe.Style+" / settled slow "+(recipe.MicroDamping ? "0.9" : "1")+"x / fast "+N(recipe.Curve.Limit)+"x");
        Console.WriteLine("  range / "+(recipe.Personal ? "measured" : "built-in starting point")+" / "+N(recipe.Curve.Start)+".."+N(recipe.Curve.End)+" counts/ms");
        Console.WriteLine("  gain stability / "+(recipe.Stability ? "4 ms" : "off")+" / micro damping "+(recipe.MicroDamping ? "0.9x" : "off"));
        Console.WriteLine("  smoothing + snap + direction reduction / off\n  dpi + game sens / kept / replaces current aim filters\n  windows pointer / 10 of 20 + accel off / desktop only");
        if(recipe.Personal) Console.WriteLine("  personal / repeat if physical dpi changes / stage is unknown");
    }
    private static void PrintGameSteps(GameRecipe recipe) {
        Console.WriteLine("  "+recipe.Game+" / "+recipe.Engine+"\n  "+recipe.InputPath+"\n");
        foreach(string step in recipe.GameSteps) Console.WriteLine("  - "+step);
        Console.WriteLine("\n  after applying / slow corrections, flick stop, then tracking\n  compare linear with your chosen style / same game sens and dpi\n  input gaps: test mouse > hz / frame-time or network issues: game telemetry\n  undo full setup / game setup > 5 / preset undo");
    }
    private static void PresetCommand(string[] words,Device selected=null) {
        if(words.Length==2 && words[1]=="tune") {TuneMovement();return;}
        if(words.Length==2 && words[1]=="status") {GamePresetStatus status=GamePresets.Status(Selected());if(json) Console.WriteLine(Store.Json.Serialize(status));else PrintPresetStatus(status);return;}
        if(words.Length==2 && words[1]=="recover") {GameRecovery.Restore();if(json) Console.WriteLine("{\"restored\":true}");else Console.WriteLine("  interrupted preset recovered / windows + driver + saved files verified");return;}
        if(words.Length==2 && words[1]=="list") {GameRecipe[] recipes=GamePresets.List();if(json) Console.WriteLine(Store.Json.Serialize(recipes));else foreach(GameRecipe entry in recipes) Console.WriteLine("  "+entry.Id+" / "+entry.Engine);return;}
        if(words.Length==2 && words[1]=="undo") {GamePresets.Undo();if(json) Console.WriteLine("{\"restored\":true}");else Console.WriteLine("  previous windows + driver settings restored / verified");return;}
        if(words.Length<3 || words.Length>5 || (words[1]!="show" && words[1]!="preview" && words[1]!="apply")) throw new ArgumentException("use preset list|status|tune|undo|recover or preset show|preview|apply <name> [balanced|steady|linear] [personal]");
        string name=words[2];bool hasStyle=false,hasPersonal=false;
        for(int i=3;i<words.Length;i++) {
            if(words[i]=="personal" && !hasPersonal) hasPersonal=true;
            else if((words[i]=="balanced" || words[i]=="steady" || words[i]=="linear") && !hasStyle) {hasStyle=true;if(words[i]!="balanced") name+="-"+words[i];}
            else throw new ArgumentException("use one style and optional personal");
        }
        if(hasPersonal) name+="-personal";
        GameRecipe recipe=GamePresets.Get(name);
        if(words[1]=="show") {if(recipe.Personal) recipe=GamePresets.Build(selected ?? Selected(),name);if(json) Console.WriteLine(Store.Json.Serialize(recipe));else {PrintRecipe(recipe);foreach(string decision in recipe.Decisions) Console.WriteLine("  "+decision);PrintGameSteps(recipe);}return;}
        Device device=selected ?? Selected();
        GamePresetPreview result=words[1]=="preview" ? GamePresets.Preview(device,recipe.Id) : GamePresets.Apply(device,recipe.Id);
        if(json) Console.WriteLine(Store.Json.Serialize(result));else {
            Console.WriteLine("  "+(result.Applied ? "applied / windows + driver readback verified / aim tools included" : "preview / nothing applied"));
            if(!result.Applied) {
                Console.WriteLine("  selected mouse stack / "+(result.Stack.RawAccelPresent==true && result.Stack.Started==true && result.Stack.ProblemCode==0 ? "driver started" : "not confirmed / apply blocked until checked"));
                Console.WriteLine("  official engine checks / "+(result.Assessment.ModelChecksPassed ? "passed" : "failed")+" / simulation");
                foreach(PresetTrial trial in result.Assessment.Trials) Console.WriteLine("  "+N(trial.IntervalMs)+" ms / small "+N(trial.SmallMotionRatio)+"x / fast "+N(trial.FastMotionRatio)+"x / tail "+trial.FlickTailPeakCounts+" counts / wrong-way "+trial.WrongWayReports);
            }
            Console.WriteLine("  in-game settings remain manual / preset show "+recipe.Id+"\n  undo both / preset undo");
        }
    }
    private static void DampingMenu() {
        Console.WriteLine("  micro damping / reduces all slow motion, including corrections\n  1 off    2 light 0.85x    3 balanced 0.75x    4 custom    0 back");string choice=Ask("choose");
        if(choice=="1") {AimCommand(new string[]{"aim","damp","off"});return;}
        if(choice!="2" && choice!="3" && choice!="4") throw new ArgumentException("choose 1..4 or 0");
        Device device=Selected();AimStatus status=Aim.Read(device);if(status.State!="ready") throw new InvalidOperationException(status.Note);
        AimDamping draft=new AimDamping {Enabled=true,LowScale=choice=="2" ? .85 : .75};
        if(choice=="4") {draft.LowScale=ReadNumber("low scale 0.25..1x",draft.LowScale);draft.RecoverySpeed=ReadNumber("full recovery 0.1..20 "+status.CurveSpeedUnit,draft.RecoverySpeed);}
        draft.Check();FilterDraft("damp",null,draft,status.CurveSpeedUnit,device.Path);
    }
    private static void FilterDraft(string feature,AimDirections directions,AimDamping damping,string unit,string expectedPath=null) {
        Device device=Selected();
        if(expectedPath!=null && !String.Equals(expectedPath,device.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / reopen filters");
        while(true) {
            Console.WriteLine("  draft / 1 preview    2 apply    0 back");string action=Ask("choose");
            Device fresh=Selected();if(!String.Equals(device.Path,fresh.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / reopen filters");
            if(action=="1") PrintFilterPreview(Aim.PreviewControls(fresh,feature,true,null,directions,damping,unit));
            else if(action=="2") {Applied(Aim.Set(fresh,feature,true,null,null,null,null,directions,damping,unit));return;}
            else throw new ArgumentException("choose 1..2 or 0");
        }
    }
    private static void PrintFilterPreview(AimResponse response,bool proposed=true) {
        Console.WriteLine(proposed ? "\n  filter preview / official engine / nothing applied" : "\n  live profile response / simulation / verified readback");PrintAim(response.Readback);
        Console.WriteLine("  1-count "+N(response.HorizontalSamples[0].OutputRatio)+"x / 8-count "+N(response.SmallMotionRatio)+"x / 800-count "+N(response.FastMotionRatio)+"x");
        Console.WriteLine("  example interval "+N(response.ProcessedIntervalMs)+" ms / "+response.IntervalSource);
        PrintDirections(response);
    }
    private static void Applied(AimStatus state) {
        if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  applied / driver readback verified");PrintAim(state);}
    }
    private static void PrintAim(AimStatus status) {
        if(GameRecovery.Pending) Console.WriteLine("  recovery pending / preset recover / close games first");
        Console.WriteLine("\n  aim / "+status.State);
        if(status.State=="ready") {
            Console.WriteLine("  enabled "+(status.Enabled==true ? "on" : "off")+" / curve "+(status.Mode=="lut" ? "custom" : status.Mode)+(status.Mode=="natural" ? " / limit "+status.GainLimit.Value.ToString(CultureInfo.InvariantCulture)+"x" : ""));
            if(status.Enabled!=true) Console.WriteLine("  effects bypassed / values below are configured only");
            if(status.Enabled==true && status.OutputHalfLifeMs>0) Console.WriteLine("  output averaging can magnify corrections after flicks / tune mouse > motion filters > remove flick tail");
            Console.WriteLine("  stability "+(status.StabilityEnabled==true ? "on" : "off")+" / input "+F(status.InputHalfLifeMs.Value)+" ms / scale "+F(status.ScaleHalfLifeMs.Value)+" ms\n  smooth "+(status.OutputHalfLifeMs>0 ? F(status.OutputHalfLifeMs.Value)+" ms" : "off")+" / output half-life");
            Console.WriteLine("  snap "+(status.SnapDegrees>0 ? F(status.SnapDegrees.Value)+" degrees" : "off"));
            if(status.Enabled==true && status.LookupInputSmoothingRisk==true) Console.WriteLine("  legacy lut speed smoothing / may suppress flick corrections / reapply curve or preset");
            if(status.Directions!=null && !status.Directions.Neutral) Console.WriteLine("  scales / left "+N(status.Directions.Left)+" / right "+N(status.Directions.Right)+" / up "+N(status.Directions.Up)+" / down "+N(status.Directions.Down));
            Console.WriteLine("  micro "+(status.DampingEnabled==null ? "unknown / unrecognized table" : status.DampingEnabled==true ? N(status.DampingLowScale.Value)+"x -> 1x at "+N(status.DampingRecoverySpeed.Value)+" "+status.CurveSpeedUnit : "off"));
            if(status.LookupData!=null && status.LookupData.Length>=4) Console.WriteLine(status.LookupIsSensitivity==true ? "  curve table / base "+N(status.LookupData[1])+"x / fast "+N(status.LookupData[status.LookupData.Length-1])+"x / speeds in "+status.CurveSpeedUnit : "  custom velocity table / not a helox sensitivity curve");
        }
        else Console.WriteLine("  "+status.Note);
    }
    private static void AimCommand(string[] words) {
        if(words.Length>=2 && words[1]=="verify") {
            if(words.Length==3 && words[2]=="restore") {LiveVerify.Restore();if(json) Console.WriteLine("{\"restored\":true}");else Console.WriteLine("  live verification snapshot restored / verified");return;}
            if(words.Length!=2) throw new ArgumentException("use aim verify or aim verify restore");
            LiveVerifyReport report=LiveVerify.Run(Selected(),json ? (Action<string>)null : delegate(string step){Console.WriteLine("  live write / "+step);});
            if(json) Console.WriteLine(Store.Json.Serialize(report));else {Console.WriteLine("  verified / "+report.Steps.Length+" real driver writes + full readback\n  original driver state restored / raw motion reports "+report.CapturedMotionReports+"\n  game input and physical transform not measured");if(report.RawInputError!=null) Console.WriteLine("  raw input check failed / "+report.RawInputError.ToLowerInvariant());}return;
        }
        if(words.Length==2 && words[1]=="events") {
            ShutdownReport report=ShutdownDiagnostics.Read();if(json) Console.WriteLine(Store.Json.Serialize(report));else PrintShutdown(report);return;
        }
        if(words.Length>=2 && words[1]=="curve") {CurveCommand(words);return;}
        if(words.Length==3 && words[1]=="bypass") {Applied(Aim.Bypass(Selected(),Toggle(words[2])!=0));return;}
        if(words.Length>=2 && words[1]=="directions") {
            if(words.Length==3 && words[2]=="off") {Applied(Aim.Set(Selected(),"directions",false));return;}
            if(words.Length!=7 || (words[2]!="apply" && words[2]!="preview")) throw new ArgumentException("use aim directions apply|preview <left> <right> <up> <down> or directions off / 0.25..1x");
            AimDirections draft=new AimDirections {Left=Number(words[3]),Right=Number(words[4]),Up=Number(words[5]),Down=Number(words[6])};draft.Check();
            if(words[2]=="preview") {AimResponse response=Aim.PreviewControls(Selected(),"directions",true,null,draft);if(json) Console.WriteLine(Store.Json.Serialize(new {applied=false,response=response}));else PrintFilterPreview(response);}
            else Applied(Aim.Set(Selected(),"directions",true,null,null,null,null,draft));return;
        }
        if(words.Length>=2 && words[1]=="damp") {
            if(words.Length!=3 && words.Length!=5) throw new ArgumentException("use aim damp on [low scale recovery speed]|off or damp preview <low scale> <recovery speed>");
            bool preview=words[2]=="preview",on=preview || Toggle(words[2])!=0;
            if(preview && words.Length!=5 || !on && words.Length!=3) throw new ArgumentException("micro strengths require on or preview");
            AimDamping draft=words.Length==5 ? new AimDamping {Enabled=true,LowScale=Number(words[3]),RecoverySpeed=Number(words[4])} : null;if(draft!=null) draft.Check();
            if(preview) {AimResponse response=Aim.PreviewControls(Selected(),"damp",true,null,null,draft);if(json) Console.WriteLine(Store.Json.Serialize(new {applied=false,response=response}));else PrintFilterPreview(response);}
            else Applied(Aim.Set(Selected(),"damp",on,null,null,null,null,null,draft));return;
        }
        if((words.Length==3 || words.Length==4) && words[1]=="snap") {
            bool on=Toggle(words[2])!=0;double? angle=words.Length==4 ? (double?)Number(words[3]) : null;
            if(angle.HasValue && !on) throw new ArgumentException("angle requires aim snap on");
            if(angle.HasValue) Aim.CheckSnap(angle.Value);
            AimStatus state=Aim.Set(Selected(),"snap",on,null,null,null,angle);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  applied / driver readback verified");PrintAim(state);}return;
        }
        if(words.Length==2 && words[1]=="response") {
            AimResponse result=Aim.Response(Selected());
            if(json) Console.WriteLine(Store.Json.Serialize(result));
            else {
                Console.WriteLine("\n  response / current profile / simulation / read only\n  example interval "+F(result.ExampleIntervalMs)+" ms / processed "+F(result.ProcessedIntervalMs)+" ms\n  "+result.IntervalSource);
                Console.WriteLine("  after "+result.WarmupReports+" reports / 8-count "+result.SmallMotionRatio.ToString("0.000",CultureInfo.InvariantCulture)+"x / 800-count "+result.FastMotionRatio.ToString("0.000",CultureInfo.InvariantCulture)+"x\n  flick -> 1-count turn / peak "+result.AfterFlickPeakCounts+" counts / zero outputs "+result.AfterFlickZeroReports+" of 16");
                Console.WriteLine("  flick -> reverse / peak "+result.ReversalPeakCounts+" counts / wrong way "+result.ReversalWrongWayReports+" / zero outputs "+result.ReversalZeroReports+" of 16");
                List<string> mid=new List<string>();
                foreach(AimResponsePoint point in result.HorizontalSamples) if(point.InputCounts==24 || point.InputCounts==80 || point.InputCounts==160) mid.Add(point.InputCounts+": "+point.OutputRatio.ToString("0.000",CultureInfo.InvariantCulture)+"x");
                Console.WriteLine("  horizontal counts/report / "+String.Join(" / ",mid.ToArray()));
                PrintDirections(result);
                if(result.Readback.Enabled!=true) Console.WriteLine("  selected device bypassed / no driver effect");
                else if(result.Readback.OutputHalfLifeMs>0) Console.WriteLine("  output averaging active / tracking preset disables it");
                Console.WriteLine("  example motion / not a test inside your game");
            }return;
        }
        if(words.Length==2 && words[1]=="tracking") {
            AimStatus state=Aim.Set(Selected(),"tracking",true);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  tracking preset applied / driver readback verified");PrintAim(state);}return;
        }
        if(words.Length==2 && words[1]=="doctor") {
            Dictionary<string,object> report=Maintenance.Doctor();
            DeviceStackReport stack=DeviceStack.Read(OptionalSelected());report["SelectedDeviceStack"]=stack;
            if(json) Console.WriteLine(Store.Json.Serialize(report));
            else {
                Console.WriteLine("\n  driver check / read only");
                foreach(string key in new string[]{"BackendPrepared","PackageVerified","BackendVerified","ServiceRegistered","ServiceDeletionPending","FilterRegistered","DriverFilePresent","DriverMatchesPackage","PendingFilePresent","EndpointPresent","KernelReadable"})
                    Console.WriteLine("  "+DoctorLabel(key)+" / "+(report[key]==null ? "unknown" : (bool)report[key] ? "yes" : "no"));
                Console.WriteLine("  signature / "+(report["DriverSignature"] ?? "not available").ToString().ToLowerInvariant()+"\n  protocol / "+(report["KernelVersion"] ?? "not available"));
                if(report["KernelError"]!=null) Console.WriteLine("  readback / "+report["KernelError"].ToString().ToLowerInvariant());
                PrintStack(stack);
                foreach(object error in (object[])report["Errors"]) Console.WriteLine("  "+error.ToString().ToLowerInvariant());
                Console.WriteLine("  after install or uninstall / restart windows, then check again");
            }return;
        }
        if(words.Length==2 && words[1]=="uninstall") {
            if(json) throw new ArgumentException("driver removal does not support json");
            Maintenance.Uninstall();return;
        }
        if(words.Length==2 && words[1]=="resume") {
            AimStatus state=Aim.Set(Selected(),"resume",false);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  saved aim resumed / driver readback verified");PrintAim(state);}return;
        }
        if(words.Length==2 && words[1]=="status") {AimStatus state=Aim.Read(OptionalSelected());if(json) Console.WriteLine(Store.Json.Serialize(state));else PrintAim(state);return;}
        if(words.Length==2 && words[1]=="restore") {Aim.Restore();if(json) Console.WriteLine("{\"restored\":true}");else Console.WriteLine("  aim restored / driver readback verified");return;}
        if(words.Length==2 && (words[1]=="install" || words[1]=="prepare")) {
            if(json) throw new ArgumentException("backend setup does not support json");
            System.Diagnostics.ProcessStartInfo start=Maintenance.Start("install-aim.ps1",words[1]=="prepare" ? "-PrepareOnly" : "");
            start.UseShellExecute=false;
            using(System.Diagnostics.Process process=System.Diagnostics.Process.Start(start)) {process.WaitForExit();if(process.ExitCode!=0) throw new InvalidOperationException("aim backend setup failed / see message above");}
            return;
        }
        if((words.Length==3 || words.Length==4) && (words[1]=="precision" || words[1]=="smooth" || words[1]=="stability")) {
            bool enabled=Toggle(words[2])!=0;double? strength=null,gain=null;
            if(words.Length==4) {
                if(!enabled || words[1]=="stability") throw new ArgumentException("use aim precision on <1.1..1.8x> or aim smooth on <1..12 ms>");
                if(words[1]=="precision") gain=double.Parse(words[3].Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture);else strength=Integer(words[3]);
            }
            Aim.CheckChange(words[1],enabled,strength,gain);
            AimStatus state=Aim.Set(Selected(),words[1],enabled,strength,gain);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  applied / driver readback verified");PrintAim(state);}return;
        }
        throw new ArgumentException("use aim status|doctor|events|response|tracking|curve|directions|prepare|install|uninstall|restore|resume or aim precision|smooth|stability|snap|damp|bypass on|off");
    }
    private static void PrintDirections(AimResponse response) {
        if(response.DirectionSamples==null) return;List<string> parts=new List<string>();
        foreach(AimDirectionPoint point in response.DirectionSamples) if(point.Name!="diagonal" && point.Name!="near horizontal") parts.Add(point.Name+" "+N(point.OutputRatio)+"x");
        Console.WriteLine("  8-count directions / "+String.Join(" / ",parts.ToArray()));
    }
    private static double Number(string value) {return double.Parse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture);}
    private static string N(double value) {return value.ToString("0.###",CultureInfo.InvariantCulture);}
    private static double ReadNumber(string label,double current) {
        Console.Write("  "+label+" ["+N(current)+"] / enter keeps / back cancels > ");string value=Console.ReadLine();
        if(value==null || value.Trim().Equals("back",StringComparison.OrdinalIgnoreCase)) throw new OperationCanceledException();
        return String.IsNullOrWhiteSpace(value) ? current : Number(value.Trim());
    }
    private static void PrintCurve(AimCurve curve,string unit) {
        Console.WriteLine("\n  curve draft / speeds in "+unit+"\n  1  base "+N(curve.Base)+"x     2  start "+N(curve.Start)+"\n  3  end "+N(curve.End)+"       4  fast / base "+N(curve.Limit)+"x\n  5  shape "+N(curve.Shape)+" / <1 earlier, >1 later\n  slow "+N(curve.Base)+"x -> fast "+N(curve.Base*curve.Limit)+"x\n  6  preview     7  apply     8  natural curve\n  0  back");
    }
    private static void PrintCurvePreview(AimCurve curve,AimResponse response) {
        Console.WriteLine("\n  curve preview / official engine / nothing applied");
        Console.WriteLine("  base curve "+N(curve.Base)+"x -> "+N(curve.Base*curve.Limit)+"x / transition "+N(curve.Start)+".."+N(curve.End)+" "+response.Readback.CurveSpeedUnit);
        foreach(AimResponsePoint point in response.HorizontalSamples) Console.WriteLine("  "+point.InputCounts+" counts/report / "+N(point.OutputRatio)+"x");
        Console.WriteLine("  example "+N(response.ProcessedIntervalMs)+" ms / "+response.IntervalSource+"\n  curve speed unit "+response.Readback.CurveSpeedUnit+" / output smoothing "+N(response.Readback.OutputHalfLifeMs.Value)+" ms\n  flick -> micro / peak "+response.AfterFlickPeakCounts+" counts / "+response.AfterFlickZeroReports+" zero outputs");
    }
    private static void CurveBuilder() {
        Device device=Selected();AimCurve draft=Aim.Draft(device);AimStatus status=Aim.Read(device);
        if(status.State!="ready") throw new InvalidOperationException(status.Note);
        while(true) {
            PrintCurve(draft,status.CurveSpeedUnit);string choice;
            try {choice=Ask("choose");}catch(OperationCanceledException) {return;}
            try {
                if(choice=="6" || choice=="7" || choice=="8") {
                    Device fresh=Selected();RequireCurveContext(device.Path,status.CurveSpeedUnit,fresh,Aim.Read(fresh));
                    if(choice=="6") PrintCurvePreview(draft,Aim.PreviewCurve(fresh,draft,status.CurveSpeedUnit));
                    else if(choice=="7") {AimStatus applied=Aim.Set(fresh,"curve",true,null,null,draft,null,null,null,status.CurveSpeedUnit);Console.WriteLine("  curve applied / driver readback verified");PrintAim(applied);}
                    else PrintAim(Aim.Set(fresh,"curve",true,null,null,null,null,null,null,status.CurveSpeedUnit));
                    continue;
                }
                AimCurve next=draft.Copy();
                switch(choice) {
                    case "1":next.Base=ReadNumber("base 0.25..2x",draft.Base);break;
                    case "2":next.Start=ReadNumber("start 0..1000",draft.Start);break;
                    case "3":next.End=ReadNumber("end > start / max 2000",draft.End);break;
                    case "4":next.Limit=ReadNumber("fast / base 1..3x",draft.Limit);break;
                    case "5":next.Shape=ReadNumber("shape 0.5..3",draft.Shape);break;
                    default:throw new ArgumentException("choose 1..8 or 0");
                }
                next.Check();draft=next;
            }catch(OperationCanceledException) {}catch(Exception e) {Console.WriteLine("  "+Error(e));}
        }
    }
    internal static void RequireCurveContext(string path,string unit,Device current,AimStatus status) {
        if(current==null || !String.Equals(path,current.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / reopen curve builder");
        if(status.State!="ready") throw new InvalidOperationException(status.Note);
        if(unit!=status.CurveSpeedUnit) throw new InvalidOperationException("curve speed unit changed / reopen curve builder");
    }
    private static void CurveCommand(string[] words) {
        if(words.Length==3 && words[2]=="natural") {
            AimStatus state=Aim.Set(Selected(),"curve",true);if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else PrintAim(state);return;
        }
        if(words.Length==2 && !json) {CurveBuilder();return;}
        if(words.Length!=8 || (words[2]!="preview" && words[2]!="apply")) throw new ArgumentException("use aim curve preview|apply <base> <start> <end> <limit> <shape> or aim curve natural");
        AimCurve curve=new AimCurve {Base=Number(words[3]),Start=Number(words[4]),End=Number(words[5]),Limit=Number(words[6]),Shape=Number(words[7])};curve.Check();
        if(words[2]=="preview") {
            AimResponse response=Aim.PreviewCurve(Selected(),curve);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=false,curve=curve.ToMap(),response=response}));else PrintCurvePreview(curve,response);
        }else {
            AimStatus state=Aim.Set(Selected(),"curve",true,null,null,curve);
            if(json) Console.WriteLine(Store.Json.Serialize(new {applied=true,readback=state}));else {Console.WriteLine("  curve applied / driver readback verified");PrintAim(state);}
        }
    }
    private static void PrintShutdown(ShutdownReport report) {
        Console.WriteLine("\n  shutdown errors / windows journal / read only\n  last request "+(report.LastShutdownUtc ?? "unknown")+"\n  helox / raw accel application crashes: "+report.AimApplicationCrashes.Length);
        List<ApplicationCrash> displayed=new List<ApplicationCrash>();
        foreach(ApplicationCrash crash in report.AimApplicationCrashes) if(displayed.Count<8) displayed.Add(crash);
        foreach(ApplicationCrash crash in report.RecentApplicationCrashes) if(displayed.Count<8 && !crash.RelatedToAim) displayed.Add(crash);
        foreach(ApplicationCrash crash in displayed) Console.WriteLine("  "+crash.Utc+" / "+(crash.Application ?? "unknown").ToLowerInvariant()+" / "+(crash.ExceptionCode ?? "unknown")+(crash.NearShutdown==true ? " / within 2 min of shutdown" : crash.NearShutdown==null ? " / shutdown timing unknown" : ""));
        foreach(string error in report.Errors) Console.WriteLine("  "+error);
        if(report.ScanLimitReached) Console.WriteLine("  scan limit reached / results incomplete");
        Console.WriteLine("  14-day crash history / timing does not prove cause\n  a popup may be unlogged; kernel failures are not covered");
    }
    private static string DoctorLabel(string key) {
        switch(key) {
            case "BackendPrepared":return "backend files";case "PackageVerified":return "official archive verified";
            case "BackendVerified":return "backend matches archive";case "ServiceRegistered":return "service registered";
            case "ServiceDeletionPending":return "service deletion pending";
            case "FilterRegistered":return "mouse filter registered";case "DriverFilePresent":return "driver file";
            case "DriverMatchesPackage":return "driver matches archive";case "PendingFilePresent":return "pending driver file";
            case "EndpointPresent":return "driver endpoint open";default:return "kernel readback";
        }
    }
    private static void ConfirmUninstall() {
        Console.WriteLine("  removes the shared raw accel driver for all mice\n  profiles and backups stay / restart required");
        if(Ask("type uninstall / 0 back")!="uninstall") throw new OperationCanceledException();
        AimCommand(new string[]{"aim","uninstall"});
    }
    private static void Finish() {
        if(Console.IsInputRedirected || Console.IsOutputRedirected) return;
        Console.Write("\n  enter / back ");
        while(true) {ConsoleKey key=Console.ReadKey(true).Key;if(key==ConsoleKey.Enter || key==ConsoleKey.Escape) break;}
    }
    private static void Help() {
        Console.WriteLine("\n  preset list / status / show|preview|apply <name> / undo / recover\n  aim verify             temporary real driver writes + restore\n  aim verify restore     recover an interrupted verification");
        Console.WriteLine("\n  aim curve              curve builder / preview then apply\n  aim curve preview|apply <base> <start> <end> <limit> <shape>\n  aim curve natural      return to natural acceleration\n  aim snap on [0..5] / off   axis direction filter / default off\n  aim directions preview|apply <left> <right> <up> <down> / off\n  aim damp on [low scale recovery speed] / off\n  aim damp preview <low scale> <recovery speed>\n  aim bypass on|off       bypass all / enable current profile\n  aim events             recent application crashes and shutdown timing");
        Console.WriteLine("\n  aim prepare / install / status / doctor / uninstall / restore / resume\n  aim precision on [1.1..1.8] / off   gradual fast-motion gain limit\n  aim stability on|off   steadier acceleration / precision required\n  aim tracking           precision + stability / output smoothing off\n  aim response           current-profile simulation / read only\n  aim smooth on [1..12] / off   output half-life ms / adds lag\n  check                  analysis checks / no settings changes");
        Console.WriteLine("\n  setup                  speed 10/20 + windows accel on\n  set acceleration on|off\n  set speed 1..20\n  set wheel 0..100|page\n  set doubleclick 200..900\n  set swap on|off\n  measure 3..30          observed input hz\n  dpi                    three-pass check, no ruler\n  calibrate <cm>         known-distance dpi estimate\n  profile save|show|apply <name>\n  profile list / undo / restore\n  cleanup --confirm      reset data + shared driver\n  devices / select <index> / probe\n  health / status / home / faq / exit\n  --mouse <index>        one-command selection\n\n  export: launch.bat status --json");
    }
    private static Device Selected() {
        List<Device> devices=Device.List();
        string requested=commandMousePath ?? selectedPath;
        if(requested!=null) {
            foreach(Device d in devices) if(String.Equals(d.Path,requested,StringComparison.OrdinalIgnoreCase)) return d;
            throw new InvalidOperationException("selected mouse disconnected; choose mouse again");
        }
        return DefaultMouse(devices);
    }
    private static void HealthCheck() {
        Device device=null;string selection=null;try {device=Selected();}catch(InvalidOperationException e) {selection=e.Message;}
        HealthReport report=Health.Read(device,selection);
        if(json) {Console.WriteLine(Store.Json.Serialize(report));return;}
        Console.WriteLine("\n  health / read only / "+report.CheckedUtc+"\n  mouse / "+(device==null ? "not selected" : (device.Product ?? "mouse device").ToLowerInvariant()));
        Console.WriteLine("  backend files / "+(report.Driver!=null && Maintenance.BackendVerified(report.Driver) ? "verified" : "not verified"));
        Console.WriteLine("  aim / "+report.Aim.State+(report.Aim.State=="ready" ? " / "+(report.Aim.Enabled==true ? "enabled" : "bypassed")+" / "+report.Aim.Mode : ""));
        Console.WriteLine("  mouse stack / "+(report.Stack.RawAccelPresent==true && report.Stack.Started==true && report.Stack.ProblemCode==0 ? "raw accel started" : "not confirmed"));
        if(report.Presets!=null) Console.WriteLine("  recipe / "+(report.Presets.DriverMatches.Length>0 ? String.Join(" + ",report.Presets.DriverMatches) : "custom setup"));
        foreach(ServiceReading reading in report.Vanguard) Console.WriteLine("  "+reading.Name+" / "+reading.State+(reading.ErrorCode.HasValue ? " / error "+reading.ErrorCode.Value : ""));
        foreach(string note in report.Notes) Console.WriteLine("  note / "+note.ToLowerInvariant());
        foreach(string error in report.Errors) Console.WriteLine("  error / "+error.ToLowerInvariant());
    }
    internal static Device DefaultMouse(List<Device> devices) {
        if(devices.Count==1) return devices[0];
        List<Device> candidates=devices.FindAll(delegate(Device d){return d.TrustCandidate;});
        if(candidates.Count==1) return candidates[0];
        throw new InvalidOperationException(devices.Count==0 ? "no mouse found / connect a mouse" : "multiple mice / choose mouse in 6 > 1");
    }
    private static Device OptionalSelected() {
        try {return Selected();}catch(InvalidOperationException) {return null;}
    }
    internal static Device ResolveChoice(List<Device> displayed,int number,List<Device> connected) {
        if(number<1 || number>displayed.Count) throw new ArgumentException("mouse number out of range");
        string path=displayed[number-1].Path;
        foreach(Device device in connected) if(String.Equals(device.Path,path,StringComparison.OrdinalIgnoreCase)) return device;
        throw new InvalidOperationException("mouse disconnected; choose mouse again");
    }
    private static void ChooseMouse() {
        List<Device> devices=Device.List();
        if(devices.Count==0) throw new InvalidOperationException("no mice detected");
        for(int i=0;i<devices.Count;i++) Console.WriteLine("  "+(i+1)+"  "+(devices[i].Product ?? "mouse device").ToLowerInvariant()+(devices[i].TrustCandidate ? " / trust" : ""));
        Device chosen=ResolveChoice(devices,Integer(Ask("mouse number / 0 back")),Device.List());
        selectedPath=chosen.Path;
        Console.WriteLine("  selected / "+(chosen.Product ?? "mouse device").ToLowerInvariant());
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
            selectedDeviceStack=DeviceStack.Read(d),
            hardwareControl="unsupported: no verified vendor protocol",gameAcceleration=Aim.Read(d),
            lastDpi=Last<DpiResult>("dpi.json",d),lastRate=Last<RateResult>("rate.json",d)};
    }
    private static T Last<T>(string filename,Device d) where T:class {
        string path=Path.Combine(Store.Root,filename);if(!File.Exists(path) || d==null) return null;
        try {
            T result=Store.Load<T>(path);DpiResult dpi=result as DpiResult;RateResult rate=result as RateResult;
            if((dpi!=null && !Analysis.ValidHistory(dpi)) || (rate!=null && !Analysis.ValidHistory(rate))) return null;
            string device=dpi!=null ? dpi.DevicePath : rate!=null ? rate.DevicePath : null;
            return String.Equals(device,d.Path,StringComparison.OrdinalIgnoreCase) ? result : null;
        }catch {return null;}
    }
    private static void CompactStatus() {
        Header();Device d=null;
        try {d=Selected();Console.WriteLine("  mouse     "+(d.Product ?? "mouse device").ToLowerInvariant());}
        catch {Console.WriteLine("  mouse     not selected / more > choose mouse");}
        Settings s=Settings.Read();
        Console.WriteLine("  windows   speed "+s.Speed+"/20 / accel "+(s.Acceleration==0 ? "off" : "on")+" / desktop");
        if(GameRecovery.Pending) Console.WriteLine("  recovery  game preset interrupted / game setup > 5");
        AimStatus aim=Aim.Read(d);
        Console.WriteLine(aim.State=="ready" ? "  aim       "+(aim.Mode=="lut" ? "custom curve" : aim.Mode=="noaccel" ? "linear" : "acceleration")+" / "+(aim.Enabled==true ? "enabled" : "bypassed")+" / smooth "+(aim.OutputHalfLifeMs>0 ? F(aim.OutputHalfLifeMs.Value)+" ms" : "off") : "  aim       "+aim.State+" / tune mouse > driver");
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
        PrintAim(Aim.Read(device));
        PrintStack(DeviceStack.Read(device));
        Console.WriteLine("\n  windows / live\n  wheel           "+(settings.WheelLines==-1 ? "page" : settings.WheelLines+" lines")+" / doubleclick "+settings.DoubleClickMs+" ms\n  buttons         "+(settings.SwapButtons==0 ? "normal" : "swapped"));
        DpiResult lastDpi=Last<DpiResult>("dpi.json",device);RateResult lastRate=Last<RateResult>("rate.json",device);
        if(lastDpi!=null) Console.WriteLine("\n  dpi history / "+lastDpi.MeasuredUtc+" / "+lastDpi.Trials+" pass(es)"+(lastDpi.SpreadPercent.HasValue ? " / spread "+F(lastDpi.SpreadPercent.Value)+"%" : ""));
        if(lastRate!=null) Console.WriteLine("  hz history / "+lastRate.MeasuredUtc+" / "+lastRate.Reports+" reports / "+lastRate.Quality);
        Console.WriteLine("\n  unavailable / sensor dpi, configured hz, battery, link power\n  dpi check / uses mouse body length; estimate, not readback");
    }
    private static string Known(bool? value) {return value.HasValue ? (value.Value ? "yes" : "no") : "unknown";}
    private static void PrintStack(DeviceStackReport stack) {
        if(stack==null) {Console.WriteLine("  mouse stack / choose a connected mouse to check");return;}
        Console.WriteLine("  mouse stack / raw accel listed "+Known(stack.RawAccelPresent)+" / started "+Known(stack.Started));
        if(stack.Services!=null && stack.Services.Length>0) {
            List<string> names=new List<string>();
            foreach(string service in stack.Services) names.Add(service.StartsWith(@"\Driver\",StringComparison.OrdinalIgnoreCase) ? service.Substring(8) : service);
            Console.WriteLine("  "+String.Join(" > ",names.ToArray()).ToLowerInvariant());
        }
        if(stack.ProblemCode>0) Console.WriteLine("  pnp problem / "+stack.ProblemCode);
        if(stack.Error!=null) Console.WriteLine("  stack / "+stack.Error);
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
        CheckDpiInput(device);
        if(facts==null) throw new ArgumentException("mouse-body dpi check supports gxt 929 only / use calibrate <cm>");
        Console.WriteLine("\n  dpi check / no ruler or marks / approximate\n  keep one fingertip beside the mouse front edge\n  slide forward until the rear edge reaches that finger\n  keep the finger still; do not rotate or lift\n  repeat 3 times / mouse length 125 mm");
        List<DpiResult> trials=new List<DpiResult>();
        for(int pass=1;pass<=3;pass++) {
            StartPass("pass "+pass+"/3 / position mouse and finger");
            Device fresh=Selected();
            if(!String.Equals(fresh.Path,device.Path,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("mouse changed / restart check");
            CheckDpiInput(fresh);
            using(RawCapture capture=new RawCapture(fresh)) {
                Console.WriteLine("  slide one body length / enter finishes");capture.Collect(45,true);
                CheckDpiInput(fresh);
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
        RateResult previous=Last<RateResult>("rate.json",d);
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(seconds,false);RateResult result=Analysis.Rate(capture.Samples);result.DevicePath=d.Path;
            result.Comparison=Analysis.CompareRates(previous,result);
            Store.Save(Path.Combine(Store.Root,"rate.json"),result);
            if(json) Console.WriteLine(Store.Json.Serialize(result));
            else {
                Console.WriteLine("  ~"+F(result.ActiveHz)+" hz / observed input\n  p95 "+F(result.P95IntervalMs)+" ms / p99 "+F(result.P99IntervalMs.Value)+" ms\n  slow intervals "+result.SlowIntervals+" / long gaps "+result.IdleGaps+" / max "+F(result.MaxGapMs.Value)+" ms\n  "+result.Quality);
                Console.WriteLine("  including gaps ~"+F(result.DeliveredHz.Value)+" hz / gap time "+F(result.GapPercent.Value)+"%\n  slow threshold "+F(result.SlowThresholdMs.Value)+" ms / heuristic, not lost-packet count");
                PrintComparison(result.Comparison);
                if(result.IdleGaps>0 || result.SlowIntervals>0) Console.WriteLine("  repeat without stopping / place receiver near mouse, away from usb 3 hubs\n  smooth reduces movement fluctuations; it cannot recover missing reports");
                Console.WriteLine("  home / return to menu");
            }
        }
    }
    private static string Signed(double value) {return value.ToString("+0.0;-0.0;0.0",CultureInfo.InvariantCulture);}
    private static void PrintComparison(RateComparison comparison) {
        if(comparison==null) return;
        Console.WriteLine("  vs previous test / hz "+Signed(comparison.ActiveHzDifference)+" / p95 "+Signed(comparison.P95IntervalDifferenceMs)+" ms"+
            (comparison.P99IntervalDifferenceMs.HasValue ? " / p99 "+Signed(comparison.P99IntervalDifferenceMs.Value)+" ms" : ""));
        List<string> shares=new List<string>();
        if(comparison.GapPercentDifference.HasValue) shares.Add("gap time "+Signed(comparison.GapPercentDifference.Value)+" pp");
        if(comparison.SlowIntervalPercentDifference.HasValue) shares.Add("slow intervals "+Signed(comparison.SlowIntervalPercentDifference.Value)+" pp");
        if(shares.Count>0) Console.WriteLine("  "+String.Join(" / ",shares.ToArray())+" / percentage points");
        if(comparison.ContextNote!=null) Console.WriteLine("  "+comparison.ContextNote);
    }
    private static void CheckDpiInput(Device device) {RequireDpiInput(Aim.Read(device).InputTransformed);}
    internal static void RequireDpiInput(bool? transformed) {
        if(transformed!=false) throw new InvalidOperationException("aim filter active or unreadable / verify aim status and restore before checking dpi / nothing saved");
    }
    private static void Calibrate(string[] words) {
        if(words.Length!=2 || json || Console.IsInputRedirected) throw new ArgumentException("use calibrate <cm> in a terminal");
        double cm=Analysis.Distance(words[1]);Device d=Selected();
        CheckDpiInput(d);
        Console.WriteLine("\n  mark "+F(cm)+" cm on pad; place mouse at first mark\n  press enter, move straight to second mark, press enter\n  do not lift or return / esc cancels");
        StartPass("ready");
        CheckDpiInput(d);
        using(RawCapture capture=new RawCapture(d)) {
            capture.Collect(60,true);DpiResult result=Analysis.Dpi(capture.Samples,cm);result.DevicePath=d.Path;
            CheckDpiInput(d);
            Store.Save(Path.Combine(Store.Root,"dpi.json"),result);
            Console.WriteLine("  ~"+F(result.EstimatedDpi)+" dpi / estimate\n  home / return to menu");
        }
    }
    private static void Profile(string[] words) {
        if(words.Length==2 && words[1]=="list") {
            List<string> names=Store.Profiles();
            if(json) Console.WriteLine(Store.Json.Serialize(names));else Console.WriteLine("  profiles / "+(names.Count==0 ? "none yet" : String.Join(" / ",names.ToArray())));
        }else if(words.Length==3 && words[1]=="save") {
            Store.SaveProfile(words[2]);if(json) Console.WriteLine(Store.Json.Serialize(new {saved=words[2]}));else Console.WriteLine("  saved / "+words[2]);
        }else if(words.Length==3 && (words[1]=="apply" || words[1]=="show")) {
            string path=Store.Profile(words[2]);Settings settings;
            try {settings=Store.Load<Settings>(path);}
            catch(FileNotFoundException) {throw new ArgumentException("profile not found / use profile list");}
            catch(DirectoryNotFoundException) {throw new ArgumentException("profile not found / use profile list");}
            if(words[1]=="show") {
                if(json) Console.WriteLine(Store.Json.Serialize(settings));
                else {
                    string[] changes=settings.PreviewChanges(Settings.Read());
                    Console.WriteLine("  profile / "+words[2]);
                    if(changes.Length==0) Console.WriteLine("  already matches / no changes");
                    else {
                        Console.WriteLine("  current -> saved");
                        foreach(string change in changes) Console.WriteLine("  "+change);
                        Console.WriteLine("  preview only / nothing applied");
                    }
                }
            }else Apply(settings);
        }
        else throw new ArgumentException("use profile save|show|apply <name> or profile list");
    }
    private static void Faq() {
        Console.WriteLine("\n  game acceleration?\n  2 tune mouse > 3 driver / install signed raw accel driver / restart once.\n  precision: slow corrections 1x / choose fast-motion limit 1.2, 1.4 or 1.6x.\n  2 > 1 > 1 curve builder / preview first, then apply.\n\n  mouse jerks?\n  3 > 2 test hz / gaps. 2 > 1 > 4 stability averages acceleration changes.\n  2 > 2 output smoothing averages movement magnitude, adding lag.\n  for wireless gaps: receiver close to mouse, away from usb 3 hubs.\n\n  dpi / hz show ?\n  hardware values cannot be read yet. use the physical dpi button.\n  dpi estimate requires aim filters off, including snap.\n\n  shutdown popup?\n  6 > 7 or aim events / inspect application crashes and timing.\n\n  undo settings?\n  2 > 3 > 4 undo aim restores previous driver settings for all devices.\n  5 > 4 undoes the last windows change; 5 > 3 restores original.\n  aim resets on reboot; 2 > 3 > 3 resume saved restores your preset.\n\n  home / return to menu");
    }
    private static void Run(string[] words) {
        if(words[0]=="cleanup") {
            if(json || words.Length!=2 || words[1]!="--confirm") throw new ArgumentException("use cleanup --confirm / restores windows backup, removes shared driver and all helox data");
            Maintenance.Reset();return;
        }
        if(json && (words[0]=="help" || words[0]=="faq" || words[0]=="home" || words[0]=="clear" || words[0]=="selftest" || words[0]=="check"))
            throw new ArgumentException("json is not supported for this command");
        switch(words[0].ToLowerInvariant()) {
            case "maintenance-validate":if(words.Length!=1) break;Maintenance.ValidateReset();Console.WriteLine("  reset backups validated / no settings changed");return;
            case "maintenance-restore":if(words.Length!=1) break;Run(new string[]{"restore"});return;
            case "maintenance-aim-restore":
                if(words.Length!=1) break;
                bool? endpoint=Aim.EndpointState();
                if(endpoint==null) throw new InvalidOperationException("driver endpoint unreadable / data kept");
                if(endpoint==true) {Aim.Restore();Console.WriteLine("  original aim snapshot restored / verified");}
                else Console.WriteLine("  driver not loaded / aim backup not applied");return;
            case "aim":AimCommand(words);return;
            case "status":if(words.Length!=1) break;Status();return;
            case "health":if(words.Length!=1) break;HealthCheck();return;
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
            case "preset":PresetCommand(words);return;
            case "undo":
                if(words.Length!=1) break;Store.Undo();
                if(json) Console.WriteLine(Store.Json.Serialize(new {undone=true,readback=Settings.Read()}));else Console.WriteLine("  last windows change undone / verified");return;
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
            case "selftest":if(words.Length!=1) break;GameRecovery.RequireNoRecovery();SelfTest.Run();return;
            case "check":if(words.Length!=1) break;SelfTest.Run(false);return;
        }throw new ArgumentException("unknown choice / type home or help");
    }
}
}
