using Photon.Deterministic;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class HazardManagerSystem : SystemMainThread, ISignalOnReturnToRoom {

        //public override unsafe void OnInit(Frame f) {

            // mock list for test
            // var triggerMap = f.ResolveList(f.Global->Rules.Triggers);
            // triggerMap.Add(new MatchConditionerTrigger {
            //     Condition = TriggerCondition.GotCoin,
            //     ConditionParameter = "",
            //     ConditionTarget = TriggerTarget.Any,
            //     Action = TriggerAction.Kill,
            //     ActionParameter = "",
            //     ActionTarget = TriggerTarget.Conditioner,
            //     Constraint = TriggerConstraint.Always,
            //     ConstraintParameter = "",
            //     ConstraintTarget = TriggerTarget.Any,
            // });
        //}


        public override void Update(Frame f) {
            VersusStageData stage = null;
            var hazarddata = f.ResolveList(f.Global->Rules.Hazards);

            if (QuantumUtils.Decrement(ref f.Global->TimeTilNextHazard)) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                TrySpawnNewHazard(f, stage, hazarddata);
            }

            var hazardspawners = f.Filter<HazardManager>();
            while (hazardspawners.NextUnsafe(out EntityRef entity, out HazardManager* dis)) {
                HandleSpawner(f, ref stage, entity, dis, hazarddata);
            }
        }

        private void TrySpawnNewHazard(Frame f, VersusStageData stage, Quantum.Collections.QList<HazardList> hazarddata) {
            int spawnpoints = stage.HazardSpawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedHazardSpawns;

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
                f.Global->TimeTilNextHazard = (ushort) (f.Global->Rules.HazardFrequency * 60);
                return;
            }

            if (!spawnedHazardSpawn) {
                //max hazards exist
                f.Global->TimeTilNextHazard = 60;
            }
        }

        public static bool HazardCapReached(Frame f, byte Bonus = 0) {
            var Cap = f.Global->Rules.MaxHazards + Bonus;

            byte CurrentHazards = 0;
            var allHazards = f.Filter<Hazard>();
            while (allHazards.NextUnsafe(out EntityRef entity, out Hazard* hazard)) {
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

        private void HandleSpawner(Frame f, ref VersusStageData stage, EntityRef entity, HazardManager* hazardspawner, Quantum.Collections.QList<HazardList> hazarddata) {
            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            if (QuantumUtils.Decrement(ref hazardspawner->Lifetime)) {
                //Sort Hazards
                List<HazardList> spawnablehazards = new();
                var position = f.Unsafe.GetPointer<Transform2D>(entity)->Position;

                //spawn a hefty or a normal hazard?
                FP heftychance = f.Global->Rules.HeftyPercentage - ((FP)f.Global->HeftyCount);
                bool hefty = f.RNG->Next() <= heftychance;
                bool TryAgain = false;

                TryAgain:
                for (byte item = 0; item < hazarddata.Count; item++) {
                    if (hazarddata[item].SpawnRandom) {
                        //Hazard Can Spawn
                        //Add special spawn conditions for:
                        //potion: spawns when the lobby contains at least 6 players, if one doesn't exist the next hazard is guerenteed to be it (this condition is disabled in advanced lobbies)
                        //cauldron: spawns only if a boss entity is in the ruleset
                        if (hazarddata[item].Hefty == hefty) { //Hefty Or No...
                            spawnablehazards.Add(hazarddata[item]);
                        }
                    }
                }
                if (spawnablehazards.Count == 0) {
                    if (TryAgain) {
                        UnityEngine.Debug.LogError("tried to spawn a hazard but couldn't locate anything, this error should NEVER occur but exists as a failsafe");
                        goto DestroySpawner;
                    } else {
                        hefty = !hefty;
                        TryAgain = true;
                        goto TryAgain;
                    }
                }

                //pick a hazard selected
                int pick = f.RNG->Next(0, spawnablehazards.Count);
                UnityEngine.Debug.Log(pick + " " + spawnablehazards.Count);
                if (hefty)
                    f.Global->HeftyCount++;

                //SpawnHazard
                EntityRef newEntity = f.Create(spawnablehazards[pick].HazardPrototype); //error out of range?
                var newhazardspawnerTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                var newhazardspawner = f.Unsafe.GetPointer<Hazard>(newEntity);

                f.Signals.InitializeHazard(newEntity, EntityRef.None, position, SpawnReason.Normal, spawnablehazards[pick].Extra);
                if (newhazardspawner->RestrictSpawnPosition) {
                    newhazardspawner->index = (byte) hazardspawner->spawnIndex;
                } else {
                    f.Global->UsedHazardSpawns.Clear(hazardspawner->spawnIndex);
                    f.Global->UsedHazardSpawnCount--;
                }
                DestroySpawner:
                f.Events.PlayPuffParticle(position);
                f.Destroy(entity);
            }
        }

        public void OnReturnToRoom(Frame f) {
            f.Global->TimeTilNextHazard = 0;
        }


        /// <summary>
        /// creates a hazard with an id for the hazards list, returns the entityref and spawndata but doesn't signal to them
        /// </summary>
        public static void CreateHazardFromReference(Frame f, byte HazardId, out EntityRef newEntity, out HazardList newSpawndata) {
            var hazarddata = f.ResolveList(f.Global->Rules.Hazards);

            newEntity = f.Create(hazarddata[HazardId].HazardPrototype);
            newSpawndata = hazarddata[HazardId];
        }
        /// <summary>
        /// creates a hazard with an id for the hazards list, returns the entityref and spawndata but doesn't signal to them
        /// 
        /// this version exists for optimal parts
        /// </summary>
        public static void CreateHazardFromReference(Frame f, byte HazardId, Quantum.Collections.QList<HazardList> hazarddata, out EntityRef newEntity, out HazardList newSpawndata) {
            newEntity = f.Create(hazarddata[HazardId].HazardPrototype);
            newSpawndata = hazarddata[HazardId];
        }
    }
}
