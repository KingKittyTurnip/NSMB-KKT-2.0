using Photon.Deterministic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
    public partial class HazardBaser : AssetObject {

        public BaseHazards Hazards;

        [Serializable]
        public class BaseHazards {
            public byte GlobalTimeTilDespawn; //Multiply value by 60 when applying, base value of 80 (1:20 minutes)
            //TODO: add category specific values(?)
            public HazardBase[] ListOfAvalibleHazards;

            [Serializable]
            public class HazardBase {
                [Header("---Generic Info---")]
                public string Name;
                public AssetRef<EntityPrototype> hazardAsset;
                public Sprite Icon;
                public Categorey categorey;
                [Header("---Subdata---")]
                public bool Hefty; //Set As Hefty
                public bool Teamable; //set to 255 to "disable" for the object
                public bool SpawnAsGoal; //decides if it should spawn from starspawn
                public bool SpawnRandom; //decides if it should spawn randomly
                public bool SpawnFridge; //decides if it should spawn from a fridge
                public bool SpawnBulb; //decides if it should spawn from the ideabulb powerup
                [Header("---SpecialData---")]
                public HValue[] SpecialValues;
            }
        }
    }

    #region Enums
    [Serializable]
    public class HValue {
        public string ButtonName = "";
        public ValueType ButtonType = ValueType.toggle;
        public byte BaseValue = 0;
        public IntVector2 ValueRange = new IntVector2(0, 1);
    }
    public enum ValueType {
        toggle, // 0, 1
        counter, //0-255, Buttoninfo is the restriction
        //Type, // 0-255, ButtonInfo Determins
        Refrence, //0-255, Buttoninfo is for the refrence (Used For Refrencing other hazards, value of 255 would do no changes to an object)
    }
    [Flags]
    public enum Categorey {
        other = 1 << 0, //cloudbill, tornado
        Collectable = 1 << 1, //bigstar, every powerup, +clocks
        Weapon = 1 << 2, //heavystone, red pow
        Boss = 1 << 3, //petey, ofc
        Minigame = 1 << 4, //starball,
        WorldModifier = 1 << 5, //spinpipe, oucherglass
        Improper = 1 << 6, //powerups, stage enemies
    }
    #endregion
}