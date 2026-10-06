using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Helox {
internal static class Native {
    [DllImport("user32.dll")] internal static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SystemParametersInfo(uint action, uint param, IntPtr data, uint flags);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputDeviceList(IntPtr list, ref uint count, uint size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool RegisterRawInputDevices([In] RawRegistration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr handles, uint milliseconds, uint wakeMask, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] internal static extern bool HidD_GetProductString(SafeFileHandle file, byte[] buffer, uint length);
    [DllImport("hid.dll")] internal static extern bool HidD_GetManufacturerString(SafeFileHandle file, byte[] buffer, uint length);
    [StructLayout(LayoutKind.Sequential)] internal struct DeviceEntry { public IntPtr Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawRegistration { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    internal static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    internal static int[] Read(uint action, int count) {
        IntPtr p=Marshal.AllocHGlobal(count*4);
        try { Check(SystemParametersInfo(action,0,p,0)); int[] a=new int[count]; Marshal.Copy(p,a,0,count); return a; }
        finally { Marshal.FreeHGlobal(p); }
    }
    internal static void Write(uint action, int[] values) {
        IntPtr p=Marshal.AllocHGlobal(values.Length*4);
        try { Marshal.Copy(values,0,p,values.Length); Check(SystemParametersInfo(action,0,p,3)); }
        finally { Marshal.FreeHGlobal(p); }
    }
    internal static string HidString(string path, bool product) {
        using (SafeFileHandle h=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
            if(h.IsInvalid) return null;
            byte[] buffer=new byte[512];
            bool ok=product ? HidD_GetProductString(h,buffer,512) : HidD_GetManufacturerString(h,buffer,512);
            return ok ? Encoding.Unicode.GetString(buffer).TrimEnd('\0') : null;
        }
    }
}
}
