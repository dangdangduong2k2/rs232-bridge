using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NationZkBridge;
class MixedInventoryTests {
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
    static byte[] Join(params byte[][] parts){var all=new List<byte>();foreach(var p in parts)all.AddRange(p);return all.ToArray();}
    static byte[] E(byte seq){return new byte[]{seq,4,0xE2,0,0x12,0x34,60};}
    static byte[] P(byte seq){return new byte[]{(byte)(128|seq),2,0x12,0x34,70};}
    static List<Tag> Parse(byte[] b,int n){return ZkReader.ParseMixedInventory(b,b.Length,n,3);}
    static void Reject(byte[] b,int n,string label){try{Parse(b,n);}catch(InvalidDataException){Check(true,label);return;}throw new Exception("Accepted "+label);}
    sealed class RepeatedReader:IReader,IQueryReader {
        public ReaderInfo Info {get{return new ReaderInfo{Antennas=1};}}
        public byte ReadPower(){return 20;}public void SetPower(byte p,bool persist){}
        public QueryParameters ReadQuery(){return new QueryParameters(2,0);}
        public void SetQuery(QueryParameters p,bool persist){}
        public List<Tag> Scan(byte a,Inventory r,byte q,byte s,byte t,CancellationToken c){var list=new List<Tag>();for(int i=0;i<100;i++)list.Add(new Tag{Epc=new byte[]{0xAB,0xCD},Pc=0x1234,Antenna=1,Rssi=60});return list;}
        public void Dispose(){}
    }
    static int Main(){try{
        var tags=Parse(Join(E(0),P(1),E(2),P(3)),4);
        Check(tags.Count==2,"Repeated EPC observations are not collapsed");
        Check(tags[0].PcKnown&&tags[1].PcKnown&&tags[0].Pc==0x1234,"PC comes from real memory, not inferred EPC length");
        Check(tags[0].Antenna==3&&tags[0].Rssi==60,"Antenna and EPC RSSI preserved");
        tags=Parse(Join(E(127),P(0)),2);Check(tags[0].PcKnown,"Sequence wraps at 127");
        tags=Parse(Join(E(0),E(1),P(2)),3);Check(!tags[0].PcKnown&&tags[1].PcKnown,"Missing PC cannot attach to previous EPC");
        tags=Parse(Join(E(0),P(3)),2);Check(!tags[0].PcKnown,"Non-adjacent PC is not associated");
        tags=Parse(Join(P(0),E(1)),2);Check(tags.Count==1&&!tags[0].PcKnown,"Orphan memory cannot attach to later EPC");
        tags=Parse(Join(E(0),P(1),P(2)),3);Check(tags.Count==1&&tags[0].Pc==0x1234,"Extra memory does not create a tag");
        var phase=Join(new byte[]{0,0x44,0xE2,0,0x12,0x34,60},new byte[7],P(1));
        Check(Parse(phase,2)[0].PcKnown,"Phase extension skipped at correct offset");
        Check(Parse(new byte[0],0).Count==0,"Empty response allowed");
        Reject(Join(E(0),P(1)),1,"Trailing packet rejected");
        Reject(new byte[]{0,4,0xE2,0,0x12,0x34},1,"Missing RSSI rejected");
        Reject(new byte[]{0,3,1,2,3,60},1,"Odd EPC length rejected");
        Reject(new byte[]{128,4,1,2,3,4,60},1,"Unexpected memory size rejected");
        Reject(new byte[]{0,0x84,1,2,3,4,60},1,"Unsupported length flags rejected");
        Reject(new byte[0],-1,"Negative packet count rejected");
        var ended=new ManualResetEvent(false);int forwarded=0;
        using(var bridge=new Bridge(new RepeatedReader(),delegate(Frame f){if(f.Control==0x00011200)Interlocked.Increment(ref forwarded);if(f.Control==0x00011201)ended.Set();},delegate(string s){},115200)){
            bridge.Handle(new Frame(0x00010210,new Bytes().U32(1).U8(0).ToArray()));
            Check(ended.WaitOne(2000),"Single inventory finishes");
            Check(forwarded==100,"All 100 repeated EPC reports reach Nation with duplicate filter disabled");
        }
        Console.WriteLine("ALL PASSED: "+checks+" mixed inventory assertions");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
}
