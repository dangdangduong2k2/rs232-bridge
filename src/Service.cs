using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace NationComPort {
    class HostService : ServiceBase {
        readonly ManualResetEvent stop=new ManualResetEvent(false);
        Thread worker;
        static readonly object logLock=new object();
        static string Logs {get{return Path.Combine(Common.DataRoot,"logs");}}
        public HostService(){ServiceName=Common.ServiceName;CanStop=true;AutoLog=false;}
        protected override void OnStart(string[] args){stop.Reset();worker=new Thread(Work){IsBackground=true};worker.Start();}
        protected override void OnStop(){stop.Set();RequestAdditionalTime(20000);if(worker!=null&&!worker.Join(18000))Log("Service shutdown exceeded deadline.");}
        static void Log(string text){try{lock(logLock){Directory.CreateDirectory(Logs);string path=Path.Combine(Logs,"service.log");if(File.Exists(path)&&new FileInfo(path).Length>2*1024*1024){string old=path+".1";if(File.Exists(old))File.Delete(old);File.Move(path,old);}File.AppendAllText(path,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}}catch{}}
        static void State(string text){try{File.WriteAllText(Path.Combine(Common.DataRoot,"status.txt"),DateTime.Now.ToString("s")+" "+text);}catch{}}
        void Work(){
            string stopFile=Path.Combine(Common.DataRoot,"worker.stop");
            try{
                Settings s=Settings.Load(Common.Config);string last=null;
                while(!stop.WaitOne(0)){
                    string port;
                    try{port=Common.Resolve(s,Common.Ports());}catch(Exception e){State("Không đọc được danh sách cổng: "+e.Message);stop.WaitOne(3000);continue;}
                    if(port==null){if(last!="missing"){State("Đang chờ module ZK đã cấu hình.");Log("Waiting for configured USB identity.");last="missing";}stop.WaitOne(3000);continue;}
                    last="starting";
                    if(File.Exists(stopFile))File.Delete(stopFile);
                    string exe=Path.Combine(Common.InstallRoot,"bin",Common.Arch,"NationZkBridge.exe");
                    string args="--zk-com "+port+" --zk-baud "+s.Baud+" --antennas "+s.Antennas+" --nation-com "+s.BridgePort+" --nation-baud 115200 --watch-peer yes --stop-file "+Common.Quote(stopFile)+" --state-file "+Common.Quote(Path.Combine(Common.DataRoot,"radio-state.xml"));
                    using(var child=new Process()){
                        child.StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(exe),RedirectStandardOutput=true,RedirectStandardError=true};
                        child.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;Log(e.Data);if(e.Data.Contains("Nation front end:"))State("Sẵn sàng: Nation COM Port ("+s.NationPort+") → "+port);if(e.Data.Contains("FATAL:"))State(e.Data);};
                        child.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)Log(e.Data);};
                        try{
                            State("Đang kết nối ZK "+port);child.Start();child.BeginOutputReadLine();child.BeginErrorReadLine();
                            while(!stop.WaitOne(2000)&&!child.HasExited){
                                try{if(Common.Resolve(s,Common.Ports())!=port){Log("USB removed or port changed.");break;}}catch(Exception e){Log("Device discovery: "+e.Message);}
                            }
                            if(!child.HasExited){File.WriteAllText(stopFile,"stop");if(!child.WaitForExit(12000)){Log("Native SDK did not stop within 12s; terminating this worker.");child.Kill();child.WaitForExit(2000);}}
                            child.WaitForExit();Log("Worker exited: "+child.ExitCode);
                        }catch(Exception e){Log(e.ToString());State("Lỗi kết nối: "+e.Message);try{if(!child.HasExited)child.Kill();}catch{}}
                    }
                    if(!stop.WaitOne(0)){State("Mất kết nối; tự thử lại sau 3 giây.");stop.WaitOne(3000);}
                }
            }catch(Exception e){Log(e.ToString());State("Lỗi cấu hình: "+e.Message);}
            finally{State("Đã dừng dịch vụ.");}
        }
        static void Main(){ServiceBase.Run(new HostService());}
    }
}
