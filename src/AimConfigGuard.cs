using System;
using System.Collections.Generic;

namespace Helox {
// Validate managed data before the upstream bridge can coerce or truncate it.
internal static class AimConfigGuard {
    internal static void Check(Dictionary<string,object> cfg) {
        if(cfg==null) Fail("empty configuration");
        Text(cfg,"version",32,false);
        Device(Bag(cfg,"defaultDeviceConfig"));
        object[] profiles=Array(cfg,"profiles"),devices=Array(cfg,"devices");
        if(profiles.Length==0) Fail("missing default profile");
        HashSet<string> names=new HashSet<string>(StringComparer.Ordinal);
        foreach(object item in profiles) {
            Dictionary<string,object> profile=ObjectBag(item,"profile");
            string name=Text(profile,"name",255,false);
            if(!names.Add(name)) Fail("duplicate profile name");
            Vector(Bag(profile,"Stretches domain for horizontal vs vertical inputs"));
            Vector(Bag(profile,"Stretches accel range for horizontal vs vertical inputs"));
            Accel(Bag(profile,"Whole or horizontal accel parameters"));
            Accel(Bag(profile,"Vertical accel parameters"));
            Dictionary<string,object> speed=Bag(profile,"Input speed calculation parameters");
            Boolean(speed,"Whole/combined accel (set false for 'by component' mode)");Number(speed,"lpNorm");
            foreach(string key in new string[]{"Time in ms after which an input is weighted at half its original value.","Time in ms after which scale is weighted at half its original value.","Time in ms after which an output is weighted at half its original value."})
                if(Number(speed,key)<0) Fail("negative smoothing half-life");
            foreach(string key in new string[]{"Output DPI","Y/X output DPI ratio (vertical sens multiplier)","L/R output DPI ratio (left sens multiplier)","U/D output DPI ratio (up sens multiplier)","Degrees of rotation","Degrees of angle snapping","Input Speed Cap"}) Number(profile,key);
        }
        HashSet<string> ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(object item in devices) {
            Dictionary<string,object> device=ObjectBag(item,"device");
            if(!ids.Add(Text(device,"id",199,false))) Fail("duplicate device id");
            Text(device,"name",255,true);string profile=Text(device,"profile",255,true);
            if(profile.Length!=0 && !names.Contains(profile)) Fail("device references a missing profile");
            Device(Bag(device,"config"));
        }
    }
    private static void Device(Dictionary<string,object> config) {
        Boolean(config,"disable");
        foreach(string key in new string[]{"setExtraInfo","Use constant time interval based on polling rate"}) if(config.ContainsKey(key)) Boolean(config,key);
        foreach(string key in new string[]{"DPI (normalizes input speed unit: counts/ms -> in/s)","Polling rate Hz (keep at 0 for automatic adjustment)"}) {
            double number=Number(config,key);if(number<0 || number>Int32.MaxValue || Math.Truncate(number)!=number) Fail("invalid device integer");
        }
        double min=config.ContainsKey("minimumTime") ? Number(config,"minimumTime") : .0625;
        double max=config.ContainsKey("maximumTime") ? Number(config,"maximumTime") : 100;
        if(min<=0 || max<min) Fail("invalid time interval clamp");
    }
    private static void Accel(Dictionary<string,object> args) {
        string mode=Text(args,"mode",32,false);
        if(mode!="classic" && mode!="jump" && mode!="natural" && mode!="synchronous" && mode!="power" && mode!="lut" && mode!="noaccel") Fail("invalid acceleration mode");
        string cap=Text(args,"Cap mode",32,false);if(cap!="in_out" && cap!="input" && cap!="output") Fail("invalid cap mode");
        Boolean(args,"Gain / Velocity");
        foreach(string key in new string[]{"inputOffset","outputOffset","acceleration","decayRate","gamma","motivity","exponentClassic","scale","exponentPower","limit","syncSpeed","smooth"}) Number(args,key);
        Vector(Bag(args,"Cap / Jump"));object[] data=Array(args,"data");
        if(data.Length>514 || data.Length%2!=0) Fail("invalid lookup data length");
        foreach(object value in data) if(Math.Abs(Finite(value,"lookup data"))>Single.MaxValue) Fail("lookup data exceeds native float range");
        if(mode=="lut") {
            if(data.Length<4) Fail("lookup requires at least two points");
            float previous=-1;
            for(int i=0;i<data.Length;i+=2) {
                // Native tables store floats: distinct doubles can collapse to the same float.
                float speed=(float)Convert.ToDouble(data[i]);
                if(speed<0 || speed<=previous) Fail("lookup speeds must increase after native float conversion");
                previous=speed;
            }
        }
    }
    private static void Vector(Dictionary<string,object> vector) {Number(vector,"x");Number(vector,"y");}
    private static object Field(Dictionary<string,object> bag,string key) {object value;if(!bag.TryGetValue(key,out value)) Fail("missing "+key);return value;}
    private static Dictionary<string,object> Bag(Dictionary<string,object> bag,string key) {return ObjectBag(Field(bag,key),key);}
    private static Dictionary<string,object> ObjectBag(object value,string key) {Dictionary<string,object> bag=value as Dictionary<string,object>;if(bag==null) Fail("invalid "+key);return bag;}
    private static object[] Array(Dictionary<string,object> bag,string key) {object[] array=Field(bag,key) as object[];if(array==null) Fail("invalid "+key);return array;}
    private static string Text(Dictionary<string,object> bag,string key,int max,bool empty) {string value=Field(bag,key) as string;if(value==null || (!empty && value.Length==0) || value.Length>max || value.IndexOf('\0')>=0) Fail("invalid "+key+" length or type");return value;}
    private static void Boolean(Dictionary<string,object> bag,string key) {if(!(Field(bag,key) is bool)) Fail("invalid "+key+" type");}
    private static double Number(Dictionary<string,object> bag,string key) {return Finite(Field(bag,key),key);}
    private static double Finite(object value,string key) {
        if(!(value is int) && !(value is long) && !(value is double) && !(value is decimal) && !(value is float)) Fail("invalid "+key+" numeric type");
        double number=Convert.ToDouble(value);if(Double.IsNaN(number) || Double.IsInfinity(number)) Fail("nonfinite "+key);return number;
    }
    private static void Fail(string reason) {throw new ArgumentException("invalid aim configuration / "+reason.ToLowerInvariant());}
}
}
