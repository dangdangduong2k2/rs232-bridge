using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace NationZkBridge {
    public sealed class Bridge : IDisposable {
        readonly IReader reader;readonly Action<Frame> send;readonly Action<string> log;
        readonly object deviceLock=new object();
        readonly DateTime started=DateTime.UtcNow;
        Thread inventoryThread;CancellationTokenSource cancellation;
        volatile bool disposed;
        readonly BridgeState state;
        byte q {get{return state.Active.Q;}} byte session {get{return state.Active.Session;}} byte target {get{return state.Active.Target;}}
        ushort duplicateUnits {get{return state.Active.Duplicate;}} byte rssiThreshold {get{return state.Active.Rssi;}}
        IRadioReader Radio {get{var r=reader as IRadioReader;if(r==null)throw new NotSupportedException("Radio configuration unavailable");return r;}}
        readonly int hostBaud;
        public Bridge(IReader r,Action<Frame> output,Action<string> logger,int baud):this(r,output,logger,baud,new BridgeState()){}
        public Bridge(IReader r,Action<Frame> output,Action<string> logger,int baud,BridgeState settings){reader=r;send=output;log=logger;hostBaud=baud;state=settings;}
        bool Running {get{return inventoryThread!=null&&inventoryThread.IsAlive;}}
        void Reply(Frame f,byte[] data){var response=new Frame(f.Control,data);response.Address=f.Address;send(response);}
        void ReplyCode(Frame f,byte code){Reply(f,new byte[]{code});}
        void Notice(Frame origin,int mid,byte[] data){var f=new Frame(0x00011200u|(uint)mid|(origin.Control&0x2000),data);f.Address=origin.Address;send(f);}
        void Error(Frame f,byte error){var b=new Bytes().U8(error).U8(Running?1:0).U16((int)f.Control).U16(f.Data.Length);var e=new Frame(0x00010000u|(f.Control&0x2000),b.ToArray());e.Address=f.Address;send(e);}
        void Empty(Frame f){if(f.Data.Length!=0)throw new InvalidDataException("Expected empty payload");}
        public void Handle(Frame f) {
            if(disposed)throw new ObjectDisposedException("Bridge");
            log("RX "+f.Control.ToString("X8")+" "+(f.Key==0x211?"write payload omitted (may contain access password)":BitConverter.ToString(f.Data)));
            if((f.Control>>24)!=0){Error(f,0);return;}
            if(((f.Control>>16)&255)!=1){Error(f,1);return;}
            // This release is RS232/TCP only. Do not pretend to support an RS485 bus.
            if((f.Control&0xF000)!=0){Error(f,4);return;}
            try {
                switch(f.Key) {
                    case 0x2ff:
                        Empty(f);Stop();ReplyCode(f,0);break;
                    case 0x100:
                        Empty(f);
                        var b=new Bytes().Text(reader.Info.Identity).U32((uint)(DateTime.UtcNow-started).TotalSeconds).Text("Nation-ZK bridge");
                        b.U8(1).U32(0x00010000).U8(2).Text("ZK compatibility bridge 0.5 RC3");Reply(f,b.ToArray());break;
                    case 0x101:
                        Empty(f);Reply(f,new byte[]{0,reader.Info.Version[0],reader.Info.Version[1],0});break;
                    case 0x103:
                        Empty(f);int baud=NationBaud(hostBaud);if(baud<0){Error(f,9);break;}Reply(f,new byte[]{(byte)baud});break;
                    case 0x112:
                        if(f.Data.Length!=4)throw new InvalidDataException("Heartbeat length");Reply(f,f.Data);break;
                    case 0x200:
                        Empty(f);var i=reader.Info;
                        Reply(f,new Bytes().U8(i.MinPower).U8(i.MaxPower).U8(i.Antennas).Var(i.NationRegions).Var(new byte[]{0}).ToArray());break;
                    case 0x202:
                        Empty(f);byte[] vector;lock(deviceLock){vector=Radio.ReadPowers();}
                        var powers=new Bytes();for(byte a=1;a<=vector.Length;a++)powers.U8(a).U8(vector[a-1]);Reply(f,powers.ToArray());break;
                    case 0x201:SetPower(f);break;
                    case 0x203:SetRegion(f);break;
                    case 0x204:
                        Empty(f);lock(deviceLock){Reply(f,new byte[]{CurrentRegion().Nation});}break;
                    case 0x205:SetFrequency(f);break;
                    case 0x206:GetFrequency(f);break;
                    case 0x20c:
                        Empty(f);lock(deviceLock){int actual=Radio.ReadProfile();byte speed=ProfileMap.FromZk(actual,state.Active.NationSpeed);var query=RefreshQuery();Reply(f,new byte[]{speed,query.Q,query.Session,target});}break;
                    case 0x20b:SetBaseband(f);break;
                    case 0x20a:
                        Empty(f);Reply(f,new Bytes().U16(duplicateUnits).U8(rssiThreshold).ToArray());break;
                    case 0x209:SetReporting(f);break;
                    case 0x210:
                        if(Running){Error(f,5);break;}
                        Inventory request;
                        try{request=Inventory.Parse(f.Data,reader.Info.Antennas);}
                        catch(InvalidDataException ex){log(ex.Message);ReplyCode(f,5);break;}
                        Start(f,request);break;
                    case 0x211:
                        if(Running){Error(f,5);break;}
                        var writer=reader as ITagWriter;if(writer==null){Error(f,3);break;}
                        try{var write=TagWrite.Parse(f.Data,reader.Info.Antennas);byte result;lock(deviceLock){result=writer.Write(write);}ReplyCode(f,result);}
                        catch(WriteParameterException ex){log(ex.Message);ReplyCode(f,ex.Result);}
                        catch(ZkException ex){log(ex.Message);ReplyCode(f,11);}
                        break;
                    default:
                        log("UNSUPPORTED command "+f.Key.ToString("X3")+" (no hardware operation performed)");Error(f,3);break;
                }
            } catch(NotSupportedException ex){log(ex.Message);Error(f,3);}
              catch(InvalidDataException ex){log(ex.Message);Error(f,7);}
              catch(ZkException ex){log(ex.Message);Error(f,9);}
              catch(IOException ex){log(ex.Message);Error(f,9);}
        }
        static int NationBaud(int baud){switch(baud){case 9600:return 0;case 19200:return 1;case 115200:return 2;case 230400:return 3;case 460800:return 4;default:return -1;}}
        QueryParameters RefreshQuery(){var source=reader as IQueryReader;if(source==null)throw new NotSupportedException("Native Q/Session unavailable");var value=source.ReadQuery();value.Validate();state.Commit(delegate(StateData d){d.Q=value.Q;d.Session=value.Session;},false);return value;}
        void SetPower(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);var ports=new Dictionary<byte,byte>();bool persist=true;bool hasPersistence=false;
            while(c.Left>0){byte a=c.U8(),p=c.U8();if(a==255){if(hasPersistence||p>1)throw new InvalidDataException("Power persistence");persist=p==1;hasPersistence=true;continue;}if(a<1||a>reader.Info.Antennas||ports.ContainsKey(a)){ReplyCode(f,1);return;}if(p<reader.Info.MinPower||p>reader.Info.MaxPower){ReplyCode(f,2);return;}ports.Add(a,p);}
            if(ports.Count==0){ReplyCode(f,1);return;}
            try{lock(deviceLock){var values=Radio.ReadPowers();byte selected=0;foreach(var entry in ports){values[entry.Key-1]=entry.Value;selected|=(byte)(1<<(entry.Key-1));}Radio.SetPowers(values,persist,selected);}ReplyCode(f,0);}
            catch(IOException ex){log(ex.Message);ReplyCode(f,3);}
        }
        RegionMap CurrentRegion(){return RegionMap.Resolve(Radio.ReadRegion(),state.Active.NationRegion);}
        void RememberRegion(RegionMap map,RadioRegion actual,int auto,bool persist){state.Commit(delegate(StateData d){d.NationRegion=map.Nation;d.RegionBand=actual.Band;d.RegionMin=actual.Min;d.RegionMax=actual.Max;d.FrequencyAuto=auto;},persist);}
        void SetRegion(Frame f){
            if(Running){Error(f,5);return;}var c=new Cursor(f.Data);byte band=c.U8();bool persist=true;
            if(c.Left>0){if(c.U8()!=1)throw new InvalidDataException("Band persistence PID");byte v=c.U8();if(v>1)throw new InvalidDataException("Band persistence");persist=v==1;}c.End();
            RegionMap map;try{map=RegionMap.Find(band);}catch(NotSupportedException ex){log(ex.Message);ReplyCode(f,1);return;}
            try{lock(deviceLock){var value=map.Full();Radio.SetRegion(value,persist);RememberRegion(map,value,1,persist);}ReplyCode(f,0);}catch(ZkException ex){log(ex.Message);ReplyCode(f,(byte)(ex.Code>=0xFD?1:2));}catch(IOException ex){log(ex.Message);ReplyCode(f,2);}
        }
        void SetFrequency(Frame f){
            if(Running){Error(f,5);return;}var c=new Cursor(f.Data);byte auto=c.U8();if(auto>1){ReplyCode(f,3);return;}bool persist=true;byte[] channels=null;var seen=new HashSet<byte>();
            while(c.Left>0){byte pid=c.U8();if(!seen.Add(pid))throw new InvalidDataException("Duplicate frequency option");switch(pid){case 1:channels=c.Var();break;case 2:byte v=c.U8();if(v>1)throw new InvalidDataException("Frequency persistence");persist=v==1;break;default:throw new NotSupportedException("Frequency parameter "+pid);}}
            if(auto==0&&(channels==null||channels.Length==0||channels.Length>50)){ReplyCode(f,2);return;}
            try{lock(deviceLock){var map=CurrentRegion();RadioRegion value;try{value=auto==1?map.Full():map.Select(channels);}catch(ArgumentException ex){log(ex.Message);ReplyCode(f,1);return;}Radio.SetRegion(value,persist);RememberRegion(map,value,auto,persist);}ReplyCode(f,0);}catch(IOException ex){log(ex.Message);ReplyCode(f,4);}
        }
        void GetFrequency(Frame f){
            Empty(f);lock(deviceLock){var actual=Radio.ReadRegion();var map=RegionMap.Resolve(actual,state.Active.NationRegion);var d=state.Active;
                bool matching=d.NationRegion==map.Nation&&d.RegionBand==actual.Band&&d.RegionMin==actual.Min&&d.RegionMax==actual.Max&&d.FrequencyAuto>=0;
                byte auto=(byte)(matching?d.FrequencyAuto:(actual.Same(map.Full())?1:0));Reply(f,new Bytes().U8(auto).Var(map.Channels(actual)).ToArray());}
        }
        void SetBaseband(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);byte nq=q,ns=session,nt=target;int speed=-1;bool persist=true;var seen=new HashSet<byte>();
            while(c.Left>0){byte p=c.U8(),v=c.U8();if(!seen.Add(p))throw new InvalidDataException("Duplicate baseband option");switch(p){
                case 1:try{ProfileMap.ToZk(v);}catch(NotSupportedException ex){log(ex.Message);ReplyCode(f,1);return;}speed=v;break;
                case 2:if(v>15){ReplyCode(f,2);return;}nq=v;break;
                case 3:if(v>3){ReplyCode(f,3);return;}ns=v;break;
                case 4:if(v>2){ReplyCode(f,4);return;}nt=v;break;
                case 255:if(v>1)throw new InvalidDataException("Baseband persistence");persist=v==1;break;
                default:throw new NotSupportedException("Baseband parameter "+p);
            }}
            try{lock(deviceLock){
                // Read before partial updates so settings changed in ZK are preserved.
                var query=Radio.ReadQuery();if(seen.Contains(2))query.Q=nq;if(seen.Contains(3))query.Session=ns;query.Validate();
                int zk=-1;if(speed>=0){zk=ProfileMap.ToZk((byte)speed);Radio.SetProfile(zk,persist);log("Nation EPC speed "+speed+" -> ZK profile "+zk+"; compatibility preset, see RADIO_MAPPING.md");}
                if(seen.Contains(2)||seen.Contains(3))Radio.SetQuery(query,persist);
                state.Commit(delegate(StateData d){if(seen.Contains(2))d.Q=nq;if(seen.Contains(3))d.Session=ns;if(seen.Contains(4))d.Target=nt;if(speed>=0){d.NationSpeed=speed;d.ZkProfile=zk;}},persist);}ReplyCode(f,0);
            }catch(ZkException ex){log(ex.Message);ReplyCode(f,(byte)(ex.Code>=0xFD?1:6));}catch(IOException ex){log(ex.Message);ReplyCode(f,6);}
        }
        void SetReporting(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);ushort nd=duplicateUnits;byte nr=rssiThreshold;bool persist=true;var seen=new HashSet<byte>();
            while(c.Left>0){byte p=c.U8();if(!seen.Add(p))throw new InvalidDataException("Duplicate report option");switch(p){case 1:nd=c.U16();break;case 2:nr=c.U8();break;case 255:byte v=c.U8();if(v>1)throw new InvalidDataException("Reporting persistence");persist=v==1;break;default:throw new NotSupportedException("Reporting parameter "+p);}}
            try{state.Commit(delegate(StateData d){if(seen.Contains(1))d.Duplicate=nd;if(seen.Contains(2))d.Rssi=nr;},persist);ReplyCode(f,0);}catch(IOException ex){log(ex.Message);ReplyCode(f,2);}
        }
        void Start(Frame origin,Inventory request) {
            Stop(); // Dispose a completed worker before the next inventory.
            lock(deviceLock){RefreshQuery();}
            cancellation=new CancellationTokenSource();CancellationToken token=cancellation.Token;
            inventoryThread=new Thread(delegate(){
                byte end=0;byte nextTarget=(byte)(target==2?0:target);var recent=new Dictionary<string,DateTime>();
                try {
                    do {
                        for(byte ant=1;ant<=reader.Info.Antennas;ant++) {
                            if((request.Antennas&(1u<<(ant-1)))==0)continue;
                            token.ThrowIfCancellationRequested();List<Tag> tags;
                            lock(deviceLock){tags=reader.Scan(ant,request,q,session,nextTarget,token);}
                            foreach(var tag in tags) {
                                token.ThrowIfCancellationRequested();if(tag.Rssi<rssiThreshold)continue;
                                string key=tag.Antenna+":"+BitConverter.ToString(tag.Epc)+":"+BitConverter.ToString(tag.Tid??new byte[0])+":"+BitConverter.ToString(tag.User??new byte[0])+":"+BitConverter.ToString(tag.Reserved??new byte[0])+":"+tag.Result;
                                if(duplicateUnits>0){DateTime previous;var now=DateTime.UtcNow;if(recent.TryGetValue(key,out previous)&&(now-previous).TotalMilliseconds<duplicateUnits*10)continue;if(recent.Count>=100000){var expired=new List<string>();foreach(var entry in recent)if((now-entry.Value).TotalMilliseconds>=duplicateUnits*10)expired.Add(entry.Key);foreach(var k in expired)recent.Remove(k);if(recent.Count>=100000)throw new IOException("Duplicate filter capacity reached");}recent[key]=now;}
                                Notice(origin,0,tag.ToNation());
                            }
                        }
                        if(target==2)nextTarget^=1;
                    }while(request.Mode==1&&!token.WaitHandle.WaitOne(5));
                    if(token.IsCancellationRequested)end=1;
                }catch(OperationCanceledException){end=1;}
                 catch(Exception ex){end=2;log("INVENTORY ERROR: "+ex.Message);}
                finally{try{Notice(origin,1,new byte[]{end});}catch(Exception ex){log("End report not delivered: "+ex.Message);}}
            });
            inventoryThread.IsBackground=true;inventoryThread.Name="ZK inventory";
            ReplyCode(origin,0);inventoryThread.Start();
        }
        public void Stop() {
            if(cancellation!=null)cancellation.Cancel();
            if(inventoryThread!=null&&inventoryThread.IsAlive&&!inventoryThread.Join(10000))
                throw new TimeoutException("ZK native call did not stop in 10 seconds. Restart the bridge; do not reuse this session.");
            inventoryThread=null;if(cancellation!=null)cancellation.Dispose();cancellation=null;
        }
        public void Dispose(){if(disposed)return;disposed=true;Stop();reader.Dispose();}
    }
}
