using System;
using System.Collections.Generic;
using System.Threading;
using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;

// Configuration only: no inventory, no tag writes, all hardware Sets are temporary.
class RadioSdk {
    static int checks;static GClient client;static string endpoint;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS: "+text);}
    static void Open(){client=new GClient();eConnectionAttemptEventStatusType status;bool serial=endpoint.StartsWith("COM",StringComparison.OrdinalIgnoreCase);Check(serial?client.OpenSerial(endpoint,5000,out status):client.OpenTcp(endpoint,5000,out status),"Nation SDK connected "+endpoint);}
    static T Send<T>(T m)where T:Message {client.SendSynMsg(m,5000);Check(m.RtCode==0,m.GetType().Name+" result="+m.RtCode+" "+m.RtMsg);return m;}
    static void SetFrequency(bool auto,List<byte> values){Send(new MsgBaseSetFrequency{Automatically=auto,ListFreqCursor=values,IsPersistence=0});}
    static int Main(string[] args){endpoint=args[0];MsgBaseGetPower oldPower=null;MsgBaseGetFreqRange oldBand=null;MsgBaseGetFrequency oldFreq=null;MsgBaseGetBaseband oldBase=null;MsgBaseGetTagLog oldReport=null;bool changed=false;int result=0;
        try{Open();oldPower=Send(new MsgBaseGetPower());oldBand=Send(new MsgBaseGetFreqRange());oldFreq=Send(new MsgBaseGetFrequency());oldBase=Send(new MsgBaseGetBaseband());oldReport=Send(new MsgBaseGetTagLog());
            Console.WriteLine("BASELINE band="+oldBand.FreqRangeIndex+" auto="+oldFreq.AutoIndex+" speed="+oldBase.BaseSpeed+" Q="+oldBase.QValue);
            changed=true;var values=new Dictionary<byte,byte>();foreach(var pair in oldPower.DicPower)values[pair.Key]=(byte)Math.Max(0,pair.Value-pair.Key+1);
            Send(new MsgBaseSetPower{DicPower=values,IsPersistence=0});var power=Send(new MsgBaseGetPower());foreach(var p in values)Check(power.DicPower[p.Key]==p.Value,"Antenna "+p.Key+" power="+p.Value);
            Send(new MsgBaseSetPower{DicPower=new Dictionary<byte,byte>{{1,7}},IsPersistence=0});power=Send(new MsgBaseGetPower());Check(power.DicPower[1]==7,"Partial antenna power update");foreach(var p in values)if(p.Key!=1)Check(power.DicPower[p.Key]==p.Value,"Other antenna preserved "+p.Key);
            foreach(byte band in new byte[]{0,1,3,4,5,13,15}){
                Send(new MsgBaseSetFreqRange{FreqRangeIndex=band,IsPersistence=0});Check(Send(new MsgBaseGetFreqRange()).FreqRangeIndex==band,"Band roundtrip "+band);
                SetFrequency(false,new List<byte>{0});var freq=Send(new MsgBaseGetFrequency());Check(freq.AutoIndex==0&&freq.ListFreqCursor.Count==1&&freq.ListFreqCursor[0]==0,"Fixed channel zero "+band);
                SetFrequency(true,null);Check(Send(new MsgBaseGetFrequency()).AutoIndex==1,"Auto channel range "+band);
            }
            foreach(byte speed in new byte[]{0,1,2,3,4,5,6,7,10,11,12,13,255}){Send(new MsgBaseSetBaseband{BaseSpeed=speed,IsPersistence=0});Check(Send(new MsgBaseGetBaseband()).BaseSpeed==speed,"EPC speed roundtrip "+speed);}
            Send(new MsgBaseSetBaseband{QValue=7,Session=1,InventoryFlag=2,IsPersistence=0});Send(new MsgBaseSetTagLog{RepeatedTime=25,RssiTV=10,IsPersistence=0});
            foreach(int pause in new[]{350,100,700,350,100,350}){client.Close();Thread.Sleep(pause);Open();var b=Send(new MsgBaseGetBaseband());Check(b.QValue==7&&b.Session==1,"Baseband survives reconnect "+pause+"ms");var r=Send(new MsgBaseGetTagLog());Check(r.RepeatedTime==25&&r.RssiTV==10,"Reporting survives reconnect "+pause+"ms");}
        }catch(Exception e){result=1;Console.WriteLine("FAIL: "+e);}
        finally{
            if(changed){try{Send(new MsgBaseSetPower{DicPower=oldPower.DicPower,IsPersistence=0});Send(new MsgBaseSetFreqRange{FreqRangeIndex=oldBand.FreqRangeIndex,IsPersistence=0});SetFrequency(oldFreq.AutoIndex==1,oldFreq.ListFreqCursor);Send(new MsgBaseSetBaseband{BaseSpeed=oldBase.BaseSpeed,QValue=oldBase.QValue,Session=oldBase.Session,InventoryFlag=oldBase.InventoryFlag,IsPersistence=0});Send(new MsgBaseSetTagLog{RepeatedTime=oldReport.RepeatedTime,RssiTV=oldReport.RssiTV,IsPersistence=0});
                var p=Send(new MsgBaseGetPower());foreach(var v in oldPower.DicPower)Check(p.DicPower[v.Key]==v.Value,"RESTORED antenna "+v.Key);
                Check(Send(new MsgBaseGetFreqRange()).FreqRangeIndex==oldBand.FreqRangeIndex,"RESTORED band");var f=Send(new MsgBaseGetFrequency());Check(f.AutoIndex==oldFreq.AutoIndex&&string.Join(",",f.ListFreqCursor)==string.Join(",",oldFreq.ListFreqCursor),"RESTORED frequency");var b=Send(new MsgBaseGetBaseband());Check(b.BaseSpeed==oldBase.BaseSpeed&&b.QValue==oldBase.QValue&&b.Session==oldBase.Session&&b.InventoryFlag==oldBase.InventoryFlag,"RESTORED baseband");var r=Send(new MsgBaseGetTagLog());Check(r.RepeatedTime==oldReport.RepeatedTime&&r.RssiTV==oldReport.RssiTV,"RESTORED reporting");
            }catch(Exception e){result=2;Console.WriteLine("RESTORE FAILED: "+e);}}
            if(client!=null)client.Close();
        }
        Console.WriteLine("CONFIG CHECKS="+checks+" EXIT="+result);return result;
    }
}
