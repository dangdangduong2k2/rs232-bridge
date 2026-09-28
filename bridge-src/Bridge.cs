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
        byte q=4,session=0,target=2;
        ushort duplicateUnits;byte rssiThreshold;
        readonly int hostBaud;
        public Bridge(IReader r,Action<Frame> output,Action<string> logger,int baud){reader=r;send=output;log=logger;hostBaud=baud;}
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
                        b.U8(1).U32(0x00010000).U8(2).Text("ZK compatibility bridge 0.4");Reply(f,b.ToArray());break;
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
                        Empty(f);byte power;lock(deviceLock){power=reader.ReadPower();}
                        var powers=new Bytes();for(byte a=1;a<=reader.Info.Antennas;a++)powers.U8(a).U8(power);Reply(f,powers.ToArray());break;
                    case 0x201:SetPower(f);break;
                    case 0x204:
                        Empty(f);if(reader.Info.NationRegions.Length!=1){Error(f,9);break;}Reply(f,reader.Info.NationRegions);break;
                    case 0x20c:
                        Empty(f);Reply(f,new byte[]{255,q,session,target});break;
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
        }
        static int NationBaud(int baud){switch(baud){case 9600:return 0;case 19200:return 1;case 115200:return 2;case 230400:return 3;case 460800:return 4;default:return -1;}}
        void SetPower(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);var ports=new Dictionary<byte,byte>();bool persist=true;bool hasPersistence=false;
            while(c.Left>0){byte a=c.U8(),p=c.U8();if(a==255){if(hasPersistence||p>1)throw new InvalidDataException("Power persistence");persist=p==1;hasPersistence=true;continue;}if(a<1||a>reader.Info.Antennas||ports.ContainsKey(a)){ReplyCode(f,1);return;}if(p<reader.Info.MinPower||p>reader.Info.MaxPower){ReplyCode(f,2);return;}ports.Add(a,p);}
            // ZK SetRfPower is global. Never silently change unrequested antennas.
            if(ports.Count!=reader.Info.Antennas){ReplyCode(f,1);return;}
            byte value=ports[1];foreach(byte p in ports.Values)if(p!=value){ReplyCode(f,2);return;}
            try{lock(deviceLock){reader.SetPower(value,persist);}ReplyCode(f,0);}
            catch(IOException ex){log(ex.Message);ReplyCode(f,3);}
        }
        void SetBaseband(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);byte nq=q,ns=session,nt=target;var seen=new HashSet<byte>();
            while(c.Left>0){byte p=c.U8(),v=c.U8();if(!seen.Add(p))throw new InvalidDataException("Duplicate baseband option");switch(p){case 1:if(v!=255){ReplyCode(f,1);return;}break;case 2:if(v>15){ReplyCode(f,2);return;}nq=v;break;case 3:if(v>3){ReplyCode(f,3);return;}ns=v;break;case 4:if(v>2){ReplyCode(f,4);return;}nt=v;break;default:throw new NotSupportedException("Baseband parameter "+p);}}
            q=nq;session=ns;target=nt;ReplyCode(f,0);
        }
        void SetReporting(Frame f) {
            if(Running){Error(f,5);return;}
            var c=new Cursor(f.Data);ushort nd=duplicateUnits;byte nr=rssiThreshold;var seen=new HashSet<byte>();
            while(c.Left>0){byte p=c.U8();if(!seen.Add(p))throw new InvalidDataException("Duplicate report option");switch(p){case 1:nd=c.U16();break;case 2:nr=c.U8();break;default:throw new NotSupportedException("Reporting parameter "+p);}}
            duplicateUnits=nd;rssiThreshold=nr;ReplyCode(f,0);
        }
        void Start(Frame origin,Inventory request) {
            Stop(); // Dispose a completed worker before the next inventory.
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
