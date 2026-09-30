using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace NationZkBridge {
    internal static partial class Native {
        const string Dll="UHFReader288.dll";
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int OpenComPort(int port,ref byte address,byte baud,ref int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int CloseSpecComPort(int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int GetReaderInformation(ref byte address,[Out] byte[] version,ref byte type,ref byte protocols,ref byte maxFreq,ref byte minFreq,ref byte power,ref byte scan,ref byte ant,ref byte beep,ref byte output,ref byte check,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int SetRfPower(ref byte address,byte power,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int GetAntennaPower(ref byte address,[Out] byte[] powers,ref int length,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int SetAntennaPower(ref byte address,byte[] powers,int length,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int ExtGetRegion(ref byte address,ref byte band,ref byte max,ref byte min,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int ExtSetRegion(ref byte address,byte flag,byte band,byte max,byte min,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int SetExtProfile(ref byte address,byte option,ref int profile,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int GetCfgParameter(ref byte address,byte number,[Out] byte[] data,ref int length,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int SetCfgParameter(ref byte address,byte option,byte number,byte[] data,int length,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int SetAntennaMultiplexing(ref byte address,byte mask,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int WriteData_G2(ref byte address,byte[] epc,byte words,byte epcWords,byte memory,byte wordPointer,byte[] data,byte[] password,byte maskMem,byte[] maskAdr,byte maskLen,byte[] maskData,ref int tagError,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int ExtWriteData_G2(ref byte address,byte[] epc,byte words,byte epcWords,byte memory,byte[] wordPointer,byte[] data,byte[] password,byte maskMem,byte[] maskAdr,byte maskLen,byte[] maskData,ref int tagError,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int Inventory_G2(ref byte address,byte q,byte session,byte maskMem,byte[] maskAdr,byte maskLen,byte[] maskData,byte maskFlag,byte adrTid,byte lenTid,byte tidFlag,byte target,byte inAnt,byte scanTime,byte fastFlag,[Out] byte[] data,ref byte ant,ref int total,ref int count,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int InventoryMix_G2(ref byte address,byte q,byte session,byte maskMem,byte[] maskAdr,byte maskLen,byte[] maskData,byte maskFlag,byte readMem,byte[] readAdr,byte readLen,byte[] password,byte target,byte inAnt,byte scanTime,byte fastFlag,[Out] byte[] data,ref byte ant,ref int total,ref int count,int handle);
        [DllImport(Dll,CallingConvention=CallingConvention.StdCall)] internal static extern int ReadData_G2(ref byte address,byte[] epc,byte epcWords,byte memory,byte wordPointer,byte words,byte[] password,byte maskMem,byte[] maskAdr,byte maskLen,byte[] maskData,[Out] byte[] data,ref int tagError,int handle);
    }
    public sealed class ZkException : IOException {
        public readonly int Code;
        public ZkException(string operation,int code):base(operation+" returned ZK 0x"+code.ToString("X2")){Code=code;}
    }
    public sealed partial class ZkReader : IReader,ITagWriter,IWriteTransport,IRadioReader,IScenarioReader {
        byte address=255;int handle=-1;readonly ReaderInfo info;
        readonly Action<string> log;
        public static byte BaudCode(int baud) {
            switch(baud){case 9600:return 0;case 19200:return 1;case 38400:return 2;case 57600:return 5;case 115200:return 6;case 921600:return 7;default:throw new ArgumentException("ZK baud must be 9600,19200,38400,57600,115200 or 921600");}
        }
        public ZkReader(string port,int baud,byte antennas,byte maxPower,Action<string> logger) {
            log=logger;
            int number;if(!port.StartsWith("COM",StringComparison.OrdinalIgnoreCase)||!int.TryParse(port.Substring(3),out number)||number<1)throw new ArgumentException("Expected COM number");
            int rc=Native.OpenComPort(number,ref address,BaudCode(baud),ref handle);Check("OpenComPort",rc);
            try {
                byte[] version=new byte[2];byte type=0,protocols=0,max=0,min=0,power=0,scan=0,ant=0,beep=0,output=0,check=0;
                Check("GetReaderInformation",Native.GetReaderInformation(ref address,version,ref type,ref protocols,ref max,ref min,ref power,ref scan,ref ant,ref beep,ref output,ref check,handle));
                info=new ReaderInfo{Identity="ZK-"+port+"-TYPE"+type.ToString("X2"),Version=version,Antennas=antennas,MinPower=0,MaxPower=maxPower,Power=power};
                int band=((max&0xc0)>>4)|((min&0xc0)>>6);
                // Only documented matching band families; do not change regional RF settings.
                info.NationRegions=RegionMap.Supported();
                log("ZK connected: "+info.Identity+", firmware "+version[0]+"."+version[1]+", configured antennas="+antennas+", power="+power+", band="+band);
            } catch {Dispose();throw;}
        }
        static void Check(string op,int rc){if(rc!=0)throw new ZkException(op,rc);}
        public byte Write(TagWrite request){return WriteExecutor.Execute(this,request,info.Antennas,log);}
        byte IWriteTransport.GetAntennaMask(){
            byte[] version=new byte[2];byte type=0,protocols=0,max=0,min=0,power=0,scan=0,ant=0,beep=0,output=0,check=0;
            Check("GetReaderInformation before write",Native.GetReaderInformation(ref address,version,ref type,ref protocols,ref max,ref min,ref power,ref scan,ref ant,ref beep,ref output,ref check,handle));return ant;
        }
        int IWriteTransport.SetAntennaMask(byte mask){return Native.SetAntennaMultiplexing(ref address,mask,handle);}
        int IWriteTransport.WriteData(TagWrite r,out int error){
            error=0;var mask=new byte[32];Array.Copy(r.Filter,mask,r.Filter.Length);
            // ENum=255 invokes the exact EPC/TID/User bit selector supplied by Nation.
            // ENum=0 is the vendor's unfiltered single-write operation.
            byte epcWords=(byte)(r.FilterBits>0?255:0);var epc=new byte[512];
            byte[] maskAddress={(byte)(r.FilterAddress>>8),(byte)r.FilterAddress};
            if(r.Start<=255)return Native.WriteData_G2(ref address,epc,(byte)(r.Data.Length/2),epcWords,r.Bank,(byte)r.Start,r.Data,r.Password,r.FilterBank,maskAddress,r.FilterBits,mask,ref error,handle);
            return Native.ExtWriteData_G2(ref address,epc,(byte)(r.Data.Length/2),epcWords,r.Bank,new byte[]{(byte)(r.Start>>8),(byte)r.Start},r.Data,r.Password,r.FilterBank,maskAddress,r.FilterBits,mask,ref error,handle);
        }
        public ReaderInfo Info {get{return info;}}
        public byte ReadPower(){
            // ReadRfPower in this SDK reads the separate WRITE power, not inventory power.
            byte[] version=new byte[2];byte type=0,protocols=0,max=0,min=0,power=0,scan=0,ant=0,beep=0,output=0,check=0;
            Check("GetReaderInformation",Native.GetReaderInformation(ref address,version,ref type,ref protocols,ref max,ref min,ref power,ref scan,ref ant,ref beep,ref output,ref check,handle));
            info.Power=power;return power;
        }
        public void SetPower(byte power,bool persist){Check("SetRfPower",Native.SetRfPower(ref address,(byte)(power|(persist?0:0x80)),handle));info.Power=ReadPower();if(info.Power!=power)throw new IOException("ZK power readback mismatch");}
        public byte[] ReadPowers(){
            var buffer=new byte[256];int length=buffer.Length;Check("GetAntennaPower",Native.GetAntennaPower(ref address,buffer,ref length,handle));
            if(length!=info.Antennas)throw new InvalidDataException("Physical power vector does not match configured antenna count");
            var result=new byte[length];Array.Copy(buffer,result,length);foreach(byte p in result)if(p>info.MaxPower)throw new InvalidDataException("Power out of configured range");return result;
        }
        public void SetPowers(byte[] values,bool persist,byte selectedMask=255){
            if(values.Length!=info.Antennas)throw new ArgumentException("Power vector length");foreach(byte p in values)if(p>info.MaxPower)throw new ArgumentException("Power out of range");
            ReadPowers();var wire=(byte[])values.Clone();for(int i=0;i<wire.Length;i++)wire[i]|=(byte)(persist&&(selectedMask&(1<<i))!=0?0:128);
            Check("SetAntennaPower",Native.SetAntennaPower(ref address,wire,wire.Length,handle));var actual=ReadPowers();
            for(int i=0;i<actual.Length;i++)if(actual[i]!=values[i])throw new IOException("Antenna power readback mismatch; hardware may have changed");
        }
        public RadioRegion ReadRegion(){byte b=0,max=0,min=0;Check("ExtGetRegion",Native.ExtGetRegion(ref address,ref b,ref max,ref min,handle));if(min>max)throw new InvalidDataException("Invalid ZK region response");return new RadioRegion(b,min,max);}
        public void SetRegion(RadioRegion value,bool persist){
            if(value.Min>value.Max)throw new ArgumentException("Region range");ReadRegion();Check("ExtSetRegion",Native.ExtSetRegion(ref address,(byte)(persist?0:1),value.Band,value.Max,value.Min,handle));
            if(!ReadRegion().Same(value))throw new IOException("Region readback mismatch; hardware may have changed");
        }
        public int ReadProfile(){int p=0;Check("Get extended profile",Native.SetExtProfile(ref address,0,ref p,handle));if(p<0||p>65535)throw new InvalidDataException("Invalid extended profile");return p;}
        // Ex10 CFG9 is the native fast-query Q/Session pair. Legacy GetQS is
        // rejected by UHF7182M (0xEE); never fall back to cached Nation values.
        public QueryParameters ReadQuery(){
            var data=new byte[256];int length=0;
            Check("Get Q/Session CFG9",Native.GetCfgParameter(ref address,9,data,ref length,handle));
            if(length!=2)throw new InvalidDataException("Invalid ZK CFG9 length");
            var value=new QueryParameters(data[0],data[1]);value.Validate();return value;
        }
        public void SetQuery(QueryParameters value,bool persist){
            value.Validate();Check("Set Q/Session CFG9",Native.SetCfgParameter(ref address,(byte)(persist?0:1),9,new byte[]{value.Q,value.Session},2,handle));
            var actual=ReadQuery();if(actual.Q!=value.Q||actual.Session!=value.Session)throw new IOException("Q/Session readback mismatch; hardware may have changed");
        }
        public void SetProfile(int value,bool persist){
            if(value<0||value>65535)throw new ArgumentException("Profile ID");ReadProfile();int p=value;Check("Set extended profile",Native.SetExtProfile(ref address,(byte)(persist?1:2),ref p,handle));
            if(ReadProfile()!=value)throw new IOException("Profile readback mismatch; hardware may have changed");
        }
        // Documented synchronous buffer: length, EPC, RSSI; phase extension only when bit6 is set.
        public static List<Tag> ParseInventory(byte[] data,int total,int count,byte antenna) {
            if(total<0||total>data.Length||count<0||count>total/4)throw new InvalidDataException("Invalid ZK inventory bounds");
            var list=new List<Tag>();int offset=0;
            for(int i=0;i<count;i++) {
                if(offset>=total)throw new InvalidDataException("Missing ZK record");
                byte flags=data[offset++];int len=flags&0x3f;
                if((flags&0x80)!=0||len<2||(len&1)!=0||offset+len+1+((flags&0x40)!=0?7:0)>total)
                    throw new InvalidDataException("Malformed/unsupported ZK EPC record");
                byte[] epc=new byte[len];Array.Copy(data,offset,epc,0,len);offset+=len;
                list.Add(new Tag{Epc=epc,Antenna=antenna,Rssi=data[offset++]});
                if((flags&0x40)!=0)offset+=7;
            }
            if(offset!=total)throw new InvalidDataException("Unexpected ZK inventory tail");
            return list;
        }
        // Mixed inventory returns EPC/data packets with consecutive 7-bit sequence numbers.
        // Retain every EPC occurrence, even when the following PC read is missing.
        public static List<Tag> ParseMixedInventory(byte[] data,int total,int count,byte antenna) {
            if(total<0||total>data.Length||count<0||count>total/5)throw new InvalidDataException("Invalid ZK mixed inventory bounds");
            var tags=new List<Tag>();int offset=0,previous=-1;Tag pending=null;
            for(int i=0;i<count;i++) {
                if(offset+2>total)throw new InvalidDataException("Missing mixed inventory record");
                byte packet=data[offset++],flags=data[offset++];int length=flags&63;
                if((flags&128)!=0||length<2||(length&1)!=0||offset+length+1+((flags&64)!=0?7:0)>total)
                    throw new InvalidDataException("Malformed mixed inventory record");
                bool memory=(packet&128)!=0;int sequence=packet&127;
                if(memory) {
                    if(length!=2)throw new InvalidDataException("Mixed PC must contain exactly one word");
                    if(pending!=null&&sequence==((previous+1)&127)) {
                        pending.Pc=(ushort)((data[offset]<<8)|data[offset+1]);pending.PcKnown=true;
                    }
                    pending=null;
                } else {
                    var epc=new byte[length];Array.Copy(data,offset,epc,0,length);
                    pending=new Tag{Epc=epc,Antenna=antenna,Rssi=data[offset+length]};tags.Add(pending);
                }
                previous=sequence;offset+=length+1+((flags&64)!=0?7:0);
            }
            if(offset!=total)throw new InvalidDataException("Unexpected mixed inventory tail");
            return tags;
        }
        bool mixedUnsupported;
        byte[] ReadWords(Tag t,byte bank,byte start,byte words,byte[] password,out int rc,out int error) {
            var data=new byte[512];error=0;
            rc=Native.ReadData_G2(ref address,t.Epc,(byte)(t.Epc.Length/2),bank,start,words,password,0,new byte[2],0,new byte[32],data,ref error,handle);
            if(rc!=0)return null;
            var result=new byte[words*2];Array.Copy(data,result,result.Length);return result;
        }
        static byte TagError(int rc,int error){if(rc==5)return 5;if(rc==0xFB||rc==0xFA)return 1;if(rc==0xFC){if(error==3)return 4;if(error==4)return 3;return 6;}return 7;}
        static void CheckTransport(int rc){if(rc==0x30||rc==0x33||rc==0x35||rc==0x37)throw new ZkException("ReadData_G2 transport",rc);}
        public List<Tag> Scan(byte antenna,Inventory request,byte q,byte session,byte target,CancellationToken stop) {
            stop.ThrowIfCancellationRequested();
            byte ant=0;int total=0,count=0;var data=new byte[50000];
            var mask=new byte[32];Array.Copy(request.Filter,mask,request.Filter.Length);
            byte[] maskAddress={(byte)(request.FilterAddress>>8),(byte)request.FilterAddress};
            bool mixed=!mixedUnsupported;int rc;
            if(mixed) {
                rc=Native.InventoryMix_G2(ref address,q,session,request.FilterBank,maskAddress,request.FilterBits,mask,(byte)(request.FilterBits>0?1:0),1,new byte[]{0,1},1,request.Password,target,(byte)(0x80+antenna-1),2,1,data,ref ant,ref total,ref count,handle);
                // Only an explicit command/parameter rejection permits the legacy path.
                if(rc==0xFD||rc==0xFE||rc==0xEE){mixedUnsupported=true;mixed=false;total=0;count=0;ant=0;log("Mixed EPC/PC inventory rejected; using separate PC reads for this connection");}
            } else rc=0;
            if(!mixed)rc=Native.Inventory_G2(ref address,q,session,request.FilterBank,maskAddress,request.FilterBits,mask,(byte)(request.FilterBits>0?1:0),0,0,0,target,(byte)(0x80+antenna-1),2,1,data,ref ant,ref total,ref count,handle);
            if(rc==0xFB)return new List<Tag>();
            if(rc!=1&&rc!=2&&rc!=4)throw new ZkException(mixed?"InventoryMix_G2":"Inventory_G2",rc);
            if(count>0&&ant!=(byte)(1<<(antenna-1)))
                throw new InvalidDataException("ZK returned antenna mask 0x"+ant.ToString("X2")+" while antenna "+antenna+" was requested; refusing to mislabel tags");
            var tags=mixed?ParseMixedInventory(data,total,count,antenna):ParseInventory(data,total,count,antenna);
            var output=new List<Tag>();int pcReads=0,pcFailed=0;
            foreach(var t in tags) {
                stop.ThrowIfCancellationRequested();int error=0;
                // Read real PC bits; never fabricate them from EPC length.
                if(!t.PcKnown){
                    pcReads++;
                    byte[] pc=ReadWords(t,1,1,1,request.Password,out rc,out error);CheckTransport(rc);
                    if(pc==null){pcFailed++;continue;}
                    t.Pc=(ushort)((pc[0]<<8)|pc[1]);t.PcKnown=true;
                }
                if(request.TidWords>0) {
                    for(byte words=request.TidWords;words>0;words--) {
                        stop.ThrowIfCancellationRequested();
                        t.Tid=ReadWords(t,2,0,words,request.Password,out rc,out error);CheckTransport(rc);
                        if(rc==0)break;
                        // Adaptive TID shortens only on explicit memory-overrun, never on transport failure.
                        if(request.TidMode!=0||rc!=0xFC||error!=3)break;
                    }
                    if(rc!=0)t.Result=TagError(rc,error);
                }
                if(request.UserWords>0){stop.ThrowIfCancellationRequested();t.User=ReadWords(t,3,(byte)request.UserAddress,request.UserWords,request.Password,out rc,out error);CheckTransport(rc);if(rc!=0)t.Result=TagError(rc,error);}
                if(request.ReservedWords>0){stop.ThrowIfCancellationRequested();t.Reserved=ReadWords(t,0,(byte)request.ReservedAddress,request.ReservedWords,request.Password,out rc,out error);CheckTransport(rc);if(rc!=0)t.Result=TagError(rc,error);}
                output.Add(t);
            }
            if(pcFailed>0)log("INVENTORY PC: antenna="+antenna+" epc="+tags.Count+" separate_reads="+pcReads+" failed="+pcFailed+" returned="+output.Count);
            return output;
        }
        public void Dispose(){if(handle>=0){int h=handle;handle=-1;Native.CloseSpecComPort(h);}}
    }
}
