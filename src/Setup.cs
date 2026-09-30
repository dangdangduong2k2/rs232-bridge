using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Net;
using System.Security.Cryptography;

namespace NationComPort {
    static class SetupEngine {
        const string MicrosoftCabUrl="https://catalog.s.download.windowsupdate.com/d/msdownload/update/driver/drvs/2017/03/3a6be190-78d5-4d50-8a7d-b957789491d3_1e751599f18446e50375e85c981a9cb7215ba282.cab";
        const string MicrosoftCabSha256="C14225D86E4AD4A8414F7FB44F0014D7A8A1FD1993EC76CF79E5B2E7FE47DB51";
        static string Hash(string file){using(var h=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(h.ComputeHash(stream)).Replace("-","");}
        public static void PrepareMicrosoftDriver(string directory,Action<string> log){
            Common.SafeDirectory(directory);Directory.CreateDirectory(directory);
            string cache=Path.Combine(directory,"microsoft-catalog");Common.SafeDirectory(cache);Directory.CreateDirectory(cache);
            string cab=Path.Combine(cache,"com0com-x64.cab");
            if(!File.Exists(cab)||Hash(cab)!=MicrosoftCabSha256){
                if(log!=null)log("Downloading pinned driver from Microsoft Update Catalog.");
                ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
                using(var client=new WebClient())client.DownloadFile(MicrosoftCabUrl,cab);
            }
            if(Hash(cab)!=MicrosoftCabSha256)throw new InvalidDataException("SHA256 driver tải về không khớp Microsoft Catalog; chưa cài driver.");
            Common.Run(Path.Combine(Environment.SystemDirectory,"expand.exe"),"-F:* "+Common.Quote(cab)+" "+Common.Quote(cache),30,log);
            // The CAB also carries unrelated NLudp files. Never install them.
            foreach(string name in new[]{"com0com.inf","com0com.sys","com0com.cat","cncport.inf","cncport.cat","comport.inf","comport.cat","setup.dll"}){
                string target=Path.Combine(directory,name);
                if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked driver file.");
                File.Copy(Path.Combine(cache,name),target,true);
            }
            if(log!=null)log("Microsoft Catalog CAB SHA256 verified: "+MicrosoftCabSha256);
        }
        static readonly string Sc=Path.Combine(Environment.SystemDirectory,"sc.exe");
        static string Driver {get{return Path.Combine(Common.InstallRoot,"driver",Common.Arch,"setupc.exe");}}
        static readonly string Owner="NationComPort-package-0.2";
        static void RemoveFailedLegacyPackages(){
            // Only retry our failed 0.2 install. Never remove an in-use/shared package.
            EnsureOwned();
            using(var search=new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_PnPEntity"))using(var devices=search.Get())
                foreach(System.Management.ManagementObject device in devices){string id=Convert.ToString(device["PNPDeviceID"]);if(id.IndexOf("COM0COM",StringComparison.OrdinalIgnoreCase)>=0)throw new IOException("Vẫn có thiết bị com0com; không tự gỡ driver dùng chung.");}
            var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
                "D4E49D561800D1DAD11DACD803B21D85E04523EB15C85A28CF7C94E5867231EC",
                "2485CF4073418D6051C16289DAB47663C0D6D7FD1B02A1B7F7F4241B97F5A2ED",
                "E901239F3B05A952D2BB63FD9F30B6898521C4E943BBC1D03219CF09EE42A517"};
            string infRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"INF");
            foreach(string inf in Directory.GetFiles(infRoot,"oem*.inf"))if(known.Contains(Hash(inf))){
                Log("Removing unused legacy 0.2 package: "+Path.GetFileName(inf));
                Common.Run(Path.Combine(Environment.SystemDirectory,"pnputil.exe"),"/delete-driver "+Common.Quote(Path.GetFileName(inf)),45,Log);
            }
        }
        static void Log(string text){File.AppendAllText(Path.Combine(Common.DataRoot,"setup.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}
        static void Protect(string path,bool serviceWrite){
            Common.SafeDirectory(path);Directory.CreateDirectory(path);
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            var inherit=InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit;
            foreach(var id in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(id,null),FileSystemRights.FullControl,inherit,PropagationFlags.None,AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.ReadAndExecute,inherit,PropagationFlags.None,AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalServiceSid,null),serviceWrite?FileSystemRights.Modify:FileSystemRights.ReadAndExecute,inherit,PropagationFlags.None,AccessControlType.Allow));
            acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));Directory.SetAccessControl(path,acl);
        }
        public static void Extract(string root){
            Common.SafeDirectory(root);Directory.CreateDirectory(root);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){
                foreach(var entry in archive.Entries){
                    string target=Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
                    if(!target.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unsafe archive path.");
                    Common.SafeDirectory(Path.GetDirectoryName(target));
                    // .NET Framework's ZIP writer uses backslashes for directory entries.
                    if(entry.FullName.EndsWith("/")||entry.FullName.EndsWith("\\")){Common.SafeDirectory(target);Directory.CreateDirectory(target);continue;}
                    if(File.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked payload file: "+target);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=entry.Open())using(var output=File.Create(target))input.CopyTo(output);
                }
            }
        }
        static void StopService(){
            using(var sc=new ServiceController(Common.ServiceName)){
                try{var status=sc.Status;if(status!=ServiceControllerStatus.Stopped){sc.Stop();sc.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(22));}}
                catch(InvalidOperationException e){var win=e.InnerException as System.ComponentModel.Win32Exception;if(win==null||win.NativeErrorCode!=1060)throw;}
            }
        }
        static bool ServiceExists(string name){using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+name))return key!=null;}
        static void EnsureOwned(){
            Common.SafeDirectory(Common.InstallRoot);string marker=Path.Combine(Common.InstallRoot,"owner.txt");
            if(!File.Exists(marker)||File.ReadAllText(marker)!=Owner)throw new IOException("Không xác nhận được thư mục cài đặt do bộ cài này quản lý.");
        }
        public static string Install(string physical,int baud,int antennas){
            if(!Environment.Is64BitOperatingSystem)throw new NotSupportedException("Bản 0.5 hỗ trợ Windows x64; chưa đóng gói driver Microsoft cho x86.");
            if((Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")+Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")).IndexOf("ARM",StringComparison.OrdinalIgnoreCase)>=0)throw new NotSupportedException("Chưa hỗ trợ Windows ARM.");
            var device=Common.Ports().SingleOrDefault(p=>p.Name==physical);
            if(device==null||device.Id.StartsWith("COM0COM",StringComparison.OrdinalIgnoreCase))throw new IOException("Hãy chọn đúng cổng USB nối module ZK.");
            if((antennas!=1&&antennas!=4)||Array.IndexOf(new[]{9600,19200,38400,57600,115200,921600},baud)<0)throw new ArgumentException("Baud hoặc số anten không hợp lệ.");
            bool repair=File.Exists(Common.Config),owned=File.Exists(Path.Combine(Common.InstallRoot,"owner.txt"));
            if(Directory.Exists(Common.InstallRoot))EnsureOwned();
            else if(ServiceExists(Common.ServiceName))throw new IOException("Tên dịch vụ đã được sử dụng bởi cài đặt khác.");
            if(!repair&&!owned&&ServiceExists("com0com"))throw new IOException("Máy đã có com0com do phần mềm khác quản lý. Bộ cài này không tự thay driver/cặp cổng đó. Cần tích hợp với cài đặt hiện có trước.");
            if(File.Exists(Path.Combine(Common.InstallRoot,"pending-pair.txt")))throw new IOException("Lần cài trước bị gián đoạn khi tạo cặp COM. Cần kỹ thuật viên kiểm tra pending-pair.txt và setup.log trước khi thử lại.");
            Settings s=repair?Settings.Load(Common.Config):new Settings{PhysicalPort=physical,PhysicalId=device.Id,Baud=baud,Antennas=antennas};
            if(repair&&(s.PhysicalId!=device.Id||s.Baud!=baud||s.Antennas!=antennas))throw new IOException("Cấu hình đã cài khác lựa chọn này. Hãy gỡ bản cũ rồi cài lại để đổi thiết bị.");
            // Fail before stopping the working service or replacing any payload when
            // Nation still owns the front-end COM. Retrying later leaves it usable.
            if(repair){try{using(var port=new System.IO.Ports.SerialPort(s.NationPort,115200)){port.Open();}}
                catch(UnauthorizedAccessException e){throw new IOException("Close Nation / disconnect "+s.NationPort+" before setup. The installed service has not been changed.",e);}}
            Common.SafeDirectory(Common.DataRoot);
            if(!repair&&Directory.Exists(Common.DataRoot)&&!File.Exists(Path.Combine(Common.InstallRoot,"owner.txt")))throw new IOException("Thư mục dữ liệu đã tồn tại nhưng không có dấu cài đặt. Cần kỹ thuật viên kiểm tra trước.");
            Protect(Common.InstallRoot,false);Protect(Common.DataRoot,true);File.WriteAllText(Path.Combine(Common.InstallRoot,"owner.txt"),Owner);
            Log("Install/repair requested: "+physical+" baud="+baud+" antennas="+antennas);
            bool pairCreated=false,serviceCreated=false;
            try{
                if(repair)StopService();Extract(Common.InstallRoot);
                string me=Assembly.GetExecutingAssembly().Location,target=Path.Combine(Common.InstallRoot,"NationComPortSetup.exe");
                if(!string.Equals(me,target,StringComparison.OrdinalIgnoreCase))File.Copy(me,target,true);
                if(!repair){
                    if(ServiceExists("com0com")){
                        string existing=Common.Run(Driver,"--silent list",15,Log);
                        if(System.Text.RegularExpressions.Regex.IsMatch(existing,@"(?m)^\s*CNC[AB]\d+\s"))throw new IOException("Đã có cặp com0com khác. Không tự cập nhật driver dùng chung.");
                    }
                    PrepareMicrosoftDriver(Path.GetDirectoryName(Driver),Log);
                    if(owned)RemoveFailedLegacyPackages();
                    Common.Run(Driver,"--silent --wait 30 preinstall",60,Log);
                    string listing=Common.Run(Driver,"--silent list",15,Log);s.Pair=Common.FreePair(listing);
                    // Record ownership before mutation so interrupted installs can be diagnosed.
                    File.WriteAllText(Path.Combine(Common.InstallRoot,"pending-pair.txt"),s.Pair.ToString());
                    pairCreated=true;
                    Common.Run(Driver,"--silent --wait 30 install "+s.Pair+" PortName=COM#,EmuOverrun=yes PortName=COM#,EmuOverrun=yes,dsr=ropen",60,Log);
                    Dictionary<string,string> ports=null;Exception last=null;
                    for(int attempt=0;attempt<15;attempt++){try{ports=Common.PairPorts(Common.Run(Driver,"--silent list",15,Log),s.Pair);break;}catch(InvalidDataException ex){last=ex;Thread.Sleep(1000);}}
                    if(ports==null)throw new IOException("Driver chưa tạo cổng COM. Cần xem CodeIntegrity/SetupAPI để xác định lỗi nạp; có thể Windows đang cần khởi động lại.",last);
                    s.NationPort=ports["A"];s.BridgePort=ports["B"];s.Validate();
                }
                else{
                    PrepareMicrosoftDriver(Path.GetDirectoryName(Driver),Log);
                    var ports=Common.PairPorts(Common.Run(Driver,"--silent list",15,Log),s.Pair);
                    if(ports["A"]!=s.NationPort||ports["B"]!=s.BridgePort)throw new IOException("Cặp COM đã bị thay đổi bên ngoài bộ cài; không tự sửa.");
                }
                Common.TestPair(s.NationPort,s.BridgePort);Log("Virtual COM pair bidirectional test passed.");
                var all=Common.Ports();
                foreach(var p in new[]{s.NationPort,s.BridgePort}){
                    var found=all.SingleOrDefault(x=>x.Name==p&&x.Id.StartsWith("COM0COM",StringComparison.OrdinalIgnoreCase));
                    if(found==null)throw new IOException("Không nhận diện được cổng COM ảo "+p+" trong Windows.");
                    Common.FriendlyName(found.Id,(p==s.NationPort?"Nation COM Port":"Nation Bridge Internal")+" ("+p+")");
                }
                s.Save(Common.Config);
                string service=Path.Combine(Common.InstallRoot,"NationComService.exe");
                if(!ServiceExists(Common.ServiceName)){
                    Common.Run(Sc,"create "+Common.ServiceName+" binPath= "+Common.Quote(Common.Quote(service))+" start= auto obj= \"NT AUTHORITY\\LocalService\" DisplayName= \"Nation COM Port - ZK Bridge\"",20,Log);serviceCreated=true;
                }
                Common.Run(Sc,"description "+Common.ServiceName+" \"Nation RS232 protocol to configured ZK USB reader\"",15,Log);
                Common.Run(Sc,"failure "+Common.ServiceName+" reset= 86400 actions= restart/5000/restart/10000/restart/30000",15,Log);
                using(var sc=new ServiceController(Common.ServiceName)){sc.Start();sc.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(15));}
                Exception failure=null;
                for(int n=0;n<5;n++){try{Common.CheckNation(s.NationPort);failure=null;break;}catch(Exception e){failure=e;Thread.Sleep(1500);}}
                if(failure!=null)throw failure;
                Thread.Sleep(500);
                Common.Run(Path.Combine(Common.InstallRoot,"diagnostics","NationSerialCheck.exe"),"--serial "+s.NationPort+":115200",55,Log);
                using(var k=Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NationComPort")){
                    k.SetValue("DisplayName","Nation COM Port (ZK bridge)");k.SetValue("DisplayVersion","0.5 RC4");k.SetValue("InstallLocation",Common.InstallRoot);k.SetValue("UninstallString",Common.Quote(target)+" /uninstall");k.SetValue("NoModify",1);k.SetValue("NoRepair",1);
                }
                string pending=Path.Combine(Common.InstallRoot,"pending-pair.txt");if(File.Exists(pending))File.Delete(pending);
                Log("PASS: original Nation protocol returned real reader firmware through "+s.NationPort);
                return "Setup complete.\r\n\r\nNation COM Port ("+s.NationPort+")\r\nRS232 · 115200";
            }catch(Exception e){
                Log("FAILED: "+e);
                if(repair){try{StopService();}catch(Exception cleanup){Log("Could not stop failed repair: "+cleanup);}}
                if(!repair){
                    try{if(serviceCreated){StopService();Common.Run(Sc,"delete "+Common.ServiceName,15,Log);}}catch(Exception cleanup){Log("Service cleanup failed: "+cleanup);}
                    try{if(pairCreated){Common.Run(Driver,"--silent --wait 20 remove "+s.Pair,35,Log);string pending=Path.Combine(Common.InstallRoot,"pending-pair.txt");if(File.Exists(pending))File.Delete(pending);}}catch(Exception cleanup){Log("Pair cleanup failed: "+cleanup);}
                    if(File.Exists(Common.Config))File.Delete(Common.Config);
                }
                throw new IOException("Setup failed.\r\n"+e.Message+"\r\n\r\nLog: "+Path.Combine(Common.DataRoot,"setup.log"),e);
            }
        }
        public static string Uninstall(){
            EnsureOwned();Settings s=File.Exists(Common.Config)?Settings.Load(Common.Config):null;
            if(s==null)throw new IOException("Chưa có cấu hình hoàn tất. Kiểm tra setup.log/pending-pair.txt trước khi dọn cài đặt lỗi.");
            var ports=Common.PairPorts(Common.Run(Driver,"--silent list",15,Log),s.Pair);
            if(ports["A"]!=s.NationPort||ports["B"]!=s.BridgePort)throw new IOException("Cặp COM không còn khớp; không xóa cặp khác.");
            StopService();if(ServiceExists(Common.ServiceName))Common.Run(Sc,"delete "+Common.ServiceName,15,Log);
            Common.Run(Driver,"--silent --wait 20 remove "+s.Pair,35,Log);
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NationComPort",false);
            File.Delete(Common.Config);Log("Uninstalled owned service and COM pair. Driver store and diagnostic files retained.");
            return "Uninstall complete.\r\nDriver files and logs retained.";
        }
    }
    class SetupForm : Form {
        const string Caption="driver RS232 brigde";
        public SetupForm(){
            Text=Caption;ClientSize=new Size(420,180);Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;
            Controls.Add(new Label{Text=Caption,Font=new Font("Segoe UI",20,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,Location=new Point(20,27),Size=new Size(380,55)});
            var setup=new Button{Text="setup",Location=new Point(88,115),Size=new Size(112,36)};
            var cancel=new Button{Text="cancel",Location=new Point(220,115),Size=new Size(112,36),DialogResult=DialogResult.Cancel};
            setup.Click+=delegate{Apply();};cancel.Click+=delegate{Close();};Controls.Add(setup);Controls.Add(cancel);AcceptButton=setup;CancelButton=cancel;
        }
        // Existing installations use their saved configuration; a new machine selects hardware once.
        bool SelectHardware(out string physical,out int speed,out int antennas){
            physical=null;speed=115200;antennas=4;
            var ports=Common.Ports().Where(p=>p.Id!=null&&!p.Id.StartsWith("COM0COM",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(File.Exists(Common.Config)){
                var s=Settings.Load(Common.Config);physical=Common.Resolve(s,ports);speed=s.Baud;antennas=s.Antennas;
                if(physical==null)throw new IOException("Connect the configured ZK reader.");return true;
            }
            using(var dialog=new Form{Text=Caption,ClientSize=new Size(420,205),Font=Font,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
                var port=new ComboBox{Location=new Point(105,24),Size=new Size(288,28),DropDownStyle=ComboBoxStyle.DropDownList};port.Items.AddRange(ports);
                var usb=ports.Where(p=>p.Id.StartsWith("USB\\",StringComparison.OrdinalIgnoreCase)).ToArray();if(usb.Length==1)port.SelectedItem=usb[0];else if(ports.Length==1)port.SelectedIndex=0;
                var baud=new ComboBox{Location=new Point(105,67),Size=new Size(125,28),DropDownStyle=ComboBoxStyle.DropDownList};baud.Items.AddRange(new object[]{9600,19200,38400,57600,115200,921600});baud.SelectedItem=115200;
                var ant=new ComboBox{Location=new Point(105,110),Size=new Size(125,28),DropDownStyle=ComboBoxStyle.DropDownList};ant.Items.AddRange(new object[]{1,4});ant.SelectedItem=4;
                dialog.Controls.AddRange(new Control[]{new Label{Text="COM",Location=new Point(24,27),Size=new Size(70,25)},port,new Label{Text="Baud",Location=new Point(24,70),Size=new Size(70,25)},baud,new Label{Text="Antenna",Location=new Point(24,113),Size=new Size(75,25)},ant});
                var setup=new Button{Text="setup",Location=new Point(160,155),Size=new Size(110,32)};setup.Click+=delegate{if(port.SelectedItem==null){MessageBox.Show(dialog,"Select COM.",Caption);return;}dialog.DialogResult=DialogResult.OK;};
                var cancel=new Button{Text="cancel",Location=new Point(283,155),Size=new Size(110,32),DialogResult=DialogResult.Cancel};dialog.Controls.Add(setup);dialog.Controls.Add(cancel);dialog.AcceptButton=setup;dialog.CancelButton=cancel;
                if(dialog.ShowDialog(this)!=DialogResult.OK)return false;physical=((Port)port.SelectedItem).Name;speed=(int)baud.SelectedItem;antennas=(int)ant.SelectedItem;return true;
            }
        }
        void Apply(){
            try{string physical;int speed,antennas;if(!SelectHardware(out physical,out speed,out antennas))return;
                Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"/install "+physical+" "+speed+" "+antennas){UseShellExecute=true,Verb="runas"});Close();}
            catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=1223)MessageBox.Show(this,e.Message,Caption,MessageBoxButtons.OK,MessageBoxIcon.Error);}
            catch(Exception e){MessageBox.Show(this,e.Message,Caption,MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
        [STAThread]static int Main(string[] args){
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==0){Application.Run(new SetupForm());return 0;}
            try{
                if(args.Length==2&&args[0]=="/extract"){SetupEngine.Extract(Path.GetFullPath(args[1]));return 0;}
                if(args.Length==2&&args[0]=="/verify-driver"){SetupEngine.PrepareMicrosoftDriver(Path.GetFullPath(args[1]),null);return 0;}
                if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)){
                    Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,string.Join(" ",args.Select(Common.Quote))){UseShellExecute=true,Verb="runas"});return 0;
                }
                using(var mutex=new Mutex(false,@"Global\NationComPort.Setup")){
                    if(!mutex.WaitOne(0))throw new IOException("Một bộ cài Nation COM Port khác đang chạy.");
                    try{string result;
                        if(args.Length==4&&args[0]=="/install")result=SetupEngine.Install(args[1],int.Parse(args[2]),int.Parse(args[3]));
                        else if(args.Length==1&&args[0]=="/uninstall")result=SetupEngine.Uninstall();
                        else throw new ArgumentException("Tham số không hợp lệ.");
                        MessageBox.Show(result,Caption,MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;
                    }finally{mutex.ReleaseMutex();}
                }
            }catch(Exception e){MessageBox.Show(e.Message,Caption,MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        }
    }
}
