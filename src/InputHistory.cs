using System;
using System.Collections.Generic;
using System.Globalization;

namespace Helox {
public sealed class InputHistoryReport {
    public bool Available {get;set;}
    public bool Recent {get;set;}
    public double? AgeHours {get;set;}
    public RateResult Rate {get;set;}
    public string[] Notes {get;set;}
    public string Source {get;set;}
}
internal static partial class Program {
    internal static InputHistoryReport History(RateResult rate,string devicePath,DateTime now) {
        if(!Analysis.ValidHistory(rate) || String.IsNullOrWhiteSpace(devicePath) || !String.Equals(rate.DevicePath,devicePath,StringComparison.OrdinalIgnoreCase)) return new InputHistoryReport {Available=false,Notes=new string[]{"no valid test for this mouse / run measure 10"},Source="saved delivery history / no measurement or setting change"};
        DateTime measured=DateTime.ParseExact(rate.MeasuredUtc,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToUniversalTime();
        List<string> notes=new List<string>();bool recent=Aim.RecentRate(rate,devicePath,now)!=null;
        if(!recent) notes.Add("old or future-dated history / repeat under the same conditions");
        if(rate.IdleGaps>0) notes.Add("long delivery gaps / pauses, receiver placement or system scheduling may contribute / filters cannot recover missing reports");
        if(rate.SameTimestampReports>0) notes.Add("same-timestamp reports / observed batching / not a configured polling-rate readback");
        if(rate.SlowIntervals>0) notes.Add("slow intervals present / repeat continuous movement before comparing");
        notes.Add("delivery timing does not measure sensor spinout, hand tremor or game latency");
        return new InputHistoryReport {Available=true,Recent=recent,AgeHours=(now.ToUniversalTime()-measured).TotalHours,Rate=rate,Notes=notes.ToArray(),Source="validated selected-mouse saved history / not a live hardware reading"};
    }
    private static void LastInputTest() {
        Device device=Selected();InputHistoryReport report=History(Last<RateResult>("rate.json",device),device.Path,DateTime.UtcNow);
        if(json) {Console.WriteLine(Store.Json.Serialize(report));return;}
        Console.WriteLine("\n  last input test / read only");
        if(report.Available) {Console.WriteLine("  measured utc / "+report.Rate.MeasuredUtc+" / "+report.Rate.Reports+" reports");PrintRate(report.Rate);}
        foreach(string note in report.Notes) Console.WriteLine("  "+note);
    }
    private static string Optional(double? value,string unit) {return value.HasValue ? F(value.Value)+unit : "unavailable";}
    private static void PrintRate(RateResult result) {
        Console.WriteLine("  ~"+F(result.ActiveHz)+" hz / observed input\n  p95 "+F(result.P95IntervalMs)+" ms / p99 "+Optional(result.P99IntervalMs," ms")+"\n  slow intervals "+(result.SlowIntervals.HasValue ? result.SlowIntervals.Value.ToString(CultureInfo.InvariantCulture) : "unavailable")+" / long gaps "+result.IdleGaps+" / max "+Optional(result.MaxGapMs," ms")+"\n  "+result.Quality);
        Console.WriteLine("  including gaps ~"+Optional(result.DeliveredHz," hz")+" / gap time "+Optional(result.GapPercent,"%")+"\n  slow threshold "+Optional(result.SlowThresholdMs," ms")+" / heuristic, not lost-packet count");
        PrintComparison(result.Comparison);
        if(result.IdleGaps>0 || result.SlowIntervals>0) Console.WriteLine("  repeat without stopping / check receiver, surface and scheduling\n  output smoothing does not repair delivery gaps");
    }
    private static void PrintSession(GameSessionReport session) {
        if(session==null) return;
        Console.WriteLine("  running games / "+(session.RunningGames.Length==0 ? session.QuerySucceeded ? "none detected" : "unknown" : String.Join(" + ",session.RunningGames)));
        if(!session.QuerySucceeded) Console.WriteLine("  process query incomplete / game input remains unknown");
    }
}
}
