using Photon.Deterministic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {

//ideas:
//make hazards despawn faster if avoided (in tanoomba's case, slower)
//this would make hazards folks aren't caring about in the moment last less long


    public class CurrentHazards : AssetObject {
        //public RulesBaser basehazards = null;
        public HazardDataList HazardGameData;

        [Serializable]
        public class HazardDataList {
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
        }

        public int HazardID;
        [EditorButton]
        public void AddHazardFromButton() {
            AddHazard(HazardID);
        }
        public void AddHazard(int ID) {
            HazardGameData.HazardDatas.Add(new());
            int i = HazardGameData.HazardDatas.Count - 1;
            Frame f = null;
            var bases = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;

            HazardGameData.HazardDatas[i].Name = bases[ID].Name;
            HazardGameData.HazardDatas[i].hazardAsset = bases[ID].entityPrototype;
            //General Buttons
            #region General Buttons
            HazardGameData.HazardDatas[i].Hefty = new();
            //HazardGameData.HazardDatas[i].SpawnAsGoal = new();
            HazardGameData.HazardDatas[i].SpawnRandom = new();
            HazardGameData.HazardDatas[i].SpawnFridge = new();
            HazardGameData.HazardDatas[i].SpawnBulb = new();
            HazardGameData.HazardDatas[i].Team = new();
            HazardGameData.HazardDatas[i].Hefty.ButtonName = "Enabled As Hefty";
             //HazardGameData.HazardDatas[i].SpawnAsGoal.ButtonName = "Spawn As Goal";
             HazardGameData.HazardDatas[i].SpawnRandom.ButtonName = "Spawn Randomly";
             HazardGameData.HazardDatas[i].SpawnFridge.ButtonName = "Appear From Fridge";
             HazardGameData.HazardDatas[i].SpawnBulb.ButtonName = "Creatable From Bulb";
            HazardGameData.HazardDatas[i].Hefty.ButtonType =
             //HazardGameData.HazardDatas[i].SpawnAsGoal.ButtonType =
             HazardGameData.HazardDatas[i].SpawnRandom.ButtonType =
             HazardGameData.HazardDatas[i].SpawnFridge.ButtonType =
             HazardGameData.HazardDatas[i].SpawnBulb.ButtonType =
                ValueType.toggle;
            HazardGameData.HazardDatas[i].Hefty.BaseValue = (byte) (bases[ID].Hefty ? 1 : 0);
             //HazardGameData.HazardDatas[i].SpawnAsGoal.BaseValue = (byte) (bases[ID].SpawnAsGoal ? 1 : 255); //Only goal objects can spawn as goal
             HazardGameData.HazardDatas[i].SpawnRandom.BaseValue = (byte) (bases[ID].SpawnRandom ? 1 : 0);
             HazardGameData.HazardDatas[i].SpawnFridge.BaseValue = (byte) (bases[ID].SpawnFridge ? 1 : 0);
             HazardGameData.HazardDatas[i].SpawnBulb.BaseValue = (byte) (bases[ID].SpawnBulb ? 1 : 0);
            HazardGameData.HazardDatas[i].Hefty.ValueRange =
             //HazardGameData.HazardDatas[i].SpawnAsGoal.ValueRange =
             HazardGameData.HazardDatas[i].SpawnRandom.ValueRange =
             HazardGameData.HazardDatas[i].SpawnFridge.ValueRange =
             HazardGameData.HazardDatas[i].SpawnBulb.ValueRange =
                new IntVector2(0, 1);

            HazardGameData.HazardDatas[i].Team.ButtonName = "Team";
            HazardGameData.HazardDatas[i].Team.ButtonType = ValueType.counter;
            HazardGameData.HazardDatas[i].Team.BaseValue = (byte) (bases[ID].Teamable ? 0 : 255);
            HazardGameData.HazardDatas[i].Team.ValueRange = (bases[ID].Teamable ? new IntVector2(0, 6) : new IntVector2(255, 255));
            #endregion

            //Special Buttons
            HazardGameData.HazardDatas[i].SpecialValues = bases[ID].SpecialValues;
        }
    }
}