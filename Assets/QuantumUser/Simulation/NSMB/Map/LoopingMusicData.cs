using Quantum;
using System;
using UnityEngine;

public class LoopingMusicData : AssetObject {

#if QUANTUM_UNITY
    public UnityEngine.AudioClip clip;
    public UnityEngine.AudioClip fastClip;
#endif
    public float loopStartSeconds;
    public float loopEndSeconds;
    public float speedupFactor = 1.25f;

    //Music Event Data?
    [Space]
    public bool UseMusicEvents = false;
    public e EventData;

    [Serializable]
    public class e {
        public bool LoopEventData;
        public h[] StartEvents;
        public h[] Events;
        [Serializable]
        public class h {
            public float Point;
            public string EventName;
        }
    }
}