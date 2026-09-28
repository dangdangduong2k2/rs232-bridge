using System;

namespace NationZkBridge {
    // Nation demo v0.39 menu, NOT the older protocol PDF (which assigns mode 4 differently).
    // Compatibility presets preserve the menu's speed class; see docs/RADIO_MAPPING.md
    // for the actual Ex10 Tari/BLF. They are not byte-for-byte RF equivalence.
    public static class ProfileMap {
        public static int ToZk(byte nation){switch(nation){
            case 0:return 205; // FM0 50 kHz, Tari 20 us (Nation 40 kHz / 25 us)
            case 1:return 146; // M4 250 kHz, Tari 20 us (Nation 25 us)
            case 2:return 141; // M4 320 kHz, Tari 20 us (Nation 300 kHz / 25 us)
            case 3:return 203; // FM0 426 kHz, Tari 12.5 us (Nation 400 kHz / 6.25 us)
            case 4:return 123; // exact exposed Tari/M/BLF
            case 5:return 103; // exact exposed Tari/M/BLF
            case 6:return 241; // exact exposed Tari/M/BLF
            case 7:return 241; // M4 320 kHz, Tari 20 us; Ex10 has no M4 400 kHz
            case 10:case 11:return 146;
            case 12:case 13:return 141;
            case 255:return 146; // Bridge auto policy: fixed balanced preset, not adaptive RF.
            default:throw new NotSupportedException("Nation EPC speed is not in the verified demo menu: "+nation);
        }}
        public static byte FromZk(int zk,int preferred){
            if(preferred>=0){try{if(ToZk((byte)preferred)==zk)return (byte)preferred;}catch(NotSupportedException){}}
            switch(zk){case 205:return 0;case 7:case 146:case 244:case 343:return 1;
                case 141:return 2;case 202:case 203:return 3;case 3:case 123:case 222:case 324:return 4;
                case 103:return 5;case 5:case 241:case 342:return 6;case 15:case 147:return 7;
                default:throw new NotSupportedException("ZK profile "+zk+" has no Nation compatibility preset");}
        }
    }
}
