using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NationZkBridge {
    public sealed class Frame {
        public uint Control;
        public byte Address;
        public byte[] Data;
        public Frame(uint control, byte[] data) { Control=control; Data=data; }
        public bool Is485 { get { return (Control & 0x2000)!=0; } }
        public int Key { get { return (int)(Control & 0x0fff); } }
        public byte[] Encode() {
            var b=new Bytes(); b.U8(0x5a).U32(Control);
            if(Is485) b.U8(Address);
            b.Var(Data);
            byte[] part=b.ToArray();
            b.U16(Crc(part,1,part.Length-1)); return b.ToArray();
        }
        public static ushort Crc(byte[] b,int offset,int count) {
            int crc=0;
            for(int i=offset;i<offset+count;i++) {
                crc^=b[i]<<8;
                for(int j=0;j<8;j++) crc=((crc&0x8000)!=0 ? (crc<<1)^0x1021 : crc<<1)&0xffff;
            }
            return (ushort)crc;
        }
    }
    // Bounded stream parser: fragmentation, coalescing, noise and CRC recovery.
    public sealed class FrameParser {
        readonly List<byte> pending=new List<byte>();
        DateTime lastByte=DateTime.UtcNow;
        public void Expire() { if((DateTime.UtcNow-lastByte).TotalSeconds>2) pending.Clear(); }
        public List<Frame> Feed(byte[] bytes,int count) {
            Expire(); lastByte=DateTime.UtcNow;
            for(int i=0;i<count;i++) pending.Add(bytes[i]);
            var frames=new List<Frame>();
            while(pending.Count>0) {
                if(pending[0]!=0x5a) { pending.RemoveAt(0); continue; }
                if(pending.Count<7) break;
                bool rs485=(pending[3]&0x20)!=0;
                int lenAt=rs485?6:5;
                if(pending.Count<lenAt+2) break;
                int len=(pending[lenAt]<<8)|pending[lenAt+1];
                if(len>1024) { pending.RemoveAt(0); continue; }
                int total=lenAt+2+len+2;
                if(pending.Count<total) break;
                byte[] raw=pending.GetRange(0,total).ToArray();
                if(Frame.Crc(raw,1,total-3)!=((raw[total-2]<<8)|raw[total-1])) {pending.RemoveAt(0);continue;}
                uint control=((uint)raw[1]<<24)|((uint)raw[2]<<16)|((uint)raw[3]<<8)|raw[4];
                byte[] data=new byte[len];Array.Copy(raw,lenAt+2,data,0,len);
                var f=new Frame(control,data);if(rs485)f.Address=raw[5];
                frames.Add(f);pending.RemoveRange(0,total);
            }
            return frames;
        }
    }
    public sealed class Bytes {
        readonly List<byte> b=new List<byte>();
        public Bytes U8(int v){b.Add((byte)v);return this;}
        public Bytes U16(int v){return U8(v>>8).U8(v);}
        public Bytes U32(uint v){return U16((int)(v>>16)).U16((int)v);}
        public Bytes Raw(byte[] a){b.AddRange(a);return this;}
        public Bytes Var(byte[] a){return U16(a.Length).Raw(a);}
        public Bytes Text(string s){return Var(Encoding.ASCII.GetBytes(s));}
        public byte[] ToArray(){return b.ToArray();}
    }
    public sealed class Cursor {
        readonly byte[] b;int pos;
        public Cursor(byte[] data){b=data;}
        public int Left {get{return b.Length-pos;}}
        public byte U8(){if(Left<1)throw new InvalidDataException("Truncated parameter");return b[pos++];}
        public ushort U16(){return (ushort)((U8()<<8)|U8());}
        public uint U32(){return ((uint)U16()<<16)|U16();}
        public byte[] Raw(int n){if(n<0||Left<n)throw new InvalidDataException("Truncated data");var a=new byte[n];Array.Copy(b,pos,a,0,n);pos+=n;return a;}
        public byte[] Var(){return Raw(U16());}
        public void End(){if(Left!=0)throw new InvalidDataException("Unexpected parameter bytes");}
    }
}
