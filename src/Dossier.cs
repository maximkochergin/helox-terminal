using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Helox {
public sealed class DriverDetails {
    public string Description {get;set;}
    public string Provider {get;set;}
    public string Version {get;set;}
    public string Inf {get;set;}
    public string Source {get;set;}
    public string Error {get;set;}
    internal static DriverDetails Read(Device device) {
        DriverDetails result=new DriverDetails {Source="installed windows device registry"};
        try {
            string[] parts=device.Path.Split('#');
            if(parts.Length<3) return result;
            string instance="HID\\"+parts[1]+"\\"+parts[2];
            using(RegistryKey node=Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Enum\\"+instance)) {
                string driver=node==null ? null : node.GetValue("Driver") as string;
                if(driver==null) {result.Error="driver details not available";return result;}
                using(RegistryKey key=Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Class\\"+driver)) {
                    if(key==null) {result.Error="driver details not available";return result;}
                    result.Description=key.GetValue("DriverDesc") as string;result.Provider=key.GetValue("ProviderName") as string;
                    result.Version=key.GetValue("DriverVersion") as string;result.Inf=key.GetValue("InfPath") as string;
                }
            }
        }catch(Exception) {result.Error="driver registry not readable";}
        return result;
    }
}
public sealed class ModelFacts {
    public string Model {get;set;}
    public int MinDpi {get;set;}
    public int MaxDpi {get;set;}
    public int DpiLevels {get;set;}
    public int LengthMm {get;set;}
    public int WidthMm {get;set;}
    public int HeightMm {get;set;}
    public int ListedButtons {get;set;}
    public string Connection {get;set;}
    public string Source {get;set;}
    public string CheckedDate {get;set;}
    internal static ModelFacts For(Device device) {
        if(device==null || !device.TrustCandidate || device.Product==null || device.Product.IndexOf("gxt 929",StringComparison.OrdinalIgnoreCase)<0) return null;
        return new ModelFacts {Model="trust gxt 929 helox",MinDpi=800,MaxDpi=4800,DpiLevels=4,LengthMm=125,WidthMm=64,HeightMm=38,
            ListedButtons=5,Connection="2.4 ghz wireless receiver",CheckedDate="2026-10-06",
            Source="https://www.trust.com/en/product/25307-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse"};
    }
}
public sealed class MouseDossier {
    public Device Device {get;set;}
    public DriverDetails Driver {get;set;}
    public ModelFacts Model {get;set;}
    public List<HidCapability> Hid {get;set;}
    public string HidError {get;set;}
    public string DpiReadback {get;set;}
    public string Link {get;set;}
    internal static MouseDossier Read(Device device) {
        MouseDossier result=new MouseDossier {Device=device,Driver=device==null ? null : DriverDetails.Read(device),Model=ModelFacts.For(device),
            DpiReadback="unavailable: no verified hardware query",Link="receiver presence does not confirm wireless link or mouse power"};
        if(device!=null) {
            try {
                result.Hid=HidProbe.Read(device.Path);
                foreach(HidCapability hid in result.Hid) if(hid.UsagePage=="0x0001" && hid.Usage=="0x0002") {
                    bool physical=false;
                    foreach(HidValue value in hid.Values) if((value.Usage=="0x0030" || value.Usage=="0x0031") && value.Units!=0 && value.PhysicalMin!=value.PhysicalMax) physical=true;
                    if(!physical) result.DpiReadback="unavailable: x/y descriptors contain no physical distance scale; vendor protocol unverified";
                }
            }catch(Exception) {result.HidError="hid diagnostics unavailable";}
        }
        return result;
    }
}
}
