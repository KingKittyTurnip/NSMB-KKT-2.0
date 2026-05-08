using Quantum.Collections;
using Quantum.Core;
using System;
using System.Collections.Generic;

namespace Quantum {
    public partial class SpecificHazardContainerAsset : AssetObject {

        public OptionsAvailable[] ListOfOptions;

        [Serializable]
        public class OptionsAvailable {
            public string Name;
            public AssetRef<EntityPrototype> EntityPrototype;
            public List<byte> Extra;
        } 
    }
}