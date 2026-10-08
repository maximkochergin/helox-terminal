using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml;

namespace Helox {
public sealed class ApplicationCrash {
    public string Utc {get;set;}
    public string Application {get;set;}
    public string Module {get;set;}
    public string ExceptionCode {get;set;}
    public bool RelatedToAim {get;set;}
    public bool? NearShutdown {get;set;}
}
public sealed class ShutdownReport {
    public string LastShutdownUtc {get;set;}
    public ApplicationCrash[] RecentApplicationCrashes {get;set;}
    public ApplicationCrash[] AimApplicationCrashes {get;set;}
    public string[] Errors {get;set;}
    public bool ScanLimitReached {get;set;}
    public string Source {get;set;}
}
internal static class ShutdownDiagnostics {
    internal static bool IsAim(string application,string module) {
        foreach(string name in new string[]{"helox.exe","rawaccel.exe","rawaccel.sys"})
            if(String.Equals(application,name,StringComparison.OrdinalIgnoreCase) || String.Equals(module,name,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    internal static bool Near(DateTime crash,List<DateTime> shutdowns) {
        foreach(DateTime time in shutdowns) if(Math.Abs((crash-time).TotalSeconds)<=120) return true;
        return false;
    }
    internal static bool? Proximity(DateTime crash,List<DateTime> shutdowns,bool complete) {return Near(crash,shutdowns) ? true : complete ? (bool?)false : null;}
    private static string Field(string xml,string name) {
        XmlDocument doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);
        foreach(XmlNode node in doc.GetElementsByTagName("Data")) if(node.Attributes["Name"]!=null && node.Attributes["Name"].Value==name) return node.InnerText;
        return null;
    }
    internal static ShutdownReport Read() {
        List<string> errors=new List<string>();List<DateTime> shutdowns=new List<DateTime>();
        List<ApplicationCrash> recent=new List<ApplicationCrash>(),aim=new List<ApplicationCrash>();bool limit=false,shutdownComplete=true;
        string age="TimeCreated[timediff(@SystemTime) >= 0 and timediff(@SystemTime) <= 1209600000]";
        try {
            EventLogQuery query=new EventLogQuery("System",PathType.LogName,"*[System[Provider[@Name='User32'] and EventID=1074 and "+age+"]]");query.ReverseDirection=true;
            using(EventLogReader reader=new EventLogReader(query)) for(int i=0;i<64;i++) {
                using(EventRecord entry=reader.ReadEvent()) {if(entry==null) break;if(entry.TimeCreated.HasValue) shutdowns.Add(entry.TimeCreated.Value.ToUniversalTime());if(i==63) {limit=true;shutdownComplete=false;}}
            }
        }catch(Exception e) {shutdownComplete=false;errors.Add("system journal unavailable / "+e.GetType().Name.ToLowerInvariant());}
        try {
            EventLogQuery query=new EventLogQuery("Application",PathType.LogName,"*[System[Provider[@Name='Application Error'] and EventID=1000 and "+age+"]]");query.ReverseDirection=true;
            using(EventLogReader reader=new EventLogReader(query)) for(int i=0;i<128;i++) {
                using(EventRecord entry=reader.ReadEvent()) {
                    if(entry==null) break;if(i==127) limit=true;if(!entry.TimeCreated.HasValue) continue;
                    string xml=entry.ToXml();DateTime time=entry.TimeCreated.Value.ToUniversalTime();
                    ApplicationCrash crash=new ApplicationCrash {Utc=time.ToString("o",CultureInfo.InvariantCulture),Application=Field(xml,"AppName"),Module=Field(xml,"ModuleName"),ExceptionCode=Field(xml,"ExceptionCode"),NearShutdown=Proximity(time,shutdowns,shutdownComplete)};
                    crash.RelatedToAim=IsAim(crash.Application,crash.Module) ||
                        (String.Equals(crash.Application,"writer.exe",StringComparison.OrdinalIgnoreCase) && String.Equals(Field(xml,"AppPath"),System.IO.Path.Combine(Aim.Root,"writer.exe"),StringComparison.OrdinalIgnoreCase)) ||
                        (String.Equals(crash.Module,"wrapper.dll",StringComparison.OrdinalIgnoreCase) && String.Equals(Field(xml,"ModulePath"),System.IO.Path.Combine(Aim.Root,"wrapper.dll"),StringComparison.OrdinalIgnoreCase));
                    if(crash.RelatedToAim) aim.Add(crash);if(recent.Count<8) recent.Add(crash);
                }
            }
        }catch(Exception e) {errors.Add("application journal unavailable / "+e.GetType().Name.ToLowerInvariant());}
        return new ShutdownReport {LastShutdownUtc=shutdowns.Count==0 ? null : shutdowns[0].ToString("o",CultureInfo.InvariantCulture),RecentApplicationCrashes=recent.ToArray(),AimApplicationCrashes=aim.ToArray(),Errors=errors.ToArray(),ScanLimitReached=limit,
            Source="windows application error 1000 + user32 shutdown 1074 / last 14 days / at most 128 crashes and 64 shutdowns / proximity is not causation; unlogged popups and kernel failures are not ruled out"};
    }
}
}
