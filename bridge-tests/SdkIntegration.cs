using System;
using System.Collections.Generic;
using System.Threading;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;

class SdkIntegration {
    static int checks;
    static void Assert(bool b,string s){if(!b)throw new Exception("FAIL: "+s);Console.WriteLine("PASS: "+s);checks++;}
    static void Send(GClient c,Message m){c.SendSynMsg(m,2500);Assert(m.RtCode==0,m.GetType().Name+" code="+m.RtCode+" "+m.RtMsg);}
    static int Main(string[] args) {
        var c=new GClient();
        try {
            eConnectionAttemptEventStatusType state;
            Assert(c.OpenTcp(args.Length>0?args[0]:"127.0.0.1:18160",3000,out state),"Original Nation SDK connected: "+state);
            var info=new MsgAppGetReaderInfo();Send(c,info);Assert(info.Imei=="SIMULATED-ZK-BRIDGE","Identity explicitly identifies simulation");
            var cap=new MsgBaseGetCapabilities();Send(c,cap);Assert(cap.AntennaCount==4,"Four antennas decoded by Nation");
            var power=new MsgBaseGetPower();Send(c,power);Assert(power.DicPower.Count==4&&power.DicPower[1]==20,"Power decoded");
            var set=new MsgBaseSetPower();set.DicPower=new Dictionary<byte,byte>{{1,22},{2,22},{3,22},{4,22}};set.IsPersistence=0;Send(c,set);
            power=new MsgBaseGetPower();Send(c,power);Assert(power.DicPower[4]==22,"Power set/read roundtrip");
            set=new MsgBaseSetPower();set.DicPower=new Dictionary<byte,byte>{{1,21},{2,22},{3,22},{4,22}};c.SendSynMsg(set,2000);Assert(set.RtCode!=0,"Unequal antenna power rejected");
            var tags=new List<LogBaseEpcInfo>();var ended=new ManualResetEvent(false);var sync=new object();
            c.OnEncapedTagEpcLog+=delegate(EncapedLogBaseEpcInfo e){lock(sync){tags.Add(e.logBaseEpcInfo);}};
            c.OnEncapedTagEpcOver+=delegate(EncapedLogBaseEpcOver e){ended.Set();};
            var inv=new MsgBaseInventoryEpc{AntennaEnable=15,InventoryMode=0,ReadTid=new ParamEpcReadTid{Mode=0,Len=6}};
            Send(c,inv);Assert(ended.WaitOne(4000),"Single inventory completes");
            lock(sync){Assert(tags.Count==4,"One report per selected antenna");for(int n=0;n<4;n++){var t=tags[n];Console.WriteLine("DECODED EPC="+t.Epc+" TID="+t.Tid+" PC="+t.Pc+" ANT="+t.AntId+" RSSI="+t.Rssi+" RESULT="+t.Result);Assert(t.Result==0&&t.AntId==n+1&&t.Pc==0x3000&&t.Rssi==65&&t.Epc.EndsWith((n+1).ToString("X2"))&&t.Tid==("E280"+new string('0',20)),"Tag fields EPC/TID/PC/RSSI/antenna "+(n+1));}}
            ended.Reset();lock(sync)tags.Clear();
            inv=new MsgBaseInventoryEpc{AntennaEnable=2,InventoryMode=1};Send(c,inv);Thread.Sleep(200);Send(c,new MsgBaseStop());Assert(ended.WaitOne(2000),"Continuous inventory end callback on stop");
            Thread.Sleep(200);int stopped;lock(sync){stopped=tags.Count;Assert(stopped>0,"Continuous inventory delivered tags");foreach(var t in tags)Assert(t.AntId==2,"Selected antenna preserved");}
            Thread.Sleep(200);lock(sync)Assert(tags.Count==stopped,"No reports after stop completion");
            inv=new MsgBaseInventoryEpc{AntennaEnable=16,InventoryMode=0};c.SendSynMsg(inv,2000);Assert(inv.RtCode!=0,"Invalid fifth antenna rejected");
            // Reset writes must never be silently acknowledged by this read-focused bridge.
            var reset=new MsgAppReset();c.SendSynMsg(reset,600);Assert(reset.RtCode!=0,"Unsupported command does not report success");
            Send(c,new MsgBaseStop());Assert(c.Close(),"Connection closes");
            Console.WriteLine("ALL PASSED: "+checks+" assertions using unmodified Nation demo SDK");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}finally{c.Close();}
    }
}
