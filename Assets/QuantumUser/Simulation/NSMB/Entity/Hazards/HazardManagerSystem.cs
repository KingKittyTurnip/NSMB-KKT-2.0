using Photon.Deterministic;
using Quantum.Physics2D;
using UnityEngine;

namespace Quantum {
    public unsafe class HazardManagerSystem : SystemMainThread, ISignalOnReturnToRoom {
        //temp
        //generic
        //hefty
        private readonly int MaxHeftys = 1;
        private readonly bool HeftyPriority = false;
        private readonly int HeftySpawnChance = 15;
        //misc
        private readonly bool FillMapOnStart = false;

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

            #region HazardCapCheck
            int CurrentHazards = 0;
            var allStars = f.Filter<Hazard>();
            while (allStars.NextUnsafe(out EntityRef entity, out Hazard* hazard)) {
                if (hazard->IsHazard) {
                    CurrentHazards++;
                }
            }
            if (CurrentHazards >= hazarddata.MaxHazards) {
                f.Global->TimeTilNextHazard = 180;
                return;
            }

            var hazardspawners = f.Filter<HazardManager>();
            while (hazardspawners.NextUnsafe(out EntityRef entity, out HazardManager* dis)) {
                CurrentHazards++;
            }
            if (CurrentHazards >= hazarddata.MaxHazards) {
                f.Global->TimeTilNextHazard = 180;
                return;
            }
            #endregion

            bool spawnedHazardSpawn = false;
            for (int i = 0; i < spawnpoints; i++) {
                // Find a spot...
                if (f.Global->UsedHazardSpawnCount >= spawnpoints) {
                    return;
                }

                int count = f.RNG->Next(0, spawnpoints - f.Global->UsedHazardSpawnCount);
                int index = 0;
                for (int j = 0; j < spawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
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
                f.Global->TimeTilNextHazard = 180;
            }
        }

        private void HandleSpawner(Frame f, ref VersusStageData stage, EntityRef entity, HazardManager* hazardspawner) {
            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            if (QuantumUtils.Decrement(ref hazardspawner->Lifetime)) {
                //GetHazard
                var hazarddata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData;
                int pick = f.RNG->Next(0, hazarddata.HazardDatas.Count);
                //TODO: Hefty Logic

                //SpawnHazard
                EntityRef newEntity = f.Create(hazarddata.HazardDatas[pick].hazardAsset);
                var newhazardspawnerTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                var newhazardspawner = f.Unsafe.GetPointer<Hazard>(newEntity);

                var position = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
                f.Signals.InitializeHazard(newEntity, EntityRef.None, position, SpawnReason.Normal, pick);
                if (newhazardspawner->RestrictSpawnPosition) {
                    newhazardspawner->index = (byte) hazardspawner->spawnIndex;
                } else {
                    f.Global->UsedHazardSpawns.Clear(hazardspawner->spawnIndex);
                    f.Global->UsedHazardSpawnCount--;
                }
                Object.Instantiate(Resources.Load("Prefabs/Particle/SpawnPoof"), new Vector3((float) position.X, (float) position.Y , -5), Quaternion.identity);
                f.Destroy(entity);
            }
        }

        public void OnReturnToRoom(Frame f) {
            f.Global->TimeTilNextHazard = 0;
        }
    }
}
