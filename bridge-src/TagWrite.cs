using System;
using System.Collections.Generic;
using System.IO;

namespace NationZkBridge {
    public sealed class WriteParameterException : IOException {
        public readonly byte Result;
        public WriteParameterException(byte result,string message):base(message){Result=result;}
    }
    public sealed class TagWrite {
        public uint Antennas;public byte Bank,FilterBank,FilterBits;
        public ushort Start,FilterAddress;
        public byte[] Data,Filter=new byte[0],Password=new byte[4];
        public static TagWrite Parse(byte[] payload,byte antennas){
            var c=new Cursor(payload);var r=new TagWrite{Antennas=c.U32(),Bank=c.U8(),Start=c.U16(),Data=c.Var()};
            if(antennas<1||antennas>4||r.Antennas==0||(r.Antennas&~((1u<<antennas)-1))!=0)throw new WriteParameterException(1,"Invalid write antenna mask");
            if(r.Bank>3||r.Data.Length==0||(r.Data.Length&1)!=0||r.Data.Length>128||(uint)r.Start+r.Data.Length/2>65536)throw new WriteParameterException(3,"Write supports 1..64 words in banks 0..3 without address wrap");
            var seen=new HashSet<byte>();
            while(c.Left>0){byte id=c.U8();if(!seen.Add(id))throw new InvalidDataException("Duplicate write option");
                switch(id){
                    case 1:
                        var filter=new Cursor(c.Var());r.FilterBank=filter.U8();r.FilterAddress=filter.U16();r.FilterBits=filter.U8();r.Filter=filter.Raw((r.FilterBits+7)/8);filter.End();
                        if(r.FilterBank<1||r.FilterBank>3||r.FilterBits==0||r.FilterAddress>16383||(uint)r.FilterAddress+r.FilterBits>16384)throw new WriteParameterException(2,"Invalid write selector");
                        break;
                    case 2:r.Password=c.Raw(4);break;
                    case 3:if(c.U8()!=0)throw new NotSupportedException("BlockWrite is not implemented; no tag written");break;
                    default:throw new NotSupportedException("Write option 0x"+id.ToString("X2")+" is not implemented; no tag written");
                }
            }
            return r;
        }
    }
    public interface ITagWriter {byte Write(TagWrite request);}
    // Transport boundary allows tests to verify native-call arguments without any RF.
    public interface IWriteTransport {
        byte GetAntennaMask();
        int SetAntennaMask(byte mask);
        int WriteData(TagWrite request,out int tagError);
    }
    public static class WriteExecutor {
        public static byte MapResult(int rc,int tagError){
            switch(rc){case 0:return 0;case 5:case 0x0c:return 8;case 0x10:return 7;case 0xfa:case 0xfb:return 10;case 0xf8:return 1;case 0xfd:case 0xff:return 3;case 0x0b:case 0x19:return 9;
                case 0xfc:switch(tagError){case 3:return 6;case 4:return 7;case 0x0b:return 5;default:return 9;}
                default:return 11;
            }
        }
        public static byte Execute(IWriteTransport transport,TagWrite request,byte antennas,Action<string> log){
            byte previous=0;bool attempted=false;
            try{
                if(antennas>1){previous=(byte)(transport.GetAntennaMask()&15);if(previous==0)return 11;
                    attempted=true;int selected=transport.SetAntennaMask((byte)(0x80|request.Antennas));if(selected!=0)return MapResult(selected,0);}
                int error;int rc=transport.WriteData(request,out error);
                log("WRITE bank="+request.Bank+" start="+request.Start+" words="+(request.Data.Length/2)+" antennas="+request.Antennas+" ZK=0x"+rc.ToString("X2")+" tagError=0x"+error.ToString("X2"));
                // Exactly one native write, including for lost replies or a selector changed by writing EPC.
                return MapResult(rc,error);
            }finally{
                if(attempted)try{int rc=transport.SetAntennaMask((byte)(0x80|previous));if(rc!=0)log("WARNING: write antenna restore failed ZK=0x"+rc.ToString("X2"));}catch(Exception e){log("WARNING: write antenna restore failed: "+e.Message);}
            }
        }
    }
}
