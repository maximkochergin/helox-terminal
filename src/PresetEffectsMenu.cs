using System;

namespace Helox {
internal static partial class Program {
    private static GameEffects EffectsMenu(string game,string style,bool personal,Device device,GameEffects effects) {
        GameEffects draft=effects.Copy();
        while(true) {
            Screen("game effects / draft / nothing applied");
            PrintRecipe(GamePresets.Build(device,game+(style=="balanced" ? "" : "-"+style)+(personal ? "-personal" : ""),draft));
            Console.WriteLine("\n  1  output smoothing\n  2  gain stability\n  3  micro damping\n  4  angle snapping\n  5  direction scales\n  6  reset effects to this style's defaults\n  0  keep draft and back");
            string choice;try {choice=Ask("choose");}catch(OperationCanceledException) {return draft;}
            try {
                GameEffects next=draft.Copy();
                if(choice=="1") {
                    Console.WriteLine("  averages output magnitude / adds delay and may amplify post-flick corrections\n  1 style default    2 off    3 light 1 ms    4 light 2 ms    5 custom");
                    string c=Ask("choose");
                    if(c=="1") next.SmoothMs=null;else if(c=="2") next.SmoothMs=0;else if(c=="3") next.SmoothMs=1;else if(c=="4") next.SmoothMs=2;else if(c=="5") next.SmoothMs=ReadNumber("output half-life 1..12 ms",next.SmoothMs>0 ? next.SmoothMs.Value : 2);else throw new ArgumentException("choose 1..5 or 0");
                }else if(choice=="2") {
                    Console.WriteLine("  averages the acceleration coefficient / input LUT averaging stays off\n  1 style default    2 off    3 on / scale half-life 4 ms");
                    string c=Ask("choose");if(c=="1") next.Stability=null;else if(c=="2") next.Stability=false;else if(c=="3") next.Stability=true;else throw new ArgumentException("choose 1..3 or 0");
                }else if(choice=="3") {
                    Console.WriteLine("  reduces all slow motion, including intended corrections\n  1 style default    2 off    3 light 0.95x    4 0.9x    5 custom");
                    string c=Ask("choose");next.MicroRecovery=null;
                    if(c=="1") next.MicroScale=null;else if(c=="2") next.MicroScale=1;else if(c=="3") next.MicroScale=.95;else if(c=="4") next.MicroScale=.9;
                    else if(c=="5") {next.MicroScale=ReadNumber("low scale 0.25..<1x",.9);next.MicroRecovery=ReadNumber("recovery speed 0.1..20 counts/ms",.75);}else throw new ArgumentException("choose 1..5 or 0");
                }else if(choice=="4") {
                    Console.WriteLine("  bends near-axis movement / optional, not riot certified\n  1 style default    2 off    3 light 1 degree    4 custom");
                    string c=Ask("choose");if(c=="1") next.SnapDegrees=null;else if(c=="2") next.SnapDegrees=0;else if(c=="3") next.SnapDegrees=1;else if(c=="4") next.SnapDegrees=ReadNumber("snap 0..5 degrees",1);else throw new ArgumentException("choose 1..4 or 0");
                }else if(choice=="5") {
                    Console.WriteLine("  reduces sensitivity by direction / changes your low-speed response too\n  1 style default    2 neutral 1x    3 vertical 0.85x    4 custom");
                    string c=Ask("choose");
                    if(c=="1") next.Directions=null;else if(c=="2") next.Directions=new AimDirections();else if(c=="3") next.Directions=new AimDirections {Up=.85,Down=.85};
                    else if(c=="4") {AimDirections axes=next.Directions==null ? new AimDirections() : next.Directions.Copy();axes.Left=ReadNumber("left 0.25..1x",axes.Left);axes.Right=ReadNumber("right 0.25..1x",axes.Right);axes.Up=ReadNumber("up 0.25..1x",axes.Up);axes.Down=ReadNumber("down 0.25..1x",axes.Down);next.Directions=axes;}else throw new ArgumentException("choose 1..4 or 0");
                }else if(choice=="6") next=new GameEffects();
                else throw new ArgumentException("choose 1..6 or 0");
                next.Check();draft=next;
            }catch(OperationCanceledException) {}catch(Exception e) {Console.WriteLine("  "+Error(e));Finish();}
        }
    }
    private static void PrintEffects(AimPreset controls) {
        Console.WriteLine("  output smoothing / "+(controls.Smooth ? N(controls.SmoothMs)+" ms" : "off")+" / gain stability "+(controls.Stability ? N(controls.StabilityMs/2)+" ms" : "off"));
        Console.WriteLine("  micro damping / "+(controls.Damping.Active ? N(controls.Damping.LowScale)+"x below "+N(controls.Damping.RecoverySpeed)+" counts/ms" : "off"));
        Console.WriteLine("  snap / "+N(controls.SnapDegrees)+" degrees / direction scales "+(controls.Directions.Neutral ? "neutral 1x" : "left "+N(controls.Directions.Left)+" / right "+N(controls.Directions.Right)+" / up "+N(controls.Directions.Up)+" / down "+N(controls.Directions.Down)));
    }
    private static void PrintPresetResult(GamePresetPreview result) {
        Console.WriteLine("  "+(result.Applied ? "applied / windows + driver readback verified / aim tools included" : "preview / nothing applied"));
        if(!result.Applied) {
            Console.WriteLine("  selected mouse stack / "+(result.Stack.RawAccelPresent==true && result.Stack.Started==true && result.Stack.ProblemCode==0 ? "driver started" : "not confirmed / apply blocked until checked"));
            Console.WriteLine("  processing checks / "+(result.Assessment.ProcessingChecksPassed ? "passed" : "failed")+" / official engine simulation");
            for(int i=0;i<result.Assessment.Trials.Length;i++) {
                PresetTrial trial=result.Assessment.Trials[i],basis=result.BaseAssessment.Trials[i];
                Console.WriteLine("  "+N(trial.IntervalMs)+" ms / small "+N(trial.SmallMotionRatio)+"x / fast "+N(trial.FastMotionRatio)+"x / tail base "+basis.FlickTailPeakCounts+" -> draft "+trial.FlickTailPeakCounts+" counts");
            }
            Console.WriteLine("  current setup / 8 ms example tail "+result.CurrentResponse.AfterFlickPeakCounts+" -> draft "+result.Response.AfterFlickPeakCounts+" counts");
        }
        foreach(string warning in result.Assessment.Warnings) Console.WriteLine("  review / "+warning);
        Console.WriteLine("  in-game settings remain manual / preset show "+result.Recipe.Id+"\n  undo both / preset undo");
    }
}
}
