using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Helox {
public sealed class LiveVerifyStep {
    public string Name {get;set;}
    public bool KernelReadbackMatched {get;set;}
    public double SmallRatio {get;set;}
    public double FastRatio {get;set;}
    public AimStatus Readback {get;set;}
}
public sealed class LiveVerifyReport {
    public LiveVerifyStep[] Steps {get;set;}
    public bool OriginalDriverStateRestored {get;set;}
    public bool RawInputSinkRegistered {get;set;}
    public int CapturedMotionReports {get;set;}
    public string RawInputError {get;set;}
    public bool? PhysicalTransformVerified {get;set;}
    public bool? GameInputVerified {get;set;}
    public string Source {get;set;}
}
internal static class LiveVerify {
    internal static readonly string RecoveryPath=Path.Combine(Store.Root,"verify-recovery.json");
    internal static void RequireNoRecovery() {GameRecovery.RequireNoRecovery();if(File.Exists(RecoveryPath)) throw new InvalidOperationException("unfinished live verification snapshot / aim verify restore first");}
    internal static void RequireClosedGames() {
        foreach(string name in new string[]{"VALORANT","VALORANT-Win64-Shipping","FPSAimTrainer","FPSAimTrainer-Win64-Shipping","cs2"}) {
            Process[] processes=Process.GetProcessesByName(name);bool running=processes.Length>0;foreach(Process p in processes) p.Dispose();
            if(running) throw new InvalidOperationException("close games before temporary live verification / "+name.ToLowerInvariant());
        }
    }
    internal static List<KeyValuePair<string,Dictionary<string,object>>> Cases(Dictionary<string,object> before,Dictionary<string,object> defaults,string id) {
        List<KeyValuePair<string,Dictionary<string,object>>> cases=new List<KeyValuePair<string,Dictionary<string,object>>>();
        foreach(string name in new string[]{"precision","smooth","stability","curve","snap","directions","micro","bypass"}) {
            Dictionary<string,object> cfg=Aim.Configure(before,defaults,id,name=="precision" || name=="stability" || name=="curve" || name=="micro",name=="smooth",2,1.4,name=="stability",8,true,
                name=="curve" ? new AimCurve() : null,name=="snap" ? 1 : 0,name=="directions" ? new AimDirections {Left=.8,Right=1,Up=.9,Down=.9} : null,name=="micro" ? new AimDamping {Enabled=true,LowScale=.85,RecoverySpeed=1} : null);
            if(name=="bypass") cfg=Aim.ConfigureBypass(Aim.Configure(before,defaults,id,true,true,2,1.4,true,8,true,new AimCurve(),1,new AimDirections {Left=.8},new AimDamping {Enabled=true}),id,true);
            cases.Add(new KeyValuePair<string,Dictionary<string,object>>(name,cfg));
        }
        foreach(GameRecipe recipe in GamePresets.List()) cases.Add(new KeyValuePair<string,Dictionary<string,object>>(recipe.Id,GamePresets.Configure(before,defaults,id,recipe)));
        return cases;
    }
    internal static LiveVerifyReport Run(Device device,Action<string> progress) {
        RequireClosedGames();DeviceStackReport stack=DeviceStack.Read(device);
        if(stack.RawAccelPresent!=true || stack.Started!=true || stack.ProblemCode!=0) throw new InvalidOperationException("selected mouse's started raw accel stack must be confirmed / aim doctor");
        return Aim.Locked(delegate {
            RequireNoRecovery();
            Dictionary<string,object> before=Aim.Active(),defaults=Aim.Defaults();string id=Aim.Id(device);
            List<KeyValuePair<string,Dictionary<string,object>>> cases=Cases(before,defaults,id);
            // Validate and calculate every request before the first real write.
            foreach(KeyValuePair<string,Dictionary<string,object>> item in cases) AimResponseTest.Run(item.Value,id,8);
            Store.Save(RecoveryPath,before);List<LiveVerifyStep> results=new List<LiveVerifyStep>();Exception failure=null;
            try {
                foreach(KeyValuePair<string,Dictionary<string,object>> item in cases) {
                    RequireClosedGames();if(Aim.Id(device)!=id) throw new InvalidOperationException("mouse identity changed during verification");
                    if(progress!=null) progress(item.Key);
                    Dictionary<string,object> live=Aim.Write(item.Value);AimResponse response=AimResponseTest.Run(live,id,8);
                    if(item.Key=="micro") Aim.DescribeDamping(response.Readback,new AimPreset {Precision=true,Damping=new AimDamping {Enabled=true,LowScale=.85,RecoverySpeed=1}});
                    else if(item.Key=="curve") Aim.DescribeDamping(response.Readback,new AimPreset {Precision=true,Curve=new AimCurve()});
                    else if(item.Key=="bypass") Aim.DescribeDamping(response.Readback,new AimPreset {Precision=true,Curve=new AimCurve(),Damping=new AimDamping {Enabled=true}});
                    else if(item.Key=="valorant" || item.Key=="cs2" || item.Key.StartsWith("kovaaks-",StringComparison.Ordinal)) Aim.DescribeDamping(response.Readback,GamePresets.Get(item.Key).Controls());
                    results.Add(new LiveVerifyStep {Name=item.Key,KernelReadbackMatched=true,SmallRatio=response.SmallMotionRatio,FastRatio=response.FastMotionRatio,Readback=response.Readback});
                }
            }catch(Exception error) {failure=error;}finally {
                // Keep durable recovery data if activation or readback fails during restore.
                try {
                    Dictionary<string,object> restored=Aim.Write(before);
                    if(!Aim.SameValue(before,restored)) throw new IOException("live verify restore mismatch");File.Delete(RecoveryPath);
                }catch(Exception rollback) {throw new IOException("live verify "+(failure==null ? "restore failed" : "failed: "+failure.Message)+"; restore failed: "+rollback.Message+" / recovery snapshot retained");}
            }
            if(failure!=null) throw new IOException("live verify failed: "+failure.Message+"; original driver state restored");
            LiveVerifyReport report=new LiveVerifyReport {Steps=results.ToArray(),OriginalDriverStateRestored=true,Source="real kernel activation + delayed full readback / ratios from the released engine / no input injection; game and physical transform checks remain unmeasured"};
            try {using(RawCapture capture=new RawCapture(device)) {report.RawInputSinkRegistered=true;capture.Collect(3,false);report.CapturedMotionReports=capture.Samples.Count;}}
            catch(Exception error) {report.RawInputError=error.Message;}
            return report;
        });
    }
    internal static void Restore() {
        RequireClosedGames();Aim.Locked(delegate {
            if(!File.Exists(RecoveryPath)) throw new InvalidOperationException("no live verification snapshot");
            Dictionary<string,object> before=Aim.Parse(File.ReadAllText(RecoveryPath));Aim.Validate(before);
            Aim.Write(before);File.Delete(RecoveryPath);return true;
        });
    }
}
}
