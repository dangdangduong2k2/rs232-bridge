using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NationZkBridge;
class ProtocolTests {
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static void Reject(Action action,string label){try{action();}catch(InvalidDataException){Check(true,label);return;}throw new Exception("Accepted "+label);}
    sealed class FailingReader:IReader {
        public ReaderInfo Info {get{return new ReaderInfo{Antennas=4,Identity="test",MaxPower=30};}}
        public byte ReadPower(){return 22;}public void SetPower(byte p,bool persist){}
        public List<Tag> Scan(byte ant,Inventory i,byte q,byte s,byte t,CancellationToken c){throw new ZkException("test disconnect",0x30);}
        public void Dispose(){}
    }
    static int Main(){try{
        var stop=new Frame(0x000102ff,new byte[0]);byte[] wire=stop.Encode();
        Check(BitConverter.ToString(wire)=="5A-00-01-02-FF-00-00-88-5A","CRC/frame matches bytes emitted by original SDK");
        var parser=new FrameParser();var frames=new List<Frame>();foreach(byte b in wire)frames.AddRange(parser.Feed(new byte[]{b},1));Check(frames.Count==1&&frames[0].Key==0x2ff,"Byte-at-a-time serial fragmentation");
        byte[] pair=new byte[wire.Length*2];wire.CopyTo(pair,0);wire.CopyTo(pair,wire.Length);Check(new FrameParser().Feed(pair,pair.Length).Count==2,"Coalesced serial frames");
        var corrupt=(byte[])wire.Clone();corrupt[8]^=1;byte[] noise=new byte[corrupt.Length+wire.Length+1];noise[0]=0;corrupt.CopyTo(noise,1);wire.CopyTo(noise,1+corrupt.Length);Check(new FrameParser().Feed(noise,noise.Length).Count==1,"Noise and bad CRC recovery");
        var tooLarge=new byte[]{0x5a,0,1,2,0xff,0xff,0xff};byte[] recovery=new byte[tooLarge.Length+wire.Length];tooLarge.CopyTo(recovery,0);wire.CopyTo(recovery,tooLarge.Length);Check(new FrameParser().Feed(recovery,recovery.Length).Count==1,"Oversized frame rejected without losing next valid frame");
        var req=new Bytes().U32(1).U8(0).U8(2).U8(0).U8(6).ToArray();Check(Inventory.Parse(req,1).TidWords==6,"One-antenna request accepted");
        Reject(delegate{Inventory.Parse(new Bytes().U32(2).U8(0).ToArray(),1);},"Second antenna rejected for one-port configuration");
        Reject(delegate{Inventory.Parse(new byte[]{0,0},4);},"Truncated command rejected");
        Reject(delegate{Inventory.Parse(new Bytes().U32(1).U8(0).U8(2).U8(0).ToArray(),4);},"Truncated optional parameter rejected");
        Reject(delegate{Inventory.Parse(new Bytes().U32(1).U8(0).U8(3).U16(256).U8(1).ToArray(),4);},"Unsupported memory address is not truncated");
        byte[] records={4,0xE2,0,0x12,0x34,60,2,0xAB,0xCD,80};var tags=ZkReader.ParseInventory(records,records.Length,2,4);
        Check(tags.Count==2&&tags[0].Antenna==4&&tags[1].Rssi==80,"Native inventory EPC/RSSI bounds and antenna mapping");
        Reject(delegate{ZkReader.ParseInventory(records,records.Length-1,2,1);},"Truncated native buffer rejected");
        Reject(delegate{ZkReader.ParseInventory(records,records.Length,1,1);},"Unexpected native tail rejected");
        Check(ZkReader.BaudCode(115200)==6,"Nation 115200 maps to ZK baud code 6");
        var output=new List<Frame>();var sync=new object();var ended=new ManualResetEvent(false);
        using(var engine=new Bridge(new FailingReader(),delegate(Frame f){lock(sync){output.Add(f);}if(f.Control==0x00011201)ended.Set();},delegate(string s){},115200)){
            engine.Handle(new Frame(0x00010210,req));Check(ended.WaitOne(2000),"Hardware error terminates inventory");
            lock(sync){Check(output.Count==2&&output[0].Data[0]==0&&output[1].Data[0]==2,"Accepted request then explicit hardware-error end; no fake tags");}
        }
        output.Clear();
        using(var engine=new Bridge(new SimReader(1),delegate(Frame f){output.Add(f);},delegate(string s){},115200)){
            engine.Handle(new Frame(0x00010210,new Bytes().U32(2).U8(0).ToArray()));Check(output[0].Data[0]!=0,"Engine rejects nonexistent antenna before scanning");
            output.Clear();engine.Handle(new Frame(0x00010211,new byte[0]));Check(output[0].Control==0x00010000&&output[0].Data[0]==3,"Unimplemented write returns protocol error");
        }
        Console.WriteLine("ALL PASSED: "+checks+" protocol/state assertions");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
}
