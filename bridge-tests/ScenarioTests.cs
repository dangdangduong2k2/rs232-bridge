using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NationZkBridge;
class ScenarioTests {
    static int checks;
    static void Check(bool v,string label){if(!v)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static byte[] Hex(string s){var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
    static byte[] Wire(byte[] b){ushort crc=ScenarioParser.Crc(b);var result=new List<byte>(b);result.Add((byte)crc);result.Add((byte)(crc>>8));return result.ToArray();}
    static void Reject(byte[] data,string label){try{new ScenarioParser(0,2).Feed(data,data.Length);}catch(InvalidDataException){Check(true,label);return;}throw new Exception("Accepted "+label);}
    static Tag T(byte ant,byte rssi){return new Tag{Antenna=ant,Epc=new byte[]{0xAB,0xCD},Rssi=rssi};}
    sealed class Streaming: IReader,IQueryReader,IScenarioReader {
        public bool ScenarioEnabled {get{return true;}}
        public readonly ManualResetEvent Started=new ManualResetEvent(false);public bool Cleaned;
        public ReaderInfo Info{get{return new ReaderInfo{Antennas=1};}}
        public byte ReadPower(){return 20;}public void SetPower(byte p,bool persist){}
        public QueryParameters ReadQuery(){return new QueryParameters(2,0);}public void SetQuery(QueryParameters p,bool persist){}
        public List<Tag> Scan(byte a,Inventory r,byte q,byte s,byte t,CancellationToken c){throw new Exception("Unexpected batch path");}
        public void StreamEpc(Inventory r,byte q,byte s,byte t,Action<Tag> report,CancellationToken stop){try{for(int i=0;i<100;i++)report(T(1,60));Started.Set();stop.WaitHandle.WaitOne();stop.ThrowIfCancellationRequested();}finally{Cleaned=true;}}
        public void Dispose(){}
    }
    static int Main(){try{
        var golden=Hex("0a00ee000202abcd3c2386");Check(ScenarioParser.Crc(golden)==0,"Independent Python CRC fixture");
        var parser=new ScenarioParser(0,2);var tags=new List<Tag>();foreach(byte b in golden)tags.AddRange(parser.Feed(new byte[]{b},1));
        Check(tags.Count==1&&tags[0].Antenna==2&&tags[0].Rssi==60&&!tags[0].PcKnown,"Fragmented live frame preserves data and has no invented PC");
        var both=new List<byte>(golden);both.AddRange(golden);Check(new ScenarioParser(0,2).Feed(both.ToArray(),both.Count).Count==2,"Repeated observations stay separate");
        var corrupt=(byte[])golden.Clone();corrupt[6]^=1;Reject(corrupt,"Bad CRC rejected");
        Reject(Wire(Hex("0a01ee000202abcd3c")),"Wrong address rejected");
        Reject(Wire(Hex("0a00ee000302abcd3c")),"Ambiguous antenna mask rejected");
        Reject(Wire(Hex("0a00ee000102abcd3c")),"Unselected antenna rejected");
        Reject(Wire(Hex("0a00ee000282abcd3c")),"Unsupported FastID flags rejected");
        var phase=Wire(Hex("1100ee000242abcd3c00000000000000"));Check(new ScenarioParser(0,2).Feed(phase,phase.Length).Count==1,"Phase extension handled");
        var ack=Wire(Hex("05005000"));Check(new ScenarioParser(0,2).Feed(ack,ack.Length).Count==0,"ACK cannot become a tag");
        var cache=new ScenarioPcCache(2);var original=T(2,60);Check(!cache.Apply(original),"Unknown PC never fabricated");
        try{cache.Remember(original);throw new Exception("Accepted unknown PC");}catch(InvalidDataException){Check(true,"Only real PC may enter cache");}
        cache.Queue(T(2,61));cache.Queue(T(2,62));cache.Queue(T(2,63));Check(cache.Pending==2&&cache.Dropped==1,"Unknown queue is bounded with drop counter");
        original.Pc=0x1234;original.PcKnown=true;cache.Remember(original);
        var ready=cache.Resolve();Check(ready.Count==2&&ready[0].Pc==0x1234&&ready[0].Rssi==62&&ready[1].Rssi==63,"Pending observations keep their own RSSI and real PC");
        Check(!cache.Apply(T(1,60)),"PC cache separated by antenna");
        Check(!new ScenarioPcCache(2).Apply(T(2,60)),"Fresh session cannot reuse previous PC");
        var reader=new Streaming();int forwarded=0,endCode=-1;var frames=new List<Frame>();var sync=new object();var ended=new ManualResetEvent(false);
        using(var bridge=new Bridge(reader,delegate(Frame f){lock(sync){frames.Add(f);if(f.Control==0x00011200)forwarded++;if(f.Control==0x00011201){endCode=f.Data[0];ended.Set();}}},delegate(string s){},115200)){
            bridge.Handle(new Frame(0x00010210,new Bytes().U32(1).U8(1).ToArray()));Check(reader.Started.WaitOne(2000),"Scenario stream selected for continuous EPC");
            bridge.Handle(new Frame(0x00010202,new byte[0]));lock(sync){Check(frames[frames.Count-1].Data[0]==5,"Hardware query returns busy without interleaving/deadlock");}
            bridge.Handle(new Frame(0x000102ff,new byte[0]));Check(ended.WaitOne(2000)&&endCode==1&&reader.Cleaned,"Stop cancels and cleans native stream before reporting end");
            Check(forwarded==100,"All 100 stream observations forwarded without count inflation");
        }
        Console.WriteLine("ALL PASSED: "+checks+" Scenario assertions");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
}
