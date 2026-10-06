using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Helox {
public sealed class HidCapability {
    public string Path {get;set;}
    public string Product {get;set;}
    public string UsagePage {get;set;}
    public string Usage {get;set;}
    public int InputReportBytes {get;set;}
    public int OutputReportBytes {get;set;}
    public int FeatureReportBytes {get;set;}
    public string Error {get;set;}
}
internal static class HidProbe {
    [StructLayout(LayoutKind.Sequential)] private struct InterfaceData {
        internal int Size; internal Guid ClassGuid; internal uint Flags; internal IntPtr Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Caps {
        internal ushort Usage, UsagePage, InputBytes, OutputBytes, FeatureBytes;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)] internal ushort[] Reserved;
        internal ushort LinkNodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices,
            FeatureButtons, FeatureValues, FeatureIndices;
    }
    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data,out Caps caps);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid,IntPtr enumerator,IntPtr parent,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint bytes,out uint needed,IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    internal static List<HidCapability> Read() {
        Guid guid; HidD_GetHidGuid(out guid);
        IntPtr set=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);
        if(set==new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        List<HidCapability> result=new List<HidCapability>();
        try {
            for(uint i=0;;i++) {
                InterfaceData data=new InterfaceData {Size=Marshal.SizeOf(typeof(InterfaceData))};
                if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref data)) {
                    int error=Marshal.GetLastWin32Error();if(error==259) break;throw new Win32Exception(error);
                }
                uint needed;
                SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out needed,IntPtr.Zero);
                if(needed<8) throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr detail=Marshal.AllocHGlobal((int)needed);
                try {
                    Marshal.WriteInt32(detail,IntPtr.Size==8 ? 8 : 6);
                    Native.Check(SetupDiGetDeviceInterfaceDetail(set,ref data,detail,needed,out needed,IntPtr.Zero));
                    string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                    if(path.IndexOf("vid_145f&pid_0326",StringComparison.OrdinalIgnoreCase)<0) continue;
                    HidCapability capability=new HidCapability {Path=path,Product=Native.HidString(path,true)};
                    using(SafeFileHandle h=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                        IntPtr parsed=IntPtr.Zero;
                        if(h.IsInvalid || !HidD_GetPreparsedData(h,out parsed)) capability.Error="read access unavailable";
                        else try {
                            Caps caps;int status=HidP_GetCaps(parsed,out caps);
                            if(status!=0x110000) capability.Error="hid capabilities unavailable";
                            else {
                                capability.UsagePage="0x"+caps.UsagePage.ToString("x4");capability.Usage="0x"+caps.Usage.ToString("x4");
                                capability.InputReportBytes=caps.InputBytes;capability.OutputReportBytes=caps.OutputBytes;capability.FeatureReportBytes=caps.FeatureBytes;
                            }
                        } finally {HidD_FreePreparsedData(parsed);}
                    }
                    result.Add(capability);
                } finally {Marshal.FreeHGlobal(detail);}
            }
        } finally {SetupDiDestroyDeviceInfoList(set);}
        return result;
    }
}
}
