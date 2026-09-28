using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;
using NationZkBridge;

class WriteTests {
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
    static byte[] Hex(string s){s=s.Replace("-","");var b=new byte[s.Length/2];for(int n=0;n<b.Length;n++)b[n]=Convert.ToByte(s.Substring(n*2,2),16);return b;}
    sealed class Device:IReader,ITagWriter,IWriteTransport,IQueryReader {
        public QueryParameters ReadQuery(){return new QueryParameters(4,0);}
        public void SetQuery(QueryParameters value,bool persist){}
        public int Rc,TagError,Calls,SelectRc;public TagWrite Last;public readonly List<byte> Masks=new List<byte>();
        public readonly ManualResetEvent Scanning=new ManualResetEvent(false);
        public ReaderInfo Info {get{return new ReaderInfo{Identity="WRITE-TEST",Antennas=4,Version=new byte[]{2,8},MaxPower=30};}}
        public byte ReadPower(){return 22;}public void SetPower(byte p,bool persist){}
        public List<Tag> Scan(byte ant,Inventory i,byte q,byte s,byte t,CancellationToken c){Scanning.Set();c.WaitHandle.WaitOne(30);c.ThrowIfCancellationRequested();return new List<Tag>();}
        public byte Write(TagWrite r){return WriteExecutor.Execute(this,r,4,delegate(string s){});}
        public byte GetAntennaMask(){return 15;}
        public int SetAntennaMask(byte mask){Masks.Add(mask);return SelectRc;}
        public int WriteData(TagWrite r,out int error){Calls++;Last=r;error=TagError;return Rc;}
        public void Dispose(){}
    }
    static void Reject(byte[] data,Device d,string label){
        var output=new List<Frame>();int before=d.Calls;
        using(var bridge=new Bridge(d,output.Add,delegate(string s){},115200))bridge.Handle(new Frame(0x00010211,data));
        Check(d.Calls==before&&output.Count==1&&(output[0].Control==0x00010000||output[0].Data[0]!=0),label);
    }
    static int Main(){try{
        // Representative Nation GUI payload with synthetic tag identifiers. No COM or physical reader is opened.
        var captured=Hex("0000000F010001000E3000E2000000000000000000A95101001001002060E2000000000000000000A950");
        var parsed=TagWrite.Parse(captured,4);
        Check(parsed.Bank==1&&parsed.Start==1&&parsed.Data.Length==14&&parsed.Data[0]==0x30&&parsed.Antennas==15,"Representative GUI EPC payload includes original PC and all four antennas");
        Check(parsed.FilterBank==1&&parsed.FilterAddress==32&&parsed.FilterBits==96&&BitConverter.ToString(parsed.Filter).EndsWith("A9-50"),"Original EPC selector retained while new EPC ends A951");
        var d=new Device();
        Check(WriteExecutor.Execute(d,parsed,4,delegate(string s){})==0&&d.Calls==1&&d.Masks.Count==2&&d.Masks[0]==0x8f&&d.Masks[1]==0x8f,"One native write with temporary antenna selection and restore");
        d.Masks.Clear();parsed.Antennas=4;
        WriteExecutor.Execute(d,parsed,4,delegate(string s){});
        Check(d.Masks[0]==0x84&&d.Masks[1]==0x8f,"Selecting ANT3 does not enable unrequested antennas; original mask restored");
        int calls=d.Calls;d.Rc=0x30;
        Check(WriteExecutor.Execute(d,parsed,4,delegate(string s){})==11&&d.Calls==calls+1,"Lost/failed write reply never triggers a second write");
        calls=d.Calls;d.SelectRc=0xf8;
        Check(WriteExecutor.Execute(d,parsed,4,delegate(string s){})==1&&d.Calls==calls,"Antenna setup failure performs no write");d.SelectRc=0;d.Rc=0;
        var odd=new Bytes().U32(1).U8(3).U16(0).Var(new byte[]{1}).ToArray();Reject(odd,d,"Odd byte count rejected before hardware");
        Reject(new Bytes().U32(16).U8(3).U16(0).Var(new byte[]{1,2}).ToArray(),d,"Unsupported antenna rejected before hardware");
        Reject(new Bytes().U32(1).U8(3).U16(65535).Var(new byte[4]).ToArray(),d,"Address overflow rejected without truncation");
        Reject(new Bytes().U32(1).U8(3).U16(0).Var(new byte[130]).ToArray(),d,"Oversized write rejected without splitting into unsafe retries");
        Reject(new byte[]{0,0,0,1,3},d,"Truncated write rejected before hardware");
        Reject(new Bytes().Raw(captured).U8(1).Var(new byte[]{1,0,32,8,0xff}).ToArray(),d,"Duplicate selector rejected");
        Reject(new Bytes().Raw(captured).U8(2).Raw(new byte[]{0,0}).ToArray(),d,"Truncated password rejected");
        Reject(new Bytes().Raw(captured).U8(3).U8(1).ToArray(),d,"Unsupported BlockWrite rejected before hardware");
        Reject(new Bytes().Raw(captured).U8(99).U8(0).ToArray(),d,"Unknown write options are not silently discarded");
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;Exception serverError=null;
        var server=new Thread(delegate(){try{using(var socket=listener.AcceptTcpClient())using(var stream=socket.GetStream())using(var bridge=new Bridge(d,delegate(Frame f){byte[] wire=f.Encode();lock(stream){stream.Write(wire,0,wire.Length);}},delegate(string s){},115200)){
            var parser=new FrameParser();var buf=new byte[4096];int n;while((n=stream.Read(buf,0,buf.Length))>0)foreach(var f in parser.Feed(buf,n))bridge.Handle(f);
        }}catch(Exception e){serverError=e;}});server.IsBackground=true;server.Start();
        var client=new GClient();
        try{
            eConnectionAttemptEventStatusType state;Check(client.OpenTcp("127.0.0.1:"+port,2000,out state),"Original Nation SDK connects to write test endpoint");
            foreach(byte bank in new byte[]{0,1,2,3}){
                var msg=new MsgBaseWriteEpc{AntennaEnable=4,Area=bank,Start=bank==3?(ushort)300:(ushort)1,HexWriteData="1234ABCD",HexPassword="12345678",Filter=new ParamEpcFilter{Area=2,BitStart=7,BitLength=9,BData=new byte[]{0xab,0x80}}};
                calls=d.Calls;client.SendSynMsg(msg,2000);
                Check(msg.RtCode==0&&d.Calls==calls+1&&d.Last.Bank==bank&&d.Last.Start==(bank==3?300:1)&&BitConverter.ToString(d.Last.Data)=="12-34-AB-CD","Nation SDK write bank "+bank+" preserves word address and bytes");
                Check(d.Last.FilterBank==2&&d.Last.FilterAddress==7&&d.Last.FilterBits==9&&BitConverter.ToString(d.Last.Password)=="12-34-56-78","Nation SDK preserves partial-bit TID selector and password for bank "+bank);
            }
            var plain=new MsgBaseWriteEpc{AntennaEnable=1,Area=3,Start=0,HexWriteData="ABCD"};client.SendSynMsg(plain,2000);
            Check(plain.RtCode==0&&d.Last.FilterBits==0&&BitConverter.ToString(d.Last.Password)=="00-00-00-00","No-selector request keeps vendor unfiltered mode and default password");
            int[,] cases={{5,0,8},{0xfc,3,6},{0xfc,4,7},{0xfc,11,5},{0xfc,15,9},{0xfb,0,10},{0xfa,0,10},{0x30,0,11},{0xf8,0,1},{0xff,0,3}};
            for(int i=0;i<cases.GetLength(0);i++){
                d.Rc=cases[i,0];d.TagError=cases[i,1];calls=d.Calls;
                var msg=new MsgBaseWriteEpc{AntennaEnable=1,Area=3,Start=0,HexWriteData="ABCD"};client.SendSynMsg(msg,2000);
                Check(msg.RtCode==cases[i,2]&&d.Calls==calls+1,"SDK decodes ZK 0x"+d.Rc.ToString("X2")+" tag="+d.TagError+" as Nation "+msg.RtCode+" (no retry)");
            }
            d.Rc=0;d.TagError=0;
            var inv=new MsgBaseInventoryEpc{AntennaEnable=1,InventoryMode=1};client.SendSynMsg(inv,2000);Check(d.Scanning.WaitOne(1000),"Inventory active for write-busy check");calls=d.Calls;
            var busy=new MsgBaseWriteEpc{AntennaEnable=1,Area=3,Start=0,HexWriteData="ABCD"};client.SendSynMsg(busy,1000);
            Check(busy.RtCode!=0&&d.Calls==calls,"Write while inventory active cannot touch hardware");
            var stop=new MsgBaseStop();client.SendSynMsg(stop,2000);Check(stop.RtCode==0,"Stop remains functional after rejected write");
        }finally{client.Close();listener.Stop();}
        bool closed=server.Join(3000);var io=serverError as IOException;var reset=io==null?null:io.InnerException as SocketException;
        if(serverError!=null&&(reset==null||reset.SocketErrorCode!=SocketError.ConnectionReset))throw serverError;
        Check(closed,"Write test connection closes (Nation SDK may send TCP reset)");
        Console.WriteLine("ALL PASSED: "+checks+" write assertions; no physical tags modified");return 0;
    }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
}
