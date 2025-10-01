using Photon.Deterministic;
using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {

    public unsafe class HazardSystem : SystemMainThreadFilterStage<HazardSystem.Filter>, ISignalOnEnemyDespawned, ISignalInitializeHazard, ISignalOnEnemyRespawned, ISignalOnStageReset {
        /*
         ---------------------------------------
         
           THIS... is The base Hazard Script
                 Say hi!

           This Script Handles Every Object 
           That Is Allowed To Be A Hazard

           If It's A Hazard it Runs On A Counter, If It Gets Destroyed Or That Counter Goes Up it's Removed
           If It's Not, It Spawns In The Stage, And Respawns Whenever A Star Is Collected

         
         ---------------------------------------
        */
        public static event Action<Frame, EntityRef> HazardInitialized;
        public static event Action<Frame> HazardDestroyed;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;

            public Hazard* Hazard;

            //public Holdable* Holdable;
            //public Freezable* Freezable;

            //public Enemy* Enemy;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var hazard = filter.Hazard;
            if (!hazard->IsHazard) {
                return;
            }

            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            // TODO: Countdown To Despawn
            if (hazard->LifeTime > 0) {
                //hazard->LifeTime--;
            }
            if (QuantumUtils.Decrement(ref hazard->LifeTime)){
                if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                    f.Global->UsedHazardSpawns.Clear(hazard->index);
                    f.Global->UsedHazardSpawnCount--;
                }
                var position = f.Unsafe.GetPointer<Transform2D>(filter.Entity)->Position;
                UnityEngine.Object.Instantiate(Resources.Load("Prefabs/Particle/SpawnPoof"), new Vector3((float) position.X, (float) position.Y, -5), Quaternion.identity);
                HazardSystem.DestroyHazard(f, filter.Entity);
            }

            // allow interactions
            if (hazard->JustSpawned) {
                if (hazard->IPWSUntilGround) {
                    if (physicsObject->IsTouchingGround)
                        hazard->JustSpawned = false;
                } else if (hazard->IPWSTime > 0) {
                    //if (physicsObject->IsTouchingGround) TODO: Do The Countdowncode
                    hazard->JustSpawned = false;
                }
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            f.Unsafe.TryGetPointer(entity, out Hazard* hazard);
            if (hazard != null && hazard->IsHazard) {
                f.Destroy(entity);
                //TODO: Remove From Hazard List
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            //var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            hazard->IsHazard = true;
            // IdeaBulb Carry On Creation :TOTEST:
            if (spawnReason == SpawnReason.Bulb && f.Exists(owner)) {
                f.Unsafe.TryGetPointer(thisEntity, out Holdable* holdable);
                if (holdable != null) {
                    holdable->Holder = owner;
                }
            }

            //Set hazard team
            //TODO: Actually Set Team (+1 From That it normally Is)
            hazard->Team = 0;

            //Set LifeTime
            hazard->BaseLifeTime = hazard->LifeTime = hazardata.DespawnTime.BaseValue * 60;

            // Create Icon on Map
            if (hazard->IsHefty) {
                //TODO: Placeicon creation code here
            }

            // Shot in Random Diraction
            transform->Position = spawnpoint;
            //physicsObject->Velocity = new(hazard->SpawningVelocityRange.X /*Insert RNG Calculator*/, hazard->SpawningVelocityRange.Y);

            HazardInitialized?.Invoke(f, thisEntity);
        }

        public static void DestroyHazard(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Hazard* hazard)) {
                if (hazard->IsHazard) {
                    f.Destroy(entity);
                    HazardDestroyed?.Invoke(f);
                } else {
                    hazard->IsActive = false;
                    if (f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider))
                        collider->Enabled = false;
                    if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physics))
                        physics->IsFrozen = true;
                    if (f.Unsafe.TryGetPointer(entity, out Interactable* inter))
                        inter->ColliderDisabled = true;
                }
            } else {
                UnityEngine.Debug.Log("Object Does Not have The Hazard Script");
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Hazard, Transform2D>();

            while (filter.NextUnsafe(out EntityRef entity, out Hazard* hazard, out Transform2D* transform)) {
                if (hazard->IsActive) {
                    // Check for respawning blocks killing us
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                        || physicsObject->DisableCollision) {
                        continue;
                    }
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                        continue;
                    }

                    if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, entity: entity)) {
                        f.Signals.OnEnemyKilledByStageReset(entity);
                    }
                } else {
                    // Check for respawns
                    if (hazard->IsHazard) { //this is a hazard, we can't respawn
                        continue;
                    }

                    if (!hazard->IgnorePlayerWhenRespawning) {
                        Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(hazard->Spawnpoint, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                        if (playerHits.Count > 0) {
                            continue;
                        }
                    }

                    if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                        enemy->Respawn(f, entity);
                    }
                    f.Signals.OnEnemyRespawned(entity);
                }
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Hazard* hazard)) {
                if (!hazard->IsHazard) {
                    hazard->IsActive = true;
                    if (f.Unsafe.TryGetPointer(entity, out Transform2D* transform)) {
                        transform->Teleport(f, hazard->Spawnpoint);
                    }
                    if (!f.Has<Enemy>(entity)) {
                        if (f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider))
                            collider->Enabled = true;
                        if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physics)) {
                            physics->Velocity = FPVector2.Zero;
                            physics->IsFrozen = false;
                        }

                        if (f.Unsafe.TryGetPointer(entity, out Interactable* inter))
                            inter->ColliderDisabled = false;
                    }
                }
            }
        }
    }
}