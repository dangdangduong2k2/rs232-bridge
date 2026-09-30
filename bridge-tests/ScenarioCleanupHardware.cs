using System;
using System.Runtime.InteropServices;
using System.Threading;
using NationZkBridge;
// Opt-in COM49 test: temporary CFG10/11 only; always restore the original values.
class ScenarioCleanupHardware {
    [DllImport("UHFReader288.dll",CallingConvention=CallingConvention.StdCall)] static extern int OpenComPort(int p,ref byte a,byte b,ref int h);
    [DllImport("UHFReader288.dll",CallingConvention=CallingConvention.StdCall)] static extern int CloseSpecComPort(int h);
    [DllImport("UHFReader288.dll",CallingConvention=CallingConvention.StdCall)] static extern int GetCfgParameter(ref byte a,byte n,byte[] d,ref int l,int h);
    [DllImport("UHFReader288.dll",CallingConvention=CallingConvention.StdCall)] static extern int SetCfgParameter(ref byte a,byte o,byte n,byte[] d,int l,int h);
    static byte address=255;static int handle=-1;
    static void Check(int rc){if(rc!=0)throw new Exception("Native status="+rc);}
    static void Open(){address=255;Check(OpenComPort(49,ref address,6,ref handle));}
    static void Close(){if(handle>=0){CloseSpecComPort(handle);handle=-1;}}
    static byte[] Get(byte n){var b=new byte[256];int len=0;Check(GetCfgParameter(ref address,n,b,ref len,handle));Array.Resize(ref b,len);return b;}
    static void Set(byte n,byte[] b){Check(SetCfgParameter(ref address,1,n,b,b.Length,handle));if(BitConverter.ToString(Get(n))!=BitConverter.ToString(b))throw new Exception("CFG readback mismatch");}
    static int Main(){byte[] tid=null,mask=null;byte ant=0;int result=0;try{
        using(var reader=new ZkReader("COM49",115200,4,30,delegate(string s){}))ant=((IWriteTransport)reader).GetAntennaMask();
        Open();tid=Get(10);mask=Get(11);Set(10,new byte[]{0,1});Set(11,new byte[]{1,0,32,8,0xE2});Close();
        bool interrupted=false;
        using(var reader=new ZkReader("COM49",115200,4,30,delegate(string s){})){
            var q=reader.ReadQuery();try{reader.StreamEpc(new Inventory{Antennas=1,Mode=1},q.Q,q.Session,0,delegate(Tag t){throw new InvalidOperationException("intentional callback failure");},CancellationToken.None);}catch(InvalidOperationException e){if(e.Message!="intentional callback failure")throw;interrupted=true;}
            if(((IWriteTransport)reader).GetAntennaMask()!=ant)throw new Exception("Antenna mask not restored");
        }
        if(!interrupted)throw new Exception("No test tag on ANT1");
        Open();if(BitConverter.ToString(Get(10))!="00-01"||BitConverter.ToString(Get(11))!="01-00-20-08-E2")throw new Exception("Scenario did not restore temporary fixture");
        Console.WriteLine("PASS: CFG10, CFG11 and antenna restored after callback exception");
    }catch(Exception e){Console.WriteLine("FAIL: "+e);result=1;}
    finally{try{if(handle<0)Open();if(mask!=null)Set(11,mask);if(tid!=null)Set(10,tid);Console.WriteLine("RESTORED original CFG10/CFG11 with readback");}catch(Exception e){Console.WriteLine("RESTORE FAILED: "+e);result=2;}Close();}
    return result;}
}
