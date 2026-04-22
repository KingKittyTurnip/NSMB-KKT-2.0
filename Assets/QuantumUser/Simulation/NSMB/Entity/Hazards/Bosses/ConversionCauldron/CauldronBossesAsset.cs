using Quantum.Collections;
using Quantum.Core;
using System;
using System.Collections.Generic;

namespace Quantum {
    public partial class CauldronBossesAsset : AssetObject {

        public BossesAvailable[] ListOfBosses;

        [Serializable]
        public class BossesAvailable {
            public string Name;
            public AssetRef<EntityPrototype> BossPrototype;
            public List<byte> Extra;
        }
    }
}