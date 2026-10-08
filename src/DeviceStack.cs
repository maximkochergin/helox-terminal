using System;
using System.IO;
using System.Text;

namespace Helox {
public sealed class DeviceStackReport {
    public string InstanceId {get;set;}
    public bool? Started {get;set;}
    public uint? ProblemCode {get;set;}
    public string[] Services {get;set;}
    public bool? RawAccelPresent {get;set;}
    public string Error {get;set;}
    public string Source {get;set;}
}
internal static class DeviceStack {
    internal delegate uint PropertyRead(out uint type,byte[] buffer,ref uint size);
    internal static string[] Decode(uint type,byte[] data,int size,bool list) {
        if(type!=(list ? 0x2012u : 0x12u) || data==null || size>(long)data.Length || size<(list ? 4 : 2) || size%2!=0)
            throw new IOException("invalid pnp text property");
        string value=new UnicodeEncoding(false,false,true).GetString(data,0,size);
        if(list) {
            if(!value.EndsWith("\0\0",StringComparison.Ordinal)) throw new IOException("unterminated pnp stack property");
            string content=value.Substring(0,value.Length-2);
            if(content.Length==0) return new string[0];
            string[] items=content.Split('\0');
            foreach(string item in items) if(String.IsNullOrWhiteSpace(item)) throw new IOException("empty pnp stack entry");
            return items;
        }
        if(value.IndexOf('\0')!=value.Length-1 || value.Length==1) throw new IOException("invalid pnp instance id");
        return new string[]{value.Substring(0,value.Length-1)};
    }
    internal static string[] Property(PropertyRead read,bool list) {
        // Configuration Manager returns CR_SUCCESS (0) or CR_BUFFER_SMALL (26).
        for(int attempt=0;attempt<3;attempt++) {
            uint type,size=0;uint result=read(out type,null,ref size);
            if(result!=0 && result!=26) throw new IOException("pnp property unavailable / code "+result);
            if(size<2 || size>65536 || size%2!=0) throw new IOException("invalid pnp property size");
            byte[] data=new byte[(int)size];
            result=read(out type,data,ref size);
            if(result==26) continue; // Device/property may change between the size and data calls.
            if(result!=0) throw new IOException("pnp property unavailable / code "+result);
            if(size>data.Length) throw new IOException("invalid pnp property size");
            return Decode(type,data,(int)size,list);
        }
        throw new IOException("pnp device changed / retry");
    }
    internal static string Instance(string path) {
        if(String.IsNullOrEmpty(path)) throw new IOException("mouse interface unavailable");
        Native.DevPropertyKey key=new Native.DevPropertyKey("78c34fc8-104a-4aca-9ea4-524d52996e57",256);
        return Property(delegate(out uint type,byte[] data,ref uint size) {return Native.CM_Get_Device_Interface_PropertyW(path,ref key,out type,data,ref size,0);},false)[0];
    }
    internal static bool? IncludesRawAccel(string[] services) {
        if(services==null || services.Length==0) return null;
        foreach(string service in services) if(String.Equals(service,"rawaccel",StringComparison.OrdinalIgnoreCase) || String.Equals(service,@"\Driver\rawaccel",StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    internal static DeviceStackReport Read(Device device) {
        if(device==null) return null;
        DeviceStackReport report=new DeviceStackReport {Source="windows pnp device stack / not game input or riot approval"};
        try {
            report.InstanceId=Instance(device.Path);
            uint node,result=Native.CM_Locate_DevNodeW(out node,report.InstanceId,0);
            if(result!=0) throw new IOException("pnp device unavailable / code "+result);
            uint status,problem;result=Native.CM_Get_DevNode_Status(out status,out problem,node,0);
            if(result!=0) throw new IOException("pnp device status unavailable / code "+result);
            report.Started=(status&8)!=0;report.ProblemCode=problem;
            // DEVPKEY_Device_Stack from the Windows SDK, not class filter registration.
            Native.DevPropertyKey key=new Native.DevPropertyKey("540b947e-8b40-45bc-a8a2-6a0b894cbda2",14);
            report.Services=Property(delegate(out uint type,byte[] data,ref uint size) {return Native.CM_Get_DevNode_PropertyW(node,ref key,out type,data,ref size,0);},true);
            report.RawAccelPresent=IncludesRawAccel(report.Services);
            if(report.RawAccelPresent==null) report.Error="pnp stack empty / filter state unknown";
        }catch(Exception e) {report.Error=e.Message.ToLowerInvariant();}
        return report;
    }
}
}
