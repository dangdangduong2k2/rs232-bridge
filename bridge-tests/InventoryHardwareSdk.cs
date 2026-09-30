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
        int count=0,bad=0,endReason=255;uint mask=args.Length>2?uint.Parse(args[2]):1;var unique=new HashSet<string>();var sync=new object();var ended=new ManualResetEvent(false);var watch=Stopwatch.StartNew();long lastReport=-1,maxGap=0;int gaps500=0,gaps1000=0;
        client.OnEncapedTagEpcLog+=delegate(EncapedLogBaseEpcInfo e){var t=e.logBaseEpcInfo;lock(sync){long now=watch.ElapsedMilliseconds;if(lastReport>=0){long gap=now-lastReport;maxGap=Math.Max(maxGap,gap);if(gap>=500)gaps500++;if(gap>=1000)gaps1000++;}lastReport=now;count++;unique.Add(t.Epc);if(t.AntId<1||t.AntId>4||(mask&(1u<<(t.AntId-1)))==0||t.Result!=0||string.IsNullOrEmpty(t.Epc)||((t.Pc>>11)&31)*4!=t.Epc.Length)bad++;}};
        client.OnEncapedTagEpcOver+=delegate(EncapedLogBaseEpcOver e){endReason=e.logBaseEpcOver.RtCode;ended.Set();};
        var inv=new MsgBaseInventoryEpc{AntennaEnable=mask,InventoryMode=1};watch.Restart();
        client.SendSynMsg(inv,3000);if(inv.RtCode!=0)throw new Exception("Inventory rejected: "+inv.RtCode);
        Thread.Sleep(args.Length>1?int.Parse(args[1]):4000);
        var stop=new MsgBaseStop();client.SendSynMsg(stop,10000);
        if(stop.RtCode!=0||!ended.WaitOne(2000)||endReason!=1)throw new Exception("Stop did not complete normally: "+endReason);
        watch.Stop();int stopped;lock(sync){stopped=count;Console.WriteLine("NATION SDK reports={0} unique={1} invalid_fields={2} elapsed_ms={3} reports/sec={4:F1}",count,unique.Count,bad,watch.ElapsedMilliseconds,1000.0*count/watch.ElapsedMilliseconds);}
        Console.WriteLine("REPORT GAPS max_ms={0} gaps_at_least_500ms={1} gaps_at_least_1000ms={2} (includes warmup; excludes initial wait and Stop)",maxGap,gaps500,gaps1000);
        Thread.Sleep(300);lock(sync){if(count==0||bad!=0||count!=stopped)throw new Exception("Missing/invalid/late tag reports");}
        Console.WriteLine("PASS: original Nation SDK receives valid EPC/PC/antenna; no reports after Stop");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}finally{client.Close();}}
}
