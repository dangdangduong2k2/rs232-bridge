using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NationZkBridge;

class RadioTests {
    static int count;
    static void Assert(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
    sealed class Fake:IReader,IRadioReader {
        public ReaderInfo Info {get{return new ReaderInfo{Identity="fake",Antennas=4,MinPower=0,MaxPower=30,NationRegions=RegionMap.Supported()};}}
        public byte[] Values={8,9,10,11};public RadioRegion Region=new RadioRegion(2,0,49);public int Profile=146,Writes;public bool Persist,Fail;public byte Selected;
        public byte ReadPower(){return Values[0];}public void SetPower(byte p,bool persist){throw new Exception("Global power must not be used");}
        public byte[] ReadPowers(){return (byte[])Values.Clone();}
        public void SetPowers(byte[] p,bool persist,byte selectedMask=255){Writes++;Persist=persist;Selected=selectedMask;if(Fail)throw new IOException("readback mismatch");Values=(byte[])p.Clone();}
        public RadioRegion ReadRegion(){return Region;}
        public void SetRegion(RadioRegion r,bool persist){Writes++;Persist=persist;if(Fail)throw new IOException("readback mismatch");Region=r;}
        public int ReadProfile(){return Profile;}
        public byte Q=6,Session=1;public bool FailQuery;
        public QueryParameters ReadQuery(){if(FailQuery)throw new IOException("query read failed");return new QueryParameters(Q,Session);}
        public void SetQuery(QueryParameters p,bool persist){Writes++;Persist=persist;if(Fail)throw new IOException("query readback mismatch");Q=p.Q;Session=p.Session;}
        public void SetProfile(int p,bool persist){Writes++;Persist=persist;if(Fail)throw new IOException("readback mismatch");Profile=p;}
        public byte ScannedQ,ScannedSession;public readonly ManualResetEvent Scanned=new ManualResetEvent(false);
        public List<Tag> Scan(byte a,Inventory r,byte q,byte s,byte t,CancellationToken c){ScannedQ=q;ScannedSession=s;Scanned.Set();return new List<Tag>();}
        public void Dispose(){}
    }
    static Frame reply;static Frame Send(Bridge b,int key,params byte[] data){reply=null;b.Handle(new Frame(0x10000u+(uint)key,data));if(reply==null)throw new Exception("No response");return reply;}
    static void OK(Frame f,string label){Assert(f.Control!=0x10000&&f.Data.Length==1&&f.Data[0]==0,label);}
    static int Main(){string path=Path.Combine(Path.GetTempPath(),"bridge-radio-"+Guid.NewGuid()+".xml");try{
        var device=new Fake();var state=new BridgeState(path);
        using(var b=new Bridge(device,delegate(Frame f){reply=f;},delegate(string s){},115200,state)){
            var initial=Send(b,0x20c);Assert(BitConverter.ToString(initial.Data)=="01-06-01-02"&&device.Writes==0,"First Get reads native profile/Q/Session before any Set");
            device.Q=3;device.Session=2;initial=Send(b,0x20c);Assert(initial.Data[1]==3&&initial.Data[2]==2,"Get reflects external ZK changes instead of stale state");
            OK(Send(b,0x20b,2,5,255,0),"Q-only native Set");Assert(device.Q==5&&device.Session==2&&!device.Persist,"Q-only Set preserves native Session");
            OK(Send(b,0x20b,3,3,255,0),"Session-only native Set");Assert(device.Q==5&&device.Session==3,"Session-only Set preserves native Q");
            device.FailQuery=true;Assert(Send(b,0x20c).Control==0x10000,"Native query failure is not hidden by cached Q/Session");device.FailQuery=false;
            OK(Send(b,0x201,2,7,255,0),"Partial power accepted");Assert(device.Values[0]==8&&device.Values[1]==7&&device.Values[2]==10&&device.Values[3]==11&&!device.Persist&&device.Selected==2,"Only selected antenna changes, temporary flag preserved");
            var f=Send(b,0x202);Assert(BitConverter.ToString(f.Data)=="01-08-02-07-03-0A-04-0B","Get returns real per-antenna vector");
            int writes=device.Writes;Send(b,0x201,1,50);Send(b,0x201,1,8,1,9);Assert(device.Writes==writes,"Invalid/duplicate antenna power causes no native writes");
            device.Fail=true;Assert(Send(b,0x201,1,6).Data[0]!=0,"Power readback failure never reports success");device.Fail=false;
            OK(Send(b,0x203,0,1,0),"Nation China band Set");Assert(device.Region.Band==1&&device.Region.Min==2&&device.Region.Max==17&&!device.Persist,"Nation channel offsets map exact MHz");
            OK(Send(b,0x205,0,1,0,1,0,2,0),"Fixed channel Set");Assert(device.Region.Min==2&&device.Region.Max==2,"Nation channel zero is ZK channel two");
            f=Send(b,0x206);Assert(BitConverter.ToString(f.Data)=="00-00-01-00","Frequency Get roundtrip");
            writes=device.Writes;Assert(Send(b,0x205,0,1,0,2,0,2).Data[0]!=0,"Sparse channel list rejected");Assert(device.Writes==writes,"Sparse list does not broaden hardware range");
            Assert(Send(b,0x203,2).Data[0]!=0,"Dual-band request unsupported without hardware mutation");
            OK(Send(b,0x205,1,2,0),"Auto restores mapped full range");Assert(device.Region.Min==2&&device.Region.Max==17,"Auto stays within Nation band");
            foreach(byte speed in new byte[]{0,1,2,3,4,5,6,7,10,11,12,13,255}){OK(Send(b,0x20b,1,speed,255,0),"EPC speed Set "+speed);f=Send(b,0x20c);Assert(f.Data[0]==speed&&device.Profile==ProfileMap.ToZk(speed),"Profile actual readback "+speed);}
            writes=device.Writes;Send(b,0x20b,1,1,2,16);Assert(device.Writes==writes,"Validate entire baseband request before setting RF");
            OK(Send(b,0x20b,2,9,3,1,4,2,255,0),"Baseband FF00 supported");
            OK(Send(b,0x209,1,0,25,2,10,255,0),"Reporting FF00 supported");
            OK(Send(b,0x209,2,12),"Persist only supplied reporting field");
            var persisted=new BridgeState(path);Assert(persisted.Active.Rssi==12&&persisted.Active.Duplicate==0&&persisted.Active.Q==4,"Saving reporting does not accidentally persist temporary baseband/filter");
            device.Fail=true;Assert(Send(b,0x20b,1,3,2,7).Data[0]!=0&&state.Active.Q==9,"RF failure does not commit Q or profile hint");device.Fail=false;
            device.Profile=241;f=Send(b,0x20c);Assert(f.Data[0]==6,"External RF change invalidates old profile hint");
            device.Profile=65500;Assert(Send(b,0x20c).Control==0x10000,"Unmapped RF profile is not fabricated as Auto");device.Profile=146;
        }
        using(var b=new Bridge(device,delegate(Frame f){reply=f;},delegate(string s){},115200,state)){
            Assert(Send(b,0x20c).Data[1]==9,"Temporary baseband survives client reconnect");
            Assert(BitConverter.ToString(Send(b,0x20a).Data)=="00-19-0C","Reporting survives client reconnect");
            device.Q=2;device.Session=3;Send(b,0x210,0,0,0,1,0);
            Assert(device.Scanned.WaitOne(2000)&&device.ScannedQ==2&&device.ScannedSession==3,"Inventory refreshes native Q/Session even without prior Get or Set");b.Stop();
        }
        string blocked=path+".directory";Directory.CreateDirectory(blocked);
        try{var failedState=new BridgeState(blocked);
            using(var b=new Bridge(device,delegate(Frame f){reply=f;},delegate(string s){},115200,failedState)){
                var failedReply=Send(b,0x209,2,19);Assert(failedReply.Data[0]==2&&failedState.Active.Rssi==0,"Disk save failure returns error without changing active settings (code="+failedReply.Data[0]+", RSSI="+failedState.Active.Rssi+")");
            }
        }finally{Directory.Delete(blocked);}
        Console.WriteLine("ALL PASSED: "+count+" radio assertions");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}finally{if(File.Exists(path))File.Delete(path);}}
}
