using Photon.Deterministic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {

//ideas:
//make hazards despawn faster if avoided (in tanoomba's case, slower)
//this would make hazards folks aren't caring about in the moment last less long

    public class CurrentHazards : AssetObject {

        public int MaxHazards = 10;
        public byte frequency = 10; //ushort
                                    //hefty
        public FP HeftyPercentage = 0;
        public byte DespawnTime = 80;

        public List<HazardData> HazardDatas;
        [Serializable]
        public class PowerupData {
            [Header("---Hazard---")]
            public string Name;
            public AssetRef<EntityPrototype> PowerupPrototype;
            [Header("---Subdata---")]
            public HValue Team;
            public HValue SpawnChance;
            public HValue WinningBonus;
            public HValue LosingBonus;
            public HValue Scale;
            [Header("---SpecialData---")]
            public HValue[] SpecialValues;
        }
        [Serializable]
        public class HazardData {
            [Header("---Hazard---")]
            public string Name;
            public AssetRef<EntityPrototype> hazardAsset;
            [Header("---Subdata---")]
            public HValue Hefty; //do i allow players to modify this value
            public HValue Team;
            public HValue SpawnRandom;
            public HValue SpawnFridge;
            public HValue SpawnBulb;
            public HValue Scale;
            [Header("---SpecialData---")]
            public HValue[] SpecialValues;
        }

        public int HazardID;
        [EditorButton]
        public void AddHazardFromButton() {
            AddHazard(HazardID);
        }
        public void AddHazard(int ID) {
        }
    }
}