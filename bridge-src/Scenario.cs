using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace NationZkBridge {
    internal static partial class Native {
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int StartRead(ref byte address,byte target,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int StopRead(ref byte address,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int GetRfidTagData(ref byte address,[Out] byte[] data,ref int length,int handle);
    }
    public sealed class ScenarioParser {
        readonly List<byte> buffer=new List<byte>();readonly byte address;readonly uint antennas;
        public ScenarioParser(byte expectedAddress,uint selectedAntennas){address=expectedAddress;antennas=selectedAntennas;}
        public static ushort Crc(byte[] data){int crc=65535;foreach(byte b in data){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0x8408:0);}return (ushort)crc;}
        public List<Tag> Feed(byte[] data,int length){
            if(length<0||length>data.Length||buffer.Count+length>65536)throw new InvalidDataException("Invalid Scenario buffer length");
            for(int i=0;i<length;i++)buffer.Add(data[i]);var tags=new List<Tag>();
            while(buffer.Count>0){
                int size=buffer[0]+1;if(size<6)throw new InvalidDataException("Invalid Scenario frame length");
                if(buffer.Count<size)break;var raw=buffer.GetRange(0,size).ToArray();buffer.RemoveRange(0,size);
                if(Crc(raw)!=0||raw[1]!=address)throw new InvalidDataException("Scenario CRC/address mismatch");
                // Start/stop ACKs can coexist with asynchronous tag data in the DLL queue.
                if(raw[2]!=0xEE)continue;
                if(raw[3]!=0||size<11)throw new InvalidDataException("Invalid Scenario tag status");
                byte mask=raw[4],flags=raw[5];int n=flags&63;
                if(mask==0||(mask&(mask-1))!=0||(mask&antennas)==0||(flags&128)!=0||n<2||(n&1)!=0||size!=9+n+((flags&64)!=0?7:0))throw new InvalidDataException("Unsupported Scenario tag format/antenna");
                byte antenna=1;for(int bit=mask;bit>1;bit>>=1)antenna++;
                var epc=new byte[n];Array.Copy(raw,6,epc,0,n);tags.Add(new Tag{Epc=epc,Antenna=antenna,Rssi=raw[6+n]});
            }
            return tags;
        }
    }
    // Lifetime is exactly one Nation Start/Stop session. Never infer or persist PC.
    public sealed class ScenarioPcCache {
        readonly Dictionary<string,ushort> pc=new Dictionary<string,ushort>();
        sealed class Retry {public int Failures;public long Due;}
        readonly Dictionary<string,Retry> retries=new Dictionary<string,Retry>();
        readonly List<Tag> pending=new List<Tag>();readonly int capacity;
        public long Dropped {get;private set;}
        public int Pending {get{return pending.Count;}}
        public ScenarioPcCache(int limit){if(limit<1)throw new ArgumentOutOfRangeException("limit");capacity=limit;}
        static string Key(Tag t){return t.Antenna+":"+BitConverter.ToString(t.Epc);}
        public void Remember(Tag t){if(!t.PcKnown)throw new InvalidDataException("PC must be read from hardware");string key=Key(t);if(pc.Count>=65536&&!pc.ContainsKey(key))throw new IOException("Scenario PC cache capacity reached");pc[key]=t.Pc;retries.Remove(key);}
        public bool Apply(Tag t){ushort value;if(!pc.TryGetValue(Key(t),out value))return false;t.Pc=value;t.PcKnown=true;return true;}
        public void Queue(Tag t){if(pending.Count>=capacity){pending.RemoveAt(0);Dropped++;}pending.Add(t);}
        public List<Tag> Unknown(int limit,long now){
            if(limit<1)throw new ArgumentOutOfRangeException("limit");
            var result=new List<Tag>();var seen=new HashSet<string>();
            foreach(var t in pending){string key=Key(t);if(pc.ContainsKey(key)||!seen.Add(key))continue;Retry retry;if(result.Count<limit&&(!retries.TryGetValue(key,out retry)||now>=retry.Due))result.Add(t);}
            // Evicted observations must not leave an unbounded retry table behind.
            var expired=new List<string>();foreach(string key in retries.Keys)if(!seen.Contains(key))expired.Add(key);foreach(string key in expired)retries.Remove(key);
            return result;
        }
        public void Failed(Tag t,long now){string key=Key(t);Retry retry;if(!retries.TryGetValue(key,out retry)){if(retries.Count>=capacity)throw new IOException("Scenario retry capacity reached");retry=new Retry();retries[key]=retry;}retry.Failures=Math.Min(3,retry.Failures+1);retry.Due=now+(30000L<<(retry.Failures-1));}
        public List<Tag> Resolve(){var ready=new List<Tag>();for(int i=0;i<pending.Count;){if(Apply(pending[i])){ready.Add(pending[i]);pending.RemoveAt(i);}else i++;}return ready;}
    }
    public sealed partial class ZkReader {
        bool scenarioEnabled=true;
        public bool ScenarioEnabled {get{return scenarioEnabled;}set{scenarioEnabled=value;}}
        byte[] ReadCfg(byte number){var data=new byte[256];int length=0;Check("Get Scenario CFG"+number,Native.GetCfgParameter(ref address,number,data,ref length,handle));if(length<0||length>256)throw new InvalidDataException("Invalid CFG length");var result=new byte[length];Array.Copy(data,result,length);return result;}
        void SetCfgTemporary(byte number,byte[] data){Check("Set temporary Scenario CFG"+number,Native.SetCfgParameter(ref address,1,number,data,data.Length,handle));var actual=ReadCfg(number);if(BitConverter.ToString(actual)!=BitConverter.ToString(data))throw new IOException("Scenario CFG readback mismatch");}
        int FetchScenario(byte[] data){int length=0;int rc=Native.GetRfidTagData(ref address,data,ref length,handle);if(rc==0xFB)return 0;Check("Get Scenario tags",rc);return length;}
        public void StreamEpc(Inventory request,byte q,byte session,byte target,Action<Tag> report,CancellationToken stop){
            if(request.Mode!=1||request.TidWords!=0||request.UserWords!=0||request.ReservedWords!=0||request.FilterBits!=0)throw new NotSupportedException("Scenario requires continuous EPC-only without a tag mask");
            var oldTid=ReadCfg(10);var oldMask=ReadCfg(11);byte oldAnt=((IWriteTransport)this).GetAntennaMask();
            if(oldTid.Length!=2||oldMask.Length<4||oldAnt==0||(oldAnt&~15)!=0)throw new InvalidDataException("Unsupported Scenario configuration snapshot");
            bool tidChanged=false,maskChanged=false,antChanged=false,active=false;byte nextTarget=(byte)(target==2?0:target);
            var cache=new ScenarioPcCache(4096);long received=0,sent=0;var watch=Stopwatch.StartNew();
            Action<Tag> emit=delegate(Tag t){stop.ThrowIfCancellationRequested();report(t);sent++;};
            Action warm=delegate{
                for(byte antenna=1;antenna<=info.Antennas;antenna++)if((request.Antennas&(1u<<(antenna-1)))!=0){
                    antChanged=true;Check("PC warmup antenna",((IWriteTransport)this).SetAntennaMask((byte)(128|(1<<(antenna-1)))));
                    foreach(var tag in Scan(antenna,request,q,session,nextTarget,stop)){cache.Remember(tag);emit(tag);}
                }
                Check("Scenario selected antennas",((IWriteTransport)this).SetAntennaMask((byte)(128|request.Antennas)));
                foreach(var tag in cache.Resolve())emit(tag);
            };
            Func<List<Tag>,int> fillMissing=delegate(List<Tag> missing){
                int attempts=0;long began=watch.ElapsedMilliseconds;
                foreach(var tag in missing){
                    // A native transaction cannot be interrupted, but never start another
                    // once this pause has spent 200 ms reading PC (at most four candidates).
                    if(attempts>0&&watch.ElapsedMilliseconds-began>=200)break;
                    stop.ThrowIfCancellationRequested();antChanged=true;
                    attempts++;
                    Check("Missing PC antenna",((IWriteTransport)this).SetAntennaMask((byte)(128|(1<<(tag.Antenna-1)))));
                    int rc,error;var pc=ReadWords(tag,1,1,1,request.Password,out rc,out error);CheckTransport(rc);
                    if(pc!=null){tag.Pc=(ushort)((pc[0]<<8)|pc[1]);tag.PcKnown=true;cache.Remember(tag);}else cache.Failed(tag,watch.ElapsedMilliseconds);
                }
                Check("Resume Scenario antennas",((IWriteTransport)this).SetAntennaMask((byte)(128|request.Antennas)));
                foreach(var tag in cache.Resolve())emit(tag);
                return attempts;
            };
            try{
                // Scenario uses device-level TID/mask/antenna settings. Match the Nation request
                // temporarily and restore every changed setting, including on cancellation.
                if(oldTid[1]!=0){tidChanged=true;SetCfgTemporary(10,new byte[]{oldTid[0],0});}
                if(oldMask[3]!=0){maskChanged=true;SetCfgTemporary(11,new byte[]{0,0,0,0});}
                if(oldAnt!=(byte)request.Antennas){antChanged=true;Check("Scenario antennas",((IWriteTransport)this).SetAntennaMask((byte)(128|request.Antennas)));}
                warm();warm();
                var parser=new ScenarioParser(address,request.Antennas);var data=new byte[4096];
                long refreshAt=watch.ElapsedMilliseconds+2000,rotateAt=watch.ElapsedMilliseconds+1000,logAt=watch.ElapsedMilliseconds+5000;
                active=true;Check("Start Scenario",Native.StartRead(ref address,nextTarget,handle));
                log("SCENARIO started: EPC live; real PC reused only within this Start/Stop session");
                while(!stop.IsCancellationRequested){
                    int length=FetchScenario(data);
                    foreach(var tag in parser.Feed(data,length)){received++;if(cache.Apply(tag))emit(tag);else cache.Queue(tag);}
                    // A small bounded PC batch per pause. Repeated failures back off
                    // independently, so a weak tag cannot stop every reader antenna every 5 s.
                    List<Tag> missing=null;
                    if(cache.Pending>0&&watch.ElapsedMilliseconds>=refreshAt){missing=cache.Unknown(4,watch.ElapsedMilliseconds);refreshAt=watch.ElapsedMilliseconds+500;}
                    bool refresh=missing!=null&&missing.Count>0;
                    bool rotate=target==2&&session>0&&watch.ElapsedMilliseconds>=rotateAt;
                    if(refresh||rotate){
                        long pauseAt=watch.ElapsedMilliseconds;
                        Check("Stop Scenario for refresh",Native.StopRead(ref address,handle));active=false;
                        long stoppedAt=watch.ElapsedMilliseconds;
                        // GetRfidTagData is a blocking receive, not a nonblocking queue drain.
                        // Calling it after StopRead waits ~1.3 s for data that cannot arrive.
                        // All complete frames from the last receive were handled above.
                        if(rotate)nextTarget^=1;
                        int attempts=0;if(refresh){attempts=fillMissing(missing);refreshAt=watch.ElapsedMilliseconds+5000;}
                        parser=new ScenarioParser(address,request.Antennas);rotateAt=watch.ElapsedMilliseconds+1000;
                        stop.ThrowIfCancellationRequested();active=true;Check("Restart Scenario",Native.StartRead(ref address,nextTarget,handle));
                        log("SCENARIO pause: reason="+(refresh?"missing_pc":"target_rotation")+" attempts="+attempts+" stop_ms="+(stoppedAt-pauseAt)+" total_ms="+(watch.ElapsedMilliseconds-pauseAt));
                    }
                    if(watch.ElapsedMilliseconds>=logAt){log("SCENARIO counts: raw="+received+" emitted="+sent+" awaiting_pc="+cache.Pending+" overflow="+cache.Dropped+" elapsed_ms="+watch.ElapsedMilliseconds);logAt=watch.ElapsedMilliseconds+5000;}
                    if(length==0)stop.WaitHandle.WaitOne(2);
                }
            }finally{
                var errors=new List<string>();
                if(active)try{Check("Stop Scenario",Native.StopRead(ref address,handle));}catch(Exception e){errors.Add(e.Message);}
                if(antChanged)try{Check("Restore Scenario antennas",((IWriteTransport)this).SetAntennaMask((byte)(128|oldAnt)));}catch(Exception e){errors.Add(e.Message);}
                if(maskChanged)try{SetCfgTemporary(11,oldMask);}catch(Exception e){errors.Add(e.Message);}
                if(tidChanged)try{SetCfgTemporary(10,oldTid);}catch(Exception e){errors.Add(e.Message);}
                log("SCENARIO end: raw="+received+" emitted_including_warmup="+sent+" awaiting_pc="+cache.Pending+" overflow="+cache.Dropped+" elapsed_ms="+watch.ElapsedMilliseconds);
                if(errors.Count>0)throw new IOException("Scenario cleanup failed: "+string.Join("; ",errors.ToArray()));
            }
        }
    }
}
