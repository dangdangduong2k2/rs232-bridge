using System;
using System.IO;
using System.Collections.Generic;
using NationComPort;
class SetupTests {
    static int checks;
    static void Assert(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
    static void Reject(Action action,string name){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Assert(rejected,name);}
    static Settings Valid(){return new Settings{PhysicalPort="COM49",PhysicalId=@"USB\VID_10C4&PID_EA60\unique",NationPort="COM5",BridgePort="COM6",Pair=0};}
    static int Main(){try{
        var s=Valid();s.Validate();Assert(true,"valid settings");
        Assert(s.EpcMode=="scenario","default continuous EPC mode is Scenario");s.EpcMode="fresh-pc";s.Validate();Assert(true,"fresh-PC mode accepted");s.EpcMode="unknown";Reject(s.Validate,"unknown EPC mode rejected");
        foreach(string bad in new[]{"COM0","COM1\n","COM49 --listen 9","COM1&whoami","LPT1","COM-2"}){s=Valid();s.PhysicalPort=bad;Reject(s.Validate,"reject invalid COM: "+bad.Replace("\n","newline"));}
        s=Valid();s.BridgePort=s.PhysicalPort;Reject(s.Validate,"reject loop to physical port");
        s=Valid();s.Antennas=2;Reject(s.Validate,"reject unsupported antenna count");
        s=Valid();s.Baud=12345;Reject(s.Validate,"reject unsupported ZK baud");
        s=Valid();s.PhysicalId=null;Reject(s.Validate,"require device identity");
        string listing="  CNCA0 PortName=COM#,RealPortName=COM5,EmuOverrun=yes\r\n  CNCB0 PortName=COM#,RealPortName=COM6\r\n  CNCA1 PortName=COM10\r\n  CNCB1 PortName=COM11\r\n";
        var ports=Common.PairPorts(listing,0);Assert(ports["A"]=="COM5"&&ports["B"]=="COM6","parse Ports class endpoints");
        Assert(Common.PairPorts(listing,1)["A"]=="COM10","parse explicit endpoints");
        var hidden=Common.PairPorts("CNCA0 PortName=COM#,RealPortName=COM5\r\nCNCB0 PortName=COM6,HiddenMode=yes,dsr=ropen",0);Assert(hidden["A"]=="COM5"&&hidden["B"]=="COM6","hidden internal endpoint preserves public/private mapping on repair");
        Assert(Common.HiddenBridgeArgs(Valid())=="--silent --wait 30 change CNCB0 PortName=COM6,HiddenMode=yes","hide only B using explicit name without altering peer wiring");
        Assert(Common.BridgeDeviceId(Valid())==@"COM0COM\PORT\CNCB0","rename hidden device by owned identity without re-enumerating as public port");
        Reject(()=>Common.PairPorts(listing,2),"missing driver endpoints fail");
        Reject(()=>Common.PairPorts("CNCA0 PortName=COM#\nCNCB0 PortName=COM#",0),"unassigned ports fail");
        Assert(Common.FreePair(listing)==2,"preserve existing pairs");Assert(Common.FreePair("CNCA10 PortName=COM20")==0,"pair boundary match");
        s=Valid();var devices=new List<Port>{new Port{Name="COM49",Id="another device"},new Port{Name="COM72",Id=s.PhysicalId}};
        Assert(Common.Resolve(s,devices)=="COM72","follow configured USB identity after port change");devices.RemoveAt(1);Assert(Common.Resolve(s,devices)==null,"never attach to reused old COM number");
        devices.Add(new Port{Name="COM70",Id=s.PhysicalId});devices.Add(new Port{Name="COM71",Id=s.PhysicalId});Assert(Common.Resolve(s,devices)==null,"ambiguous identity does not guess");
        Assert(Common.Quote("C:\\Program Files\\Nation\\a.exe")=="\"C:\\Program Files\\Nation\\a.exe\"","quote paths with spaces");
        Assert(Common.Quote("tail\\")=="\"tail\\\\\"","quote trailing backslash");
        Assert(Common.Quote("a\"b")=="\"a\\\"b\"","quote embedded quote");
        string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".xml");try{s=Valid();s.Save(file);var restored=Settings.Load(file);Assert(restored.PhysicalId==s.PhysicalId&&restored.NationPort==s.NationPort,"settings persist identity and mapping");}finally{if(File.Exists(file))File.Delete(file);}
        Console.WriteLine("ALL PASSED: "+checks+" packaging assertions");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
}
