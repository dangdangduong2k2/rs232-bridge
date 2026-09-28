using System;
using System.Threading;
using NationZkBridge;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;

// Opt-in hardware regression. No inventory/tag writes. Restores the native CFG9 pair.
class BasebandSdk {
    static string endpoint,port;static GClient client;static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static ZkReader Native(){return new ZkReader(port,115200,4,30,delegate(string s){});}
    static void Open(){client=new GClient();eConnectionAttemptEventStatusType status;Check(endpoint.StartsWith("COM")?client.OpenSerial(endpoint,5000,out status):client.OpenTcp(endpoint,5000,out status),"Nation connected");}
    static void Close(){if(client!=null){client.Close();client=null;Thread.Sleep(800);}}
    static T Send<T>(T m)where T:Message{client.SendSynMsg(m);Check(m.RtCode==0,m.GetType().Name+" default-timeout result="+m.RtCode);return m;}
    static void Get(byte q,byte s,string label){var m=Send(new MsgBaseGetBaseband());Check(m.QValue==q&&m.Session==s,label+" Q="+m.QValue+" Session="+m.Session+" Speed="+m.BaseSpeed);}
    static int Main(string[] args){endpoint=args[0];port=args[1];QueryParameters baseline=null;int result=0;
        try{
            using(var r=Native()){baseline=r.ReadQuery();Console.WriteLine("NATIVE BASELINE Q="+baseline.Q+" Session="+baseline.Session+" Profile="+r.ReadProfile());}
            Open();Get(baseline.Q,baseline.Session,"FIRST Get before any Set");Close();
            using(var r=Native())r.SetQuery(new QueryParameters(9,3),false);
            Open();Get(9,3,"FIRST Get after external ZK change");
            Send(new MsgBaseSetBaseband{QValue=0,IsPersistence=0});Get(0,3,"Q-only keeps native Session");Close();
            using(var r=Native()){var p=r.ReadQuery();Check(p.Q==0&&p.Session==3,"Independent ZK read confirms Nation Q Set");}
            Open();Send(new MsgBaseSetBaseband{Session=2,IsPersistence=0});Get(0,2,"Session-only keeps native Q");Close();
            using(var r=Native()){var p=r.ReadQuery();Check(p.Q==0&&p.Session==2,"Independent ZK read confirms Nation Session Set");}
            Open();foreach(byte s in new byte[]{0,1,2,3}){Send(new MsgBaseSetBaseband{QValue=15,Session=s,IsPersistence=0});Get(15,s,"Q upper bound and Session roundtrip");}Close();
            for(int n=0;n<3;n++){Open();Get(15,3,"FIRST Get after reconnect "+n);Close();}
        }catch(Exception e){result=1;Console.WriteLine("FAIL: "+e);}
        finally{Close();if(baseline!=null)try{using(var r=Native()){r.SetQuery(baseline,false);var p=r.ReadQuery();Check(p.Q==baseline.Q&&p.Session==baseline.Session,"RESTORED native Q/Session");}}catch(Exception e){result=2;Console.WriteLine("RESTORE FAILED: "+e);}}
        Console.WriteLine("CHECKS="+checks+" EXIT="+result);return result;
    }
}
