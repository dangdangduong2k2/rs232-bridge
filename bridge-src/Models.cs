using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace NationZkBridge {
    public sealed class ReaderInfo {
        public string Identity;
        public byte[] Version=new byte[2];
        public byte Antennas,MinPower,MaxPower,Power;
        // Only a verified translation is advertised. Empty list means unknown.
        public byte[] NationRegions=new byte[0];
    }
    public sealed class Inventory {
        public uint Antennas;public byte Mode;
        public byte TidMode,TidWords;
        public byte FilterBank,FilterBits;public ushort FilterAddress;
        public byte[] Filter=new byte[0],Password=new byte[4];
        public ushort UserAddress,ReservedAddress;
        public byte UserWords,ReservedWords;
        public static Inventory Parse(byte[] data,byte antennaCount) {
            var c=new Cursor(data);var r=new Inventory();r.Antennas=c.U32();r.Mode=c.U8();
            if(r.Antennas==0||(r.Antennas & ~((1u<<antennaCount)-1))!=0||r.Mode>1)
                throw new InvalidDataException("Invalid antenna selection or inventory mode");
            var seen=new HashSet<byte>();
            while(c.Left>0) {
                byte pid=c.U8();if(!seen.Add(pid))throw new InvalidDataException("Duplicate option");
                switch(pid) {
                    case 1:
                        var f=new Cursor(c.Var());r.FilterBank=f.U8();r.FilterAddress=f.U16();r.FilterBits=f.U8();r.Filter=f.Raw((r.FilterBits+7)/8);f.End();
                        if(r.FilterBank<1||r.FilterBank>3||r.FilterBits==0||r.FilterAddress>16383)throw new InvalidDataException("Invalid filter");break;
                    case 2:r.TidMode=c.U8();r.TidWords=c.U8();if(r.TidMode>1||r.TidWords==0||r.TidWords>32)throw new InvalidDataException("Invalid TID range (1..32 words)");break;
                    case 3:r.UserAddress=c.U16();r.UserWords=c.U8();if(r.UserAddress>255||r.UserWords==0||r.UserWords>32)throw new InvalidDataException("User range unsupported");break;
                    case 4:r.ReservedAddress=c.U16();r.ReservedWords=c.U8();if(r.ReservedAddress>255||r.ReservedWords==0||r.ReservedWords>32)throw new InvalidDataException("Reserved range unsupported");break;
                    case 5:r.Password=c.Raw(4);break;
                    default:throw new NotSupportedException("Inventory option 0x"+pid.ToString("X2")+" is not implemented");
                }
            }
            return r;
        }
    }
    public sealed class Tag {
        public byte[] Epc,Tid,User,Reserved;
        public ushort Pc;public byte Antenna,Rssi,Result;
        public byte[] ToNation() {
            var b=new Bytes().Var(Epc).U16(Pc).U8(Antenna).U8(1).U8(Rssi).U8(2).U8(Result);
            if(Tid!=null)b.U8(3).Var(Tid);
            if(User!=null)b.U8(4).Var(User);
            if(Reserved!=null)b.U8(5).Var(Reserved);
            return b.ToArray();
        }
    }
    public interface IReader : IDisposable {
        ReaderInfo Info {get;}
        byte ReadPower();
        void SetPower(byte power,bool persist);
        List<Tag> Scan(byte antenna,Inventory request,byte q,byte session,byte target,CancellationToken stop);
    }
    public sealed class SimReader : IReader,IRadioReader {
        readonly ReaderInfo info;
        byte[] powers;RadioRegion region=new RadioRegion(2,0,49);int profile=146;
        static byte simulatedQ=4,simulatedSession=0;
        public QueryParameters ReadQuery(){return new QueryParameters(simulatedQ,simulatedSession);}
        public void SetQuery(QueryParameters value,bool persist){value.Validate();simulatedQ=value.Q;simulatedSession=value.Session;}
        public SimReader(byte antennas){info=new ReaderInfo{Identity="SIMULATED-ZK-BRIDGE",Antennas=antennas,MinPower=0,MaxPower=30,Power=20,Version=new byte[]{0,1},NationRegions=RegionMap.Supported()};powers=new byte[antennas];for(int i=0;i<antennas;i++)powers[i]=20;}
        public byte[] ReadPowers(){return (byte[])powers.Clone();}
        public void SetPowers(byte[] values,bool persist,byte selectedMask=255){powers=(byte[])values.Clone();info.Power=powers[0];}
        public RadioRegion ReadRegion(){return new RadioRegion(region.Band,region.Min,region.Max);}
        public void SetRegion(RadioRegion value,bool persist){region=new RadioRegion(value.Band,value.Min,value.Max);}
        public int ReadProfile(){return profile;}
        public void SetProfile(int value,bool persist){profile=value;}
        public ReaderInfo Info {get{return info;}}
        public byte ReadPower(){return info.Power;}
        public void SetPower(byte p,bool persist){info.Power=p;}
        public List<Tag> Scan(byte antenna,Inventory r,byte q,byte session,byte target,CancellationToken stop) {
            stop.WaitHandle.WaitOne(35);stop.ThrowIfCancellationRequested();
            byte[] epc=new byte[]{0xE2,0x00,0x00,0x01,0x23,0x45,0x67,0x89,0x00,0x00,0x00,antenna};
            var t=new Tag{Epc=epc,Pc=0x3000,Antenna=antenna,Rssi=65};
            if(r.TidWords>0){t.Tid=new byte[r.TidWords*2];t.Tid[0]=0xE2;t.Tid[1]=0x80;}
            if(r.UserWords>0)t.User=new byte[r.UserWords*2];
            if(r.ReservedWords>0)t.Reserved=new byte[r.ReservedWords*2];
            return new List<Tag>{t};
        }
        public void Dispose(){}
    }
}
