using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NationZkBridge {
    public static class Program {
        static volatile bool shutdown;
        static string stopFile;
        static bool Stopping { get { return shutdown || (stopFile != null && File.Exists(stopFile)); } }
        static StreamWriter logfile;
        static readonly object logLock=new object();
        static void Log(string s){lock(logLock){string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" "+s;Console.WriteLine(line);if(logfile!=null){logfile.WriteLine(line);logfile.Flush();}}}
        static Dictionary<string,string> ReadArgs(string[] args) {
            var r=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<args.Length;i++){if(args[i]=="--help"){r["help"]="true";continue;}if(args[i]=="--simulate"){r["simulate"]="true";continue;}if(!args[i].StartsWith("--")||i+1>=args.Length)throw new ArgumentException("Arguments are --name value");string key=args[i].Substring(2);if(r.ContainsKey(key))throw new ArgumentException("Duplicate argument "+key);r.Add(key,args[++i]);}
            foreach(string k in r.Keys)if(Array.IndexOf(new[]{"help","simulate","nation-com","nation-baud","listen","zk-com","zk-baud","antennas","max-power","log","stop-file","watch-peer"},k)<0)throw new ArgumentException("Unknown argument "+k);
            return r;
        }
        static string Get(Dictionary<string,string> a,string k,string d){string v;return a.TryGetValue(k,out v)?v:d;}
        static IReader OpenReader(bool simulation,string port,int baud,byte antennas,byte maxPower){return simulation?(IReader)new SimReader(antennas):new ZkReader(port,baud,antennas,maxPower,Log);}
        public static int Main(string[] args) {
            try {
                var a=ReadArgs(args);
                stopFile=Get(a,"stop-file",null);
                if(a.Count==0||a.ContainsKey("help")){Console.WriteLine("NationZkBridge 0.1 - keep Nation software and SDK unchanged\n\nHardware: NationZkBridge.exe --zk-com COM5 --zk-baud 115200 --antennas 4 --nation-com COM11\nTCP front end (ZK still COM): --zk-com COM5 --antennas 4 --listen 18160\nSimulation only: --simulate --antennas 4 --listen 18160\nOptional: --nation-baud 115200 --max-power 30 --log bridge.log\nCOM11 must be one side of an existing virtual null-modem pair. Select its OTHER side in Nation.\nTCP listens only on 127.0.0.1. No automatic physical COM probing. Ctrl+C stops.");return 0;}
                bool sim=a.ContainsKey("simulate");string port=Get(a,"zk-com",null),nationPort=Get(a,"nation-com",null);
                bool watchPeer=Get(a,"watch-peer","no")=="yes";
                if(a.ContainsKey("watch-peer")&&(nationPort==null||!watchPeer))throw new ArgumentException("--watch-peer yes requires --nation-com with DSR wired to remote-open");
                int baud=int.Parse(Get(a,"zk-baud","115200")),hostBaud=int.Parse(Get(a,"nation-baud","115200"));
                byte antennas=byte.Parse(Get(a,"antennas","4")),maxPower=byte.Parse(Get(a,"max-power","30"));
                if(antennas!=1&&antennas!=4)throw new ArgumentException("antennas must be 1 or 4");
                if(maxPower>33)throw new ArgumentException("max-power must be 0..33");
                if(!sim&&port==null)throw new ArgumentException("Hardware mode requires --zk-com");
                if(nationPort!=null&&string.Equals(nationPort,port,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Nation virtual COM and ZK physical COM must differ");
                if((nationPort!=null)==a.ContainsKey("listen"))throw new ArgumentException("Choose exactly one: --nation-com or --listen");
                if(!sim)ZkReader.BaudCode(baud);
                string logPath=Get(a,"log",null);if(logPath!=null)logfile=new StreamWriter(logPath,true);
                Console.CancelKeyPress+=delegate(object s,ConsoleCancelEventArgs e){e.Cancel=true;shutdown=true;};
                Log(sim?"SIMULATION ONLY - all tag data is artificial":"HARDWARE MODE - backend "+port+", "+baud+" baud");
                if(nationPort!=null) {
                    using(var serial=new SerialPort(nationPort,hostBaud,Parity.None,8,StopBits.One)) {
                        serial.Handshake=Handshake.None;serial.ReadTimeout=250;serial.WriteTimeout=2000;serial.Open();
                        Log("Nation front end: "+nationPort+" (select the OTHER virtual COM endpoint in Nation)");
                        do {
                            if(watchPeer&&!serial.DsrHolding){Thread.Sleep(100);continue;}
                            IReader reader=OpenReader(sim,port,baud,antennas,maxPower);
                            // Serve owns the backend; dropping peer-open cancels inventory and releases ZK.
                            Serve(delegate(byte[] b){if(watchPeer&&!serial.DsrHolding)return 0;return serial.Read(b,0,b.Length);},delegate(byte[] b){if(watchPeer&&!serial.DsrHolding)throw new IOException("Nation port closed");serial.Write(b,0,b.Length);},reader,hostBaud);
                            if(!watchPeer)break;
                            serial.DiscardInBuffer();serial.DiscardOutBuffer();
                        }while(!Stopping);
                    }
                } else {
                    int listen=int.Parse(a["listen"]);if(listen<1||listen>65535)throw new ArgumentException("listen port out of range");
                    var server=new TcpListener(IPAddress.Loopback,listen);server.Start();
                    try {Log("Nation TCP front end: 127.0.0.1:"+listen);while(!Stopping){if(!server.Pending()){Thread.Sleep(100);continue;}using(var client=server.AcceptTcpClient()){client.NoDelay=true;using(var stream=client.GetStream()){stream.ReadTimeout=250;stream.WriteTimeout=2000;IReader reader=OpenReader(sim,port,baud,antennas,maxPower);Serve(delegate(byte[] b){return stream.Read(b,0,b.Length);},delegate(byte[] b){stream.Write(b,0,b.Length);},reader,hostBaud);}}}}
                    finally {server.Stop();}
                }
                return 0;
            } catch(Exception ex){Log("FATAL: "+ex.Message);return 1;}
              finally{if(logfile!=null)logfile.Dispose();}
        }
        static bool IsReadTimeout(IOException ex){var socket=ex.InnerException as SocketException;return socket!=null&&socket.SocketErrorCode==SocketError.TimedOut;}
        static void Serve(Func<byte[],int> read,Action<byte[]> write,IReader reader,int hostBaud) {
            var parser=new FrameParser();var writeLock=new object();var buf=new byte[4096];
            using(var bridge=new Bridge(reader,delegate(Frame f){byte[] bytes=f.Encode();lock(writeLock){write(bytes);}if(f.Key!=0x200||(f.Control&0x1000)==0)Log("TX "+f.Control.ToString("X8")+" "+BitConverter.ToString(f.Data));},Log,hostBaud)) {
                try {while(!Stopping){int n;try{n=read(buf);}catch(TimeoutException){parser.Expire();continue;}catch(IOException ex){if(IsReadTimeout(ex)){parser.Expire();continue;}throw;}if(n==0)break;foreach(var f in parser.Feed(buf,n))bridge.Handle(f);}}
                catch(IOException ex){Log("Session disconnected: "+ex.Message);}
            }
            Log("Session closed");
        }
    }
}
