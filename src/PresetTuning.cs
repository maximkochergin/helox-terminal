using System;
using System.Collections.Generic;
using System.IO;

namespace Helox {
public sealed class MovementTuning {
    public string DevicePath {get;set;}
    public string MeasuredUtc {get;set;}
    public double SlowP90 {get;set;}
    public double FastP75 {get;set;}
    public double IntervalMs {get;set;}
    public int SlowReports {get;set;}
    public int FastReports {get;set;}
    public string Source {get;set;}
}
internal static class PresetTuning {
    internal static readonly string PathName=Path.Combine(Store.Root,"game-tuning.json");
    private static List<double> Speeds(List<Sample> samples,out int reports) {
        if(samples==null || samples.Count==0) throw new ArgumentException("invalid movement samples");
        List<double> speeds=new List<double>();double active=0,anchor=0,distance=0;int skipped=0,pending=0;reports=0;
        for(int i=0;i<samples.Count;i++) {
            Sample b=samples[i];
            if(b==null || !Finite(b.Ms) || b.Ms<0 || (i>0 && b.Ms<samples[i-1].Ms))
                throw new ArgumentException("invalid movement samples");
            if(i==0) {anchor=b.Ms;continue;}
            if(b.Ms-samples[i-1].Ms>35) {skipped++;anchor=b.Ms;distance=0;pending=0;continue;}
            // Keep sub-ms and batched reports; measure path length over at least 1 ms.
            // Summing magnitudes also keeps opposite movements from cancelling each other.
            distance+=Math.Sqrt((double)b.X*b.X+(double)b.Y*b.Y);if(b.X!=0 || b.Y!=0) pending++;
            double elapsed=b.Ms-anchor;if(elapsed<1) continue;
            if(distance>0) {speeds.Add(distance/elapsed);active+=elapsed;reports+=pending;}
            anchor=b.Ms;distance=0;pending=0;
        }
        if(speeds.Count<100 || active<1500 || skipped>samples.Count*.3) throw new InvalidOperationException("not enough clean continuous motion / repeat without lifting the mouse");
        speeds.Sort();return speeds;
    }
    internal static MovementTuning Analyze(string device,List<Sample> slow,List<Sample> fast) {
        int slowReports,fastReports;List<double> s=Speeds(slow,out slowReports),f=Speeds(fast,out fastReports);
        MovementTuning result=new MovementTuning {DevicePath=device,MeasuredUtc=DateTime.UtcNow.ToString("o"),SlowP90=Analysis.Percentile(s,.9),FastP75=Analysis.Percentile(f,.75),
            IntervalMs=(Analysis.Rate(slow).MedianIntervalMs+Analysis.Rate(fast).MedianIntervalMs)/2,SlowReports=slowReports,FastReports=fastReports,
            Source="two manual raw input captures / untransformed input / >=1 ms path windows / counts per ms / not game telemetry"};
        Validate(result,device,DateTime.UtcNow);return result;
    }
    internal static void Validate(MovementTuning value,string device,DateTime now) {
        DateTime measured;
        if(value==null || String.IsNullOrWhiteSpace(value.DevicePath) || !String.Equals(value.DevicePath,device,StringComparison.OrdinalIgnoreCase) ||
            !DateTime.TryParse(value.MeasuredUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out measured) ||
            measured.ToUniversalTime()>now.AddMinutes(1) || measured.ToUniversalTime()<now.AddDays(-7) || value.SlowReports<100 || value.FastReports<100 ||
            !Finite(value.SlowP90) || !Finite(value.FastP75) || !Finite(value.IntervalMs) || value.SlowP90<.01 || value.SlowP90>800 || value.FastP75>1800 ||
            value.FastP75<value.SlowP90*1.5 || value.IntervalMs<=0 || value.IntervalMs>35)
            throw new InvalidOperationException("movement tuning missing, stale or unclear / preset tune / keep the same physical dpi stage");
    }
    private static bool Finite(double value) {return !Double.IsNaN(value) && !Double.IsInfinity(value);}
    internal static MovementTuning Read(Device device) {
        if(!File.Exists(PathName)) throw new InvalidOperationException("no movement tuning / preset tune first");
        Dictionary<string,object> map=Aim.Parse(File.ReadAllText(PathName));object value;
        if(!map.TryGetValue(device.Path,out value)) throw new InvalidOperationException("this mouse has no movement tuning / preset tune first");
        MovementTuning result=Store.Json.Deserialize<MovementTuning>(Store.Json.Serialize(value));Validate(result,device.Path,DateTime.UtcNow);return result;
    }
    internal static void Save(MovementTuning value) {
        Validate(value,value.DevicePath,DateTime.UtcNow);
        Store.Locked(delegate {
            Dictionary<string,object> map=File.Exists(PathName) ? Aim.Parse(File.ReadAllText(PathName)) : new Dictionary<string,object>();
            map[value.DevicePath]=value;Store.Save(PathName,map);
        });
    }
}
public sealed class PresetTrial {
    public double IntervalMs {get;set;}
    public double SmallMotionRatio {get;set;}
    public double FastMotionRatio {get;set;}
    public long FlickTailPeakCounts {get;set;}
    public int WrongWayReports {get;set;}
}
public sealed class PresetAssessment {
    public PresetTrial[] Trials {get;set;}
    public bool ModelChecksPassed {get;set;}
    public bool? GameInputVerified {get;set;}
    public string Source {get;set;}
    internal static PresetAssessment Run(Dictionary<string,object> cfg,string id) {
        List<PresetTrial> trials=new List<PresetTrial>();bool passed=true;
        foreach(double interval in new double[]{8,4,2,1}) {
            AimResponse response=AimResponseTest.Run(cfg,id,interval);
            trials.Add(new PresetTrial {IntervalMs=interval,SmallMotionRatio=response.SmallMotionRatio,FastMotionRatio=response.FastMotionRatio,FlickTailPeakCounts=response.AfterFlickPeakCounts,WrongWayReports=response.ReversalWrongWayReports});
            if(response.ReversalWrongWayReports!=0 || response.AfterFlickPeakCounts>3) passed=false;
        }
        return new PresetAssessment {Trials=trials.ToArray(),ModelChecksPassed=passed,Source="official engine model at 125 / 250 / 500 / 1000 hz intervals / not measured hardware rates or game results"};
    }
}
}
