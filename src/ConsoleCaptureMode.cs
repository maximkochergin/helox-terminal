using System;
using System.Runtime.InteropServices;

namespace Helox {
// Console selection pauses captures. Disable quick edit for the capture only.
internal sealed class ConsoleCaptureMode : IDisposable {
    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr handle,out uint mode);
    [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr handle,uint mode);
    private IntPtr handle;private uint original;private bool changed;
    internal ConsoleCaptureMode() {
        if(Console.IsInputRedirected) return;
        handle=GetStdHandle(-10);
        if(GetConsoleMode(handle,out original)) changed=SetConsoleMode(handle,(original | 0x80u) & ~0x40u);
    }
    public void Dispose() {if(changed) {SetConsoleMode(handle,original);changed=false;}}
}
}
