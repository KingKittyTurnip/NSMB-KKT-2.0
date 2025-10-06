using UnityEngine;
using UnityEngine.Serialization;

//namespace NSMB.Sound {
    public class MusicEventData : ScriptableObject {
        public (float, string)[] StartEventData;
        public bool LoopEventData;
        public (float, string)[] EventData;
    }
//}
