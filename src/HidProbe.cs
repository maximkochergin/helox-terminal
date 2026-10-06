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
    public string Manufacturer {get;set;}
    public string VendorId {get;set;}
    public string ProductId {get;set;}
    public string DeviceRevision {get;set;}
    public List<HidValue> Values {get;set;}
}
public sealed class HidValue {
    public string UsagePage {get;set;}
    public string Usage {get;set;}
    public int ReportId {get;set;}
    public int Bits {get;set;}
    public bool Absolute {get;set;}
    public uint Units {get;set;}
    public uint UnitsExponent {get;set;}
    public int LogicalMin {get;set;}
    public int LogicalMax {get;set;}
    public int PhysicalMin {get;set;}
    public int PhysicalMax {get;set;}
}
internal static class HidProbe {
    [StructLayout(LayoutKind.Sequential)] private struct Attributes {internal uint Size;internal ushort Vendor,Product,Version;}
    [StructLayout(LayoutKind.Sequential)] private struct ValueCaps {
        internal ushort Page;internal byte Report,Alias;internal ushort BitField,Collection,LinkUsage,LinkPage;
        internal byte Range,StringRange,DesignatorRange,Absolute,Null,Reserved;
        internal ushort Bits,Count;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=5)] internal ushort[] Reserved2;
        internal uint UnitsExponent,Units;internal int LogicalMin,LogicalMax,PhysicalMin,PhysicalMax;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=8)] internal ushort[] UsageData;
    }
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
    [DllImport("hid.dll")] private static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attributes);
    [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int reportType,[Out] ValueCaps[] values,ref ushort count,IntPtr data);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid,IntPtr enumerator,IntPtr parent,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint bytes,out uint needed,IntPtr device);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    internal static string Family(string path) {
        if(path==null) return null;
        string[] parts=path.ToLowerInvariant().Split('#');
        if(parts.Length<3) return path.ToLowerInvariant();
        string id=System.Text.RegularExpressions.Regex.Replace(parts[1],@"&col[0-9a-f]+","");
        int last=parts[2].LastIndexOf('&');
        return id+"#"+(last<0 ? parts[2] : parts[2].Substring(0,last));
    }
    internal static List<HidCapability> Read(string selectedPath=null) {
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
                    if(selectedPath==null ? path.IndexOf("vid_145f&pid_0326",StringComparison.OrdinalIgnoreCase)<0 : Family(path)!=Family(selectedPath)) continue;
                    HidCapability capability=new HidCapability {Path=path,Product=Native.HidString(path,true),Manufacturer=Native.HidString(path,false),Values=new List<HidValue>()};
                    using(SafeFileHandle h=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                        Attributes attributes=new Attributes {Size=(uint)Marshal.SizeOf(typeof(Attributes))};
                        if(!h.IsInvalid && HidD_GetAttributes(h,ref attributes)) {
                            capability.VendorId=attributes.Vendor.ToString("x4");capability.ProductId=attributes.Product.ToString("x4");capability.DeviceRevision="0x"+attributes.Version.ToString("x4");
                        }
                        IntPtr parsed=IntPtr.Zero;
                        if(h.IsInvalid || !HidD_GetPreparsedData(h,out parsed)) capability.Error="read access unavailable";
                        else try {
                            Caps caps;int status=HidP_GetCaps(parsed,out caps);
                            if(status!=0x110000) capability.Error="hid capabilities unavailable";
                            else {
                                capability.UsagePage="0x"+caps.UsagePage.ToString("x4");capability.Usage="0x"+caps.Usage.ToString("x4");
                                capability.InputReportBytes=caps.InputBytes;capability.OutputReportBytes=caps.OutputBytes;capability.FeatureReportBytes=caps.FeatureBytes;
                                if(caps.InputValues>0) {
                                    ushort count=caps.InputValues;ValueCaps[] values=new ValueCaps[count];
                                    if(HidP_GetValueCaps(0,values,ref count,parsed)==0x110000)
                                        for(int v=0;v<count;v++) {
                                            ValueCaps value=values[v];
                                            capability.Values.Add(new HidValue {UsagePage="0x"+value.Page.ToString("x4"),Usage="0x"+value.UsageData[0].ToString("x4"),
                                                ReportId=value.Report,Bits=value.Bits,Absolute=value.Absolute!=0,Units=value.Units,UnitsExponent=value.UnitsExponent,
                                                LogicalMin=value.LogicalMin,LogicalMax=value.LogicalMax,PhysicalMin=value.PhysicalMin,PhysicalMax=value.PhysicalMax});
                                        }
                                }
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
