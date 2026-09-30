using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;

// Opt-in, read-only RF check. Place test tags on ANT1. No configuration/tag writes.
class InventoryHardwareSdk {
    static int Main(string[] args){var client=new GClient();try{
        eConnectionAttemptEventStatusType status;
        bool opened=args[0].StartsWith("COM")?client.OpenSerial(args[0],5000,out status):client.OpenTcp(args[0],5000,out status);
        if(!opened)throw new Exception("Cannot connect: "+status);
        int count=0,bad=0;var unique=new HashSet<string>();var sync=new object();var ended=new ManualResetEvent(false);
        client.OnEncapedTagEpcLog+=delegate(EncapedLogBaseEpcInfo e){var t=e.logBaseEpcInfo;lock(sync){count++;unique.Add(t.Epc);if(t.AntId!=1||t.Result!=0||string.IsNullOrEmpty(t.Epc)||((t.Pc>>11)&31)*4!=t.Epc.Length)bad++;}};
        client.OnEncapedTagEpcOver+=delegate(EncapedLogBaseEpcOver e){ended.Set();};
        var inv=new MsgBaseInventoryEpc{AntennaEnable=1,InventoryMode=1};var watch=Stopwatch.StartNew();
        client.SendSynMsg(inv,3000);if(inv.RtCode!=0)throw new Exception("Inventory rejected: "+inv.RtCode);
        Thread.Sleep(4000);
        var stop=new MsgBaseStop();client.SendSynMsg(stop,10000);
        if(stop.RtCode!=0||!ended.WaitOne(2000))throw new Exception("Stop did not complete");
        watch.Stop();int stopped;lock(sync){stopped=count;Console.WriteLine("NATION SDK reports={0} unique={1} invalid_fields={2} elapsed_ms={3} reports/sec={4:F1}",count,unique.Count,bad,watch.ElapsedMilliseconds,1000.0*count/watch.ElapsedMilliseconds);}
        Thread.Sleep(300);lock(sync){if(count==0||bad!=0||count!=stopped)throw new Exception("Missing/invalid/late tag reports");}
        Console.WriteLine("PASS: original Nation SDK receives valid EPC/PC/antenna; no reports after Stop");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}finally{client.Close();}}
}
