using Photon.Deterministic;
using Quantum.Physics2D;
using System.Collections.Generic;
using UnityEngine;
using static Quantum.CurrentHazards.HazardDataList;

namespace Quantum {
    public unsafe class HazardManagerSystem : SystemMainThread, ISignalOnReturnToRoom {

        public override void Update(Frame f) {
            VersusStageData stage = null;

            if (QuantumUtils.Decrement(ref f.Global->TimeTilNextHazard)) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                TrySpawnNewHazard(f, stage);
            }

            var hazardspawners = f.Filter<HazardManager>();
            while (hazardspawners.NextUnsafe(out EntityRef entity, out HazardManager* dis)) {
                HandleSpawner(f, ref stage, entity, dis);
            }
        }

        private void TrySpawnNewHazard(Frame f, VersusStageData stage) {
            int spawnpoints = stage.HazardSpawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedHazardSpawns;
            var hazarddata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData;

            if (HazardCapReached(f)) {
                f.Global->TimeTilNextHazard = 60;
                return;
            }

            bool spawnedHazardSpawn = false;
            for (int i = 0; i < spawnpoints; i++) {
                // Find a spot...
                if (f.Global->UsedHazardSpawnCount >= spawnpoints) {
                    return;
                }

                int count = f.RNG->Next(0, spawnpoints - f.Global->UsedHazardSpawnCount);
                int index = -1;
                for (int j = 0; j < spawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                if (index == -1) {
                    //All Spawn Locations Are In Use...?
                    f.Global->TimeTilNextHazard = 60;
                    continue;
                }
                usedSpawnpoints.Set(index);
                f.Global->UsedHazardSpawnCount++;

                // Create hazardspawn, spawning hazard handled later
                FPVector2 position = stage.HazardSpawnpoints[index];

                EntityRef newEntity = f.Create(f.SimulationConfig.HazardSpawn);
                var newhazardspawnerTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                var newhazardspawner = f.Unsafe.GetPointer<HazardManager>(newEntity);

                newhazardspawnerTransform->Position = position;
                newhazardspawner->Lifetime = 150;
                newhazardspawner->spawnIndex = index;
                spawnedHazardSpawn = true;
                f.Global->TimeTilNextHazard = (ushort) (hazarddata.frequency * 60);
                return;
            }

            if (!spawnedHazardSpawn) {
                //max hazards exist
                f.Global->TimeTilNextHazard = 60;
            }
        }

        public static bool HazardCapReached(Frame f, byte Bonus = 0) {
            var Cap = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.MaxHazards + Bonus;

            byte CurrentHazards = 0;
            var allStars = f.Filter<Hazard>();
            while (allStars.NextUnsafe(out EntityRef entity, out Hazard* hazard)) {
                if (hazard->IsHazard) {
                    CurrentHazards++;
                }
            }
            if (CurrentHazards >= Cap) {
                return true;
            }

            var hazardspawners = f.Filter<HazardManager>();
            while (hazardspawners.NextUnsafe(out EntityRef entity, out HazardManager* dis)) {
                CurrentHazards++;
            }
            if (CurrentHazards >= Cap) {
                return true;
            }
            return false;
        }

        public static EntityRef GetHazardInListFromReference(Frame f) {
            return EntityRef.None;
        }

        private void HandleSpawner(Frame f, ref VersusStageData stage, EntityRef entity, HazardManager* hazardspawner) {
            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            if (QuantumUtils.Decrement(ref hazardspawner->Lifetime)) {
                //(prob save this somewhere instead of calculating it each time tbh...)
                //Sort Hazards
                var hazardSettings = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData;
                List<(HazardData, byte)> hazarddata = new(), heftydata = new(); //new(hazardSettings.HazardDatas)
                for (byte item = 0; item < hazardSettings.HazardDatas.Count; item++) {
                    if (hazardSettings.HazardDatas[item].SpawnRandom.BaseValue == 1) {
                        //Hazard Can Spawn
                        //Add special spawn conditions for:
                        //potion: spawns when the lobby contains at least 6 players, if one doesn't exist the next hazard is guerenteed to be it (this condition is disabled in advanced lobbies)
                        //cauldron: spawns only if a boss entity is in the ruleset
                        if (hazardSettings.HazardDatas[item].Hefty.BaseValue == 1) { //Hefty Or No...
                            heftydata.Add((hazardSettings.HazardDatas[item], item));
                        } else {
                            hazarddata.Add((hazardSettings.HazardDatas[item], item));
                        }
                    }
                }
                //Get Hazard
                int pick = 0; //don't use an int, use the value of hazardasset to use less checks (?)
                FP heftychance = hazardSettings.HeftyPercentage - f.Global->HeftyCount;
                bool hefty = f.RNG->Next() <= heftychance;
                if (hefty) {
                    f.Global->HeftyCount++;
                    pick = f.RNG->Next(0, heftydata.Count);
                } else {
                    pick = f.RNG->Next(0, hazarddata.Count);
                }

                //SpawnHazard
                EntityRef newEntity = f.Create(hefty ? heftydata[pick].Item1.hazardAsset : hazarddata[pick].Item1.hazardAsset); //error out of range?
                var newhazardspawnerTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                var newhazardspawner = f.Unsafe.GetPointer<Hazard>(newEntity);

                var position = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                f.Signals.InitializeHazard(newEntity, EntityRef.None, position, SpawnReason.Normal, hefty ? heftydata[pick].Item2 : hazarddata[pick].Item2);
                if (newhazardspawner->RestrictSpawnPosition) {
                    newhazardspawner->index = (byte) hazardspawner->spawnIndex;
                } else {
                    f.Global->UsedHazardSpawns.Clear(hazardspawner->spawnIndex);
                    f.Global->UsedHazardSpawnCount--;
                }
                f.Events.PlayPuffParticle(position);
                f.Destroy(entity);
            }
        }

        public void OnReturnToRoom(Frame f) {
            f.Global->TimeTilNextHazard = 0;
        }
    }
}
