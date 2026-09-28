using System;
using System.Threading;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;
class NationSerialCheck {
    static int Main(string[] args){
        if(args.Length!=2||(args[0]!="--serial"&&args[0]!="--test-tcp")){Console.WriteLine("Use --serial COMx:115200. Reads reader information only; no RF scan or writes.");return 2;}
        for(int retry=0;retry<3;retry++){
            var client=new GClient();
            try{
                eConnectionAttemptEventStatusType state;
                bool opened=args[0]=="--serial"?client.OpenSerial(args[1],5000,out state):client.OpenTcp(args[1],5000,out state);
                if(!opened)throw new Exception("Nation OpenSerial/OpenTcp: "+state);
                var info=new MsgAppGetReaderInfo();client.SendSynMsg(info,5000);if(info.RtCode!=0)throw new Exception("Reader info: "+info.RtCode);
                var cap=new MsgBaseGetCapabilities();client.SendSynMsg(cap,5000);if(cap.RtCode!=0||cap.AntennaCount<1)throw new Exception("Capabilities not decoded.");
                if(args[0]=="--serial"&&!info.Imei.StartsWith("ZK-",StringComparison.Ordinal))throw new Exception("Hardware identity not confirmed: "+info.Imei);
                Console.WriteLine("PASS: original GReaderApi.dll "+args[0]+" identity="+info.Imei+" antennas="+cap.AntennaCount);return 0;
            }catch(Exception e){Console.WriteLine("Attempt "+(retry+1)+": "+e.Message);}
            finally{client.Close();}
            Thread.Sleep(800);
        }
        return 1;
    }
}
