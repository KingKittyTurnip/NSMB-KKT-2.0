using Microsoft.SqlServer.Server;
using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Prototypes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Quantum {
    public partial class SpecificHazardContainerAsset : AssetObject {

        public OptionsAvailable[] ListOfOptions;

        [Serializable]
        public class OptionsAvailable {
            public string Name;
            public AssetRef<EntityPrototype> EntityPrototype;
            public ExtrasListPrototype Extra;
        }
    }
}