using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace NationComPort {
    public class Settings {
        public string PhysicalPort, PhysicalId, NationPort, BridgePort;
        public int Baud=115200, Antennas=4, Pair=-1;
        public void Validate() {
            foreach(string p in new[]{PhysicalPort,NationPort,BridgePort})
                if(p==null||!Regex.IsMatch(p,@"\ACOM[1-9][0-9]{0,3}\z"))throw new InvalidDataException("Cổng COM không hợp lệ.");
            if(new[]{PhysicalPort,NationPort,BridgePort}.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=3)
                throw new InvalidDataException("Ba cổng COM phải khác nhau.");
            if(string.IsNullOrEmpty(PhysicalId)||Pair<0||Pair>999999||(Antennas!=1&&Antennas!=4)||Array.IndexOf(new[]{9600,19200,38400,57600,115200,921600},Baud)<0)
                throw new InvalidDataException("Cấu hình thiết bị không hợp lệ.");
        }
        public void Save(string file){Validate();using(var w=new StreamWriter(file))new XmlSerializer(typeof(Settings)).Serialize(w,this);}
        public static Settings Load(string file){using(var r=new StreamReader(file)){var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(r);s.Validate();return s;}}
    }
    public class Port {
        public string Name, Id, Description;
        public override string ToString(){return Name+" — "+Description;}
    }
    public static class Common {
        public const string ServiceName="NationZkComBridge";
        public static readonly string InstallRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"NationComPort");
        public static readonly string DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"NationComPort");
        public static readonly string Config=Path.Combine(InstallRoot,"settings.xml");
        public static string Arch {get{return Environment.Is64BitOperatingSystem?"x64":"x86";}}
        public static string Quote(string s){
            var b=new StringBuilder("\"");int slashes=0;
            foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
            b.Append('\\',slashes*2);b.Append('"');return b.ToString();
        }
        public static List<Port> Ports(){
            // Win32_SerialPort can omit software ports. Enumerate the Ports PnP class
            // and read each device's assigned PortName, including com0com COM# ports.
            var result=new List<Port>();
            using(var q=new ManagementObjectSearcher("SELECT DeviceID,Name,ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ClassGuid='{4D36E978-E325-11CE-BFC1-08002BE10318}'"))using(var rows=q.Get())
                foreach(ManagementObject row in rows){using(row){
                    if(Convert.ToInt32(row["ConfigManagerErrorCode"])!=0)continue;
                    string id=(string)row["DeviceID"];
                    if(string.IsNullOrEmpty(id))continue;
                    using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\"+id+@"\Device Parameters")){
                        string name=key==null?null:key.GetValue("PortName") as string;
                        if(name!=null&&Regex.IsMatch(name,@"\ACOM[1-9][0-9]{0,3}\z"))result.Add(new Port{Name=name,Id=id,Description=(string)row["Name"]});
                    }
                }}
            return result;
        }
        public static string Resolve(Settings s,IEnumerable<Port> ports){
            var matches=ports.Where(p=>string.Equals(p.Id,s.PhysicalId,StringComparison.OrdinalIgnoreCase)).ToArray();
            return matches.Length==1?matches[0].Name:null;
        }
        public static string Run(string exe,string args,int seconds,Action<string> log){
            var output=new StringBuilder();var sync=new object();
            using(var p=new Process()){
                p.StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(exe),RedirectStandardOutput=true,RedirectStandardError=true};
                DataReceivedEventHandler add=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null){lock(sync)output.AppendLine(e.Data);}};
                p.OutputDataReceived+=add;p.ErrorDataReceived+=add;
                p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
                if(!p.WaitForExit(seconds*1000)){try{p.Kill();}catch{}throw new TimeoutException(Path.GetFileName(exe)+" hết thời gian chờ; cần kiểm tra trạng thái trước khi thử lại.");}
                p.WaitForExit();string text=output.ToString();if(log!=null)log(text);
                if(p.ExitCode!=0)throw new IOException(Path.GetFileName(exe)+" lỗi "+p.ExitCode+". "+text);
                return text;
            }
        }
        public static Dictionary<string,string> PairPorts(string listing,int pair){
            var result=new Dictionary<string,string>();
            foreach(string side in new[]{"A","B"}){
                var m=Regex.Match(listing,@"(?m)^\s*CNC"+side+pair+@"\s+([^\r\n]+)");
                if(!m.Success)throw new InvalidDataException("Chưa tạo được cặp COM ảo. Windows có thể đã chặn driver hoặc đang cần khởi động lại.");
                var n=Regex.Match(m.Groups[1].Value,@"(?:^|,)RealPortName=(COM\d+)(?:,|$)");
                if(!n.Success)n=Regex.Match(m.Groups[1].Value,@"(?:^|,)PortName=(COM\d+)(?:,|$)");
                if(!n.Success)throw new InvalidDataException("Windows chưa cấp số COM cho CNC"+side+pair+".");
                result[side]=n.Groups[1].Value;
            }
            if(result["A"]==result["B"])throw new InvalidDataException("Hai đầu cặp COM bị trùng.");return result;
        }
        public static int FreePair(string listing){for(int n=0;n<1000000;n++)if(!Regex.IsMatch(listing,@"(?m)^\s*CNC[AB]"+n+@"\s"))return n;throw new IOException("Không còn vị trí COM ảo.");}
        public static void TestPair(string a,string b){
            using(var x=new SerialPort(a,115200))using(var y=new SerialPort(b,115200)){
                x.ReadTimeout=y.ReadTimeout=1800;x.WriteTimeout=y.WriteTimeout=1800;x.Open();y.Open();x.DiscardInBuffer();y.DiscardInBuffer();
                byte[] payload=Guid.NewGuid().ToByteArray();x.Write(payload,0,payload.Length);ReadExact(y,payload);Array.Reverse(payload);y.Write(payload,0,payload.Length);ReadExact(x,payload);
                CheckDsr(y,true);x.Close();CheckDsr(y,false);x.Open();CheckDsr(y,true);
            }
        }
        static void CheckDsr(SerialPort p,bool expected){for(int i=0;i<20;i++){if(p.DsrHolding==expected)return;Thread.Sleep(50);}throw new IOException("Cổng ảo không báo đúng trạng thái mở/đóng của Nation.");}
        static void ReadExact(SerialPort p,byte[] expected){for(int n=0;n<expected.Length;n++)if(p.ReadByte()!=expected[n])throw new IOException("Kiểm tra truyền dữ liệu COM ảo không đạt.");}
        public static void CheckNation(string port){
            using(var s=new SerialPort(port,115200)){
                s.ReadTimeout=250;s.WriteTimeout=1000;s.Open();s.DiscardInBuffer();
                byte[] request=new NationZkBridge.Frame(0x00010101,new byte[0]).Encode();s.Write(request,0,request.Length);
                var parser=new NationZkBridge.FrameParser();var buffer=new byte[256];var limit=DateTime.UtcNow.AddSeconds(6);
                while(DateTime.UtcNow<limit){int n;try{n=s.Read(buffer,0,buffer.Length);}catch(TimeoutException){continue;}
                    foreach(var f in parser.Feed(buffer,n))if(f.Control==0x00010101&&f.Data.Length==4&&f.Data[0]==0)return;
                }
                throw new IOException("Cổng đã tạo nhưng ZK chưa trả lời qua giao thức Nation. Kiểm tra cáp, baud và chương trình đang chiếm cổng ZK.");
            }
        }
        public static void SafeDirectory(string directory){
            var d=new DirectoryInfo(Path.GetFullPath(directory));
            while(d!=null){if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Không sử dụng thư mục liên kết: "+d.FullName);d=d.Parent;}
        }
        public static void FriendlyName(string deviceId,string name){
            IntPtr set=SetupDiCreateDeviceInfoList(IntPtr.Zero,IntPtr.Zero);if(set==new IntPtr(-1))throw new System.ComponentModel.Win32Exception();
            try{var data=new SP_DEVINFO_DATA();data.cbSize=(uint)Marshal.SizeOf(data);
                if(!SetupDiOpenDeviceInfo(set,deviceId,IntPtr.Zero,0,ref data))throw new System.ComponentModel.Win32Exception();
                byte[] bytes=Encoding.Unicode.GetBytes(name+"\0");
                if(!SetupDiSetDeviceRegistryProperty(set,ref data,12,bytes,(uint)bytes.Length))throw new System.ComponentModel.Win32Exception();
            }finally{SetupDiDestroyDeviceInfoList(set);}
        }
        [StructLayout(LayoutKind.Sequential)] struct SP_DEVINFO_DATA {public uint cbSize;public Guid ClassGuid;public uint DevInst;public IntPtr Reserved;}
        [DllImport("setupapi.dll",SetLastError=true)]static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr classGuid,IntPtr hwnd);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiOpenDeviceInfo(IntPtr set,string id,IntPtr hwnd,uint flags,ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiSetDeviceRegistryProperty(IntPtr set,ref SP_DEVINFO_DATA data,uint property,byte[] buffer,uint size);
        [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    }
}
