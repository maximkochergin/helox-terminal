using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Helox {
public sealed class Device {
    public string Path {get;set;}
    public string Product {get;set;}
    public string Manufacturer {get;set;}
    public bool TrustCandidate {get;set;}
    public int? DriverButtonCount {get;set;}
    public int? DriverSampleRate {get;set;}
    public bool? HorizontalWheel {get;set;}
    internal IntPtr Handle;
    internal static List<Device> List() {
        uint count=0, size=(uint)Marshal.SizeOf(typeof(Native.DeviceEntry));
        if(Native.GetRawInputDeviceList(IntPtr.Zero,ref count,size)==uint.MaxValue) Native.Check(false);
        for(int attempt=0;attempt<3;attempt++) {
            IntPtr p=Marshal.AllocHGlobal(checked((int)(Math.Max(count,1)*size)));
            try {
                uint found=Native.GetRawInputDeviceList(p,ref count,size);
                if(found==uint.MaxValue) { if(attempt==2) Native.Check(false); continue; }
                List<Device> result=new List<Device>();
                for(int i=0;i<found;i++) {
                    Native.DeviceEntry entry=(Native.DeviceEntry)Marshal.PtrToStructure(IntPtr.Add(p,i*(int)size),typeof(Native.DeviceEntry));
                    if(entry.Type!=0) continue;
                    uint chars=0;
                    if(Native.GetRawInputDeviceInfo(entry.Handle,0x20000007,IntPtr.Zero,ref chars)==uint.MaxValue) continue;
                    IntPtr name=Marshal.AllocHGlobal(checked((int)(chars+1)*2));
                    try {
                        if(Native.GetRawInputDeviceInfo(entry.Handle,0x20000007,name,ref chars)==uint.MaxValue) continue;
                        string path=Marshal.PtrToStringUni(name);
                        Device mouse=new Device { Handle=entry.Handle, Path=path, Product=Native.HidString(path,true),
                            Manufacturer=Native.HidString(path,false), TrustCandidate=path.IndexOf("vid_145f&pid_0326",StringComparison.OrdinalIgnoreCase)>=0 };
                        uint infoSize=32;IntPtr info=Marshal.AllocHGlobal(32);
                        try {
                            Marshal.WriteInt32(info,32);
                            if(Native.GetRawInputDeviceInfo(entry.Handle,0x2000000b,info,ref infoSize)!=uint.MaxValue && Marshal.ReadInt32(info,4)==0) {
                                mouse.DriverButtonCount=Marshal.ReadInt32(info,12);mouse.DriverSampleRate=Marshal.ReadInt32(info,16);mouse.HorizontalWheel=Marshal.ReadInt32(info,20)!=0;
                            }
                        }finally {Marshal.FreeHGlobal(info);}
                        result.Add(mouse);
                    } finally { Marshal.FreeHGlobal(name); }
                }
                result.Sort(delegate(Device a,Device b){return String.Compare(a.Path,b.Path,StringComparison.OrdinalIgnoreCase);});
                return result;
            } finally { Marshal.FreeHGlobal(p); }
        }
        throw new InvalidOperationException("device list changed; reconnect and try again");
    }
}
public sealed class Sample {
    public double Ms; public int X,Y;
    public Sample(double ms,int x,int y) {Ms=ms;X=x;Y=y;}
}
public sealed class RateResult {
    public double? SpanMs {get;set;}
    public double? GapDurationMs {get;set;}
    public double? DeliveredHz {get;set;}
    public double? GapPercent {get;set;}
    public double? SlowThresholdMs {get;set;}
    public RateComparison Comparison {get;set;}
    public int Reports {get;set;}
    public int Intervals {get;set;}
    public int IdleGaps {get;set;}
    public double MedianIntervalMs {get;set;}
    public double P95IntervalMs {get;set;}
    public double? P99IntervalMs {get;set;}
    public double? MaxGapMs {get;set;}
    public int? SlowIntervals {get;set;}
    public int? SameTimestampReports {get;set;}
    public double ObservedHz {get;set;}
    public double ActiveHz {get;set;}
    public double MedianHz {get;set;}
    public string Source {get;set;}
    public string DevicePath {get;set;}
    public string MeasuredUtc {get;set;}
    public string Quality {get;set;}
}
public sealed class RateComparison {
    public string PreviousMeasuredUtc {get;set;}
    public double ActiveHzDifference {get;set;}
    public double P95IntervalDifferenceMs {get;set;}
}
public sealed class DpiResult {
    public double EstimatedDpi {get;set;}
    public double DistanceCm {get;set;}
    public long Counts {get;set;}
    public string DevicePath {get;set;}
    public string MeasuredUtc {get;set;}
    public string Source {get;set;}
    public int Trials {get;set;}
    public double? SpreadPercent {get;set;}
    public List<double> TrialDpi {get;set;}
}
public static class Analysis {
    private static bool Positive(double value) {return value>0 && !double.IsNaN(value) && !double.IsInfinity(value);}
    private static bool Finite(double value) {return !double.IsNaN(value) && !double.IsInfinity(value);}
    private static bool Timestamp(string value) {
        DateTime parsed;return ReadTimestamp(value,out parsed);
    }
    private static bool ReadTimestamp(string value,out DateTime parsed) {
        return DateTime.TryParseExact(value,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed) && parsed.Kind!=DateTimeKind.Unspecified;
    }
    internal static bool ValidHistory(RateResult result) {
        return result!=null && result.Reports>=101 && result.Intervals>=100 && result.Intervals<=result.Reports-1 && result.IdleGaps>=0 &&
            Positive(result.ActiveHz) && Positive(result.MedianIntervalMs) && Positive(result.P95IntervalMs) && result.P95IntervalMs>=result.MedianIntervalMs && Timestamp(result.MeasuredUtc) &&
            (!result.P99IntervalMs.HasValue || (Positive(result.P99IntervalMs.Value) && result.P99IntervalMs.Value>=result.P95IntervalMs)) &&
            (!result.MaxGapMs.HasValue || (Finite(result.MaxGapMs.Value) && result.MaxGapMs.Value>=0)) &&
            (!result.SlowIntervals.HasValue || (result.SlowIntervals.Value>=0 && result.SlowIntervals.Value<=result.Intervals)) &&
            (!result.SameTimestampReports.HasValue || (result.SameTimestampReports.Value>=0 && result.SameTimestampReports.Value<=result.Intervals)) &&
            (!result.SpanMs.HasValue || Positive(result.SpanMs.Value)) &&
            (!result.GapDurationMs.HasValue || (Finite(result.GapDurationMs.Value) && result.GapDurationMs.Value>=0 && (!result.SpanMs.HasValue || result.GapDurationMs.Value<=result.SpanMs.Value))) &&
            (!result.DeliveredHz.HasValue || Positive(result.DeliveredHz.Value)) &&
            (!result.GapPercent.HasValue || (Finite(result.GapPercent.Value) && result.GapPercent.Value>=0 && result.GapPercent.Value<=100)) &&
            (!result.SlowThresholdMs.HasValue || Positive(result.SlowThresholdMs.Value)) &&
            (result.Comparison==null || (Timestamp(result.Comparison.PreviousMeasuredUtc) && Finite(result.Comparison.ActiveHzDifference) && Finite(result.Comparison.P95IntervalDifferenceMs)));
    }
    internal static bool ValidHistory(DpiResult result) {
        return result!=null && Positive(result.EstimatedDpi) && result.Counts>=100 && result.DistanceCm>=2 && result.DistanceCm<=100 && result.Trials>=1 && Timestamp(result.MeasuredUtc) &&
            (!result.SpreadPercent.HasValue || (Finite(result.SpreadPercent.Value) && result.SpreadPercent.Value>=0 && result.SpreadPercent.Value<=15)) &&
            (result.TrialDpi==null || (result.TrialDpi.Count==result.Trials && result.TrialDpi.TrueForAll(Positive)));
    }
    internal static RateComparison CompareRates(RateResult previous,RateResult current) {
        if(!ValidHistory(previous) || !ValidHistory(current) || String.IsNullOrEmpty(current.DevicePath) ||
            !String.Equals(previous.DevicePath,current.DevicePath,StringComparison.OrdinalIgnoreCase)) return null;
        DateTime before,after;ReadTimestamp(previous.MeasuredUtc,out before);ReadTimestamp(current.MeasuredUtc,out after);
        if(before.ToUniversalTime()>after.ToUniversalTime()) return null;
        return new RateComparison {PreviousMeasuredUtc=previous.MeasuredUtc,ActiveHzDifference=current.ActiveHz-previous.ActiveHz,
            P95IntervalDifferenceMs=current.P95IntervalMs-previous.P95IntervalMs};
    }
    public static DpiResult CombineDpi(List<DpiResult> trials,string source) {
        if(trials==null || trials.Count<3) throw new InvalidOperationException("complete all three passes / nothing saved");
        List<double> values=new List<double>();
        foreach(DpiResult trial in trials) {
            if(trial==null || trial.EstimatedDpi<=0 || double.IsNaN(trial.EstimatedDpi) || double.IsInfinity(trial.EstimatedDpi)) throw new InvalidOperationException("invalid dpi pass");
            values.Add(trial.EstimatedDpi);
        }
        values.Sort();double median=Percentile(values,.5),spread=(values[values.Count-1]-values[0])*100/median;
        if(spread>15) throw new InvalidOperationException("passes differ by more than 15% / repeat check");
        return new DpiResult {EstimatedDpi=median,DistanceCm=trials[0].DistanceCm,Counts=(long)Math.Round(median*trials[0].DistanceCm/2.54),Trials=trials.Count,
            SpreadPercent=spread,TrialDpi=values,Source=source,MeasuredUtc=DateTime.UtcNow.ToString("o")};
    }
    public static double Distance(string value) {
        double cm=double.Parse(value.Replace(',','.'),NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
        ValidateDistance(cm);return cm;
    }
    private static void ValidateDistance(double cm) {
        if(double.IsNaN(cm) || double.IsInfinity(cm) || cm<2 || cm>100) throw new ArgumentException("distance: 2..100 cm");
    }
    public static double Percentile(List<double> sorted,double p) {
        if(sorted.Count==0) throw new ArgumentException("no samples");
        double index=(sorted.Count-1)*p; int lo=(int)index, hi=(int)Math.Ceiling(index);
        return sorted[lo]+(sorted[hi]-sorted[lo])*(index-lo);
    }
    public static RateResult Rate(List<Sample> samples) {
        List<double> times=new List<double>(); int gaps=0,batched=0; double total=0,maxGap=0,gapDuration=0;
        foreach(Sample sample in samples)
            if(sample==null || double.IsNaN(sample.Ms) || double.IsInfinity(sample.Ms))
                throw new InvalidOperationException("invalid capture timestamps / repeat test");
        for(int i=1;i<samples.Count;i++) {
            double delta=samples[i].Ms-samples[i-1].Ms;
            maxGap=Math.Max(maxGap,delta);
            if(delta>50) {gaps++;gapDuration+=delta;continue;}
            if(delta<0) throw new InvalidOperationException("capture timestamps out of order / repeat test");
            if(delta==0) {batched++;continue;}
            times.Add(delta); total+=delta;
        }
        if(times.Count<100 || total<250) throw new InvalidOperationException("not enough sustained motion / repeat test");
        times.Sort(); double median=Percentile(times,.5);
        double slowThreshold=Math.Max(median*1.75,median+.25);
        int slow=times.FindAll(delegate(double time){return time>slowThreshold;}).Count;
        double medianHz=1000/median, activeHz=1000*(times.Count+batched)/total;
        double span=total+gapDuration;
        return new RateResult { Reports=samples.Count, Intervals=times.Count+batched, IdleGaps=gaps, MedianIntervalMs=median,
            SpanMs=span,GapDurationMs=gapDuration,DeliveredHz=1000*(samples.Count-1)/span,GapPercent=100*gapDuration/span,SlowThresholdMs=slowThreshold,
            P95IntervalMs=Percentile(times,.95),P99IntervalMs=Percentile(times,.99),MaxGapMs=maxGap,SlowIntervals=slow,SameTimestampReports=batched,
            ObservedHz=activeHz, ActiveHz=activeHz, MedianHz=medianHz,
            Quality=gaps>0 ? "long gaps / pauses or delivery interruption; repeat with continuous motion" : slow>0 || batched>0 || Math.Abs(medianHz-activeHz)/activeHz>.2 ? "uneven delivery / repeat test" : "consistent delivery",
            Source="observed raw input delivery; not configured usb polling rate", MeasuredUtc=DateTime.UtcNow.ToString("o") };
    }
    public static DpiResult Dpi(List<Sample> samples,double cm) {
        ValidateDistance(cm);
        long x=0,y=0,pathX=0,pathY=0;
        foreach(Sample s in samples) { x+=s.X; y+=s.Y; pathX+=Math.Abs((long)s.X); pathY+=Math.Abs((long)s.Y); }
        long dominant=Math.Max(Math.Abs(x),Math.Abs(y)), transverse=Math.Min(Math.Abs(x),Math.Abs(y));
        long path=Math.Abs(x)>=Math.Abs(y) ? pathX : pathY;
        if(dominant<100) throw new InvalidOperationException("not enough motion; calibration was not saved");
        long offAxis=Math.Abs(x)>=Math.Abs(y) ? pathY : pathX;
        if(transverse>dominant*.2 || offAxis>path*.25 || path>dominant*1.15) throw new InvalidOperationException("use one straight stroke without returning or lifting; calibration was not saved");
        return new DpiResult { EstimatedDpi=dominant*2.54/cm, DistanceCm=cm, Counts=dominant,
            Trials=1,Source="distance calibration estimate; not hardware readback", MeasuredUtc=DateTime.UtcNow.ToString("o") };
    }
}
internal sealed class RawCapture : NativeWindow, IDisposable {
    internal readonly List<Sample> Samples=new List<Sample>();
    internal int AbsoluteReports;
    private IntPtr device;
    private Stopwatch watch=new Stopwatch();
    private Exception failure;
    private bool disposed;
    private bool collecting;
    private double deadline;
    internal RawCapture(Device selected) {
        device=selected.Handle;
        CreateParams cp=new CreateParams(); cp.Caption="helox raw input sink"; cp.Parent=new IntPtr(-3); CreateHandle(cp);
        Native.RawRegistration r=new Native.RawRegistration {Page=1,Usage=2,Flags=0x2100,Target=Handle};
        try { Native.Check(Native.RegisterRawInputDevices(new Native.RawRegistration[]{r},1,(uint)Marshal.SizeOf(typeof(Native.RawRegistration)))); watch.Start(); }
        catch { DestroyHandle(); throw; }
    }
    protected override void WndProc(ref Message message) {
        if(message.Msg==0xfe && message.WParam==new IntPtr(2) && message.LParam==device)
            failure=new InvalidOperationException("selected mouse disconnected");
        if(message.Msg==0xff && failure==null && collecting && watch.Elapsed.TotalMilliseconds<=deadline) {
            try {
                uint size=0, header=(uint)(IntPtr.Size==8 ? 24 : 16);
                if(Native.GetRawInputData(message.LParam,0x10000003,IntPtr.Zero,ref size,header)==uint.MaxValue) Native.Check(false);
                if(size>=header+24) {
                    IntPtr p=Marshal.AllocHGlobal((int)size);
                    try {
                        if(Native.GetRawInputData(message.LParam,0x10000003,p,ref size,header)==uint.MaxValue) Native.Check(false);
                        if(Marshal.ReadInt32(p)==0 && Marshal.ReadIntPtr(p,8)==device) {
                            int flags=Marshal.ReadInt16(p,(int)header);
                            if((flags&1)!=0) AbsoluteReports++;
                            else {
                                int x=Marshal.ReadInt32(p,(int)header+12),y=Marshal.ReadInt32(p,(int)header+16);
                                if(x!=0 || y!=0) Samples.Add(new Sample(watch.Elapsed.TotalMilliseconds,x,y));
                            }
                        }
                    } finally {Marshal.FreeHGlobal(p);}
                }
            } catch(Exception e) {failure=e;}
        }
        base.WndProc(ref message);
    }
    internal void Collect(int seconds,bool enterStops) {
        bool completed=!enterStops;
        Samples.Clear();AbsoluteReports=0;watch.Restart();
        deadline=seconds*1000.0;collecting=true;
        try {
        using(ConsoleCaptureMode mode=new ConsoleCaptureMode()) {
        while(watch.Elapsed.TotalSeconds<seconds) {
            Application.DoEvents();
            if(failure!=null) throw failure;
            if(!Console.IsInputRedirected && Console.KeyAvailable) {
                ConsoleKey key=Console.ReadKey(true).Key;
                if(key==ConsoleKey.Escape) throw new OperationCanceledException("measurement cancelled");
                if(enterStops && key==ConsoleKey.Enter) {completed=true;break;}
            }
            // Wake on real input instead of a sleep-based polling loop which batches reports.
            uint wait=Native.MsgWaitForMultipleObjectsEx(0,IntPtr.Zero,10,0x04ff,4);
            if(wait==uint.MaxValue) Native.Check(false);
        }
        collecting=false;
        Application.DoEvents();
        if(failure!=null) throw failure;
        if(!completed) throw new InvalidOperationException("calibration timed out / nothing saved");
        if(AbsoluteReports>0) throw new InvalidOperationException("absolute pointer reports cannot be used for this measurement");
        }
        }finally {collecting=false;}
    }
    public void Dispose() {
        if(disposed) return;disposed=true;
        Native.RawRegistration r=new Native.RawRegistration {Page=1,Usage=2,Flags=1,Target=IntPtr.Zero};
        Native.RegisterRawInputDevices(new Native.RawRegistration[]{r},1,(uint)Marshal.SizeOf(typeof(Native.RawRegistration)));
        DestroyHandle();
    }
}
}
