using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace NationZkBridge {
    public sealed class RadioRegion {
        public byte Band, Min, Max;
        public RadioRegion(){}
        public RadioRegion(byte band,byte min,byte max){Band=band;Min=min;Max=max;}
        public bool Same(RadioRegion other){return other!=null&&Band==other.Band&&Min==other.Min&&Max==other.Max;}
    }
    public interface IRadioReader {
        byte[] ReadPowers();
        void SetPowers(byte[] values,bool persist,byte selectedMask=255);
        RadioRegion ReadRegion();
        void SetRegion(RadioRegion value,bool persist);
        int ReadProfile();
        void SetProfile(int profile,bool persist);
    }
    // Channel numbers come from the supplied Nation demo v0.39 and Ex10 v2.25.
    // Never copy channel indexes across brands: China's Nation channel 0 is ZK 2.
    public sealed class RegionMap {
        public byte Nation, Band, Offset, Count;
        public RegionMap(byte nation,byte band,byte offset,byte count){Nation=nation;Band=band;Offset=offset;Count=count;}
        public RadioRegion Full(){return new RadioRegion(Band,Offset,(byte)(Offset+Count-1));}
        public bool Contains(RadioRegion r){return r.Band==Band&&r.Min>=Offset&&r.Max>=r.Min&&r.Max<Offset+Count;}
        public static readonly RegionMap[] All={
            new RegionMap(0,1,2,16),new RegionMap(1,8,2,16),new RegionMap(3,2,0,50),
            new RegionMap(4,9,0,4),new RegionMap(5,31,0,4),new RegionMap(13,9,1,3),
            new RegionMap(15,1,0,20)
        };
        public static byte[] Supported(){var b=new List<byte>();foreach(var m in All)b.Add(m.Nation);return b.ToArray();}
        public static RegionMap Find(byte nation){foreach(var m in All)if(m.Nation==nation)return m;throw new NotSupportedException("Nation frequency band has no verified ZK channel mapping: "+nation);}
        public static RegionMap Resolve(RadioRegion actual,int preferred){foreach(var m in All)if(m.Nation==preferred&&m.Contains(actual))return m;foreach(var m in All)if(m.Contains(actual))return m;throw new NotSupportedException("Current ZK band/channel range cannot be represented by Nation");}
        public RadioRegion Select(byte[] channels){
            if(channels.Length==0||channels.Length>50)throw new ArgumentException("Expected 1..50 channels");
            var sorted=(byte[])channels.Clone();Array.Sort(sorted);
            for(int i=0;i<sorted.Length;i++)if(sorted[i]>=Count||(i>0&&sorted[i]!=sorted[i-1]+1))throw new ArgumentException("ZK requires a contiguous, unique channel list within this band");
            return new RadioRegion(Band,(byte)(Offset+sorted[0]),(byte)(Offset+sorted[sorted.Length-1]));
        }
        public byte[] Channels(RadioRegion actual){if(!Contains(actual))throw new InvalidDataException("Region mismatch");var b=new byte[actual.Max-actual.Min+1];for(int i=0;i<b.Length;i++)b[i]=(byte)(actual.Min-Offset+i);return b;}
    }
    public sealed class StateData {
        public byte Q=4, Session=0, Target=2, Rssi=0;
        public ushort Duplicate=0;
        public int NationSpeed=-1, ZkProfile=-1, NationRegion=-1, FrequencyAuto=-1;
        public byte RegionBand, RegionMin, RegionMax;
        public StateData Copy(){return (StateData)MemberwiseClone();}
        public void Validate(){if(Q>15||Session>3||Target>2||NationSpeed < -1||NationSpeed>255||ZkProfile < -1||ZkProfile>65535||NationRegion < -1||NationRegion>255||FrequencyAuto < -1||FrequencyAuto>1)throw new InvalidDataException("Invalid saved bridge configuration");}
    }
    public sealed class BridgeState {
        public StateData Active {get;private set;}
        StateData saved;readonly string file;
        public BridgeState():this(null){}
        public BridgeState(string path){file=path;saved=new StateData();if(file!=null&&File.Exists(file)){using(var r=File.OpenRead(file))saved=(StateData)new XmlSerializer(typeof(StateData)).Deserialize(r);saved.Validate();}Active=saved.Copy();}
        public void Commit(Action<StateData> update,bool persist){
            var next=Active.Copy();update(next);next.Validate();
            if(persist){var disk=saved.Copy();update(disk);disk.Validate();
                if(file!=null){string full=Path.GetFullPath(file);Directory.CreateDirectory(Path.GetDirectoryName(full));string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try{using(var w=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(StateData)).Serialize(w,disk);w.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}
                    catch(UnauthorizedAccessException e){throw new IOException("Cannot save bridge configuration",e);}
                    finally{if(File.Exists(temp))File.Delete(temp);}}
                saved=disk;
            }Active=next;
        }
    }
}
