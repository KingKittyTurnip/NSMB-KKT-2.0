using Photon.Deterministic;
using Quantum.Collections;
using System;
using UnityEngine;
using static UnityEditor.Progress;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {

    public unsafe class HazardSystem : SystemMainThreadFilterStage<HazardSystem.Filter>, ISignalOnEnemyDespawned, ISignalInitializeHazard, /*ISignalOnEnemyRespawned,*/ ISignalOnStageReset {
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
        public static event Action<Frame, EntityRef, bool> HazardIconChanged;
        //public static event Action<Frame, EntityRef> HazardDestroyed;

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
            //f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            //f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var hazard = filter.Hazard;
            //if (!hazard->IsActive)
            //    return;

            var transform = filter.Transform;
            var collider = filter.Collider;

            if (!hazard->IsHazard && !hazard->IsCoinItem) {
                //stage object.
                return;
            }

            // Despawn off bottom of stage
            if (!hazard->DoNotDespawnInPit && transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                HazardSystem.DestroyHazard(f, filter.Entity);
                return;
            }

            var physicsObject = filter.PhysicsObject;

            // Countdown To Despawn
            if (QuantumUtils.Decrement(ref hazard->LifeTime)){
                if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                    f.Global->UsedHazardSpawns.Clear(hazard->index);
                    f.Global->UsedHazardSpawnCount--;
                }
                f.Events.PlayPuffParticle(transform->Position);
                HazardSystem.DestroyHazard(f, filter.Entity);
            }

            // allow interactions
            if (hazard->JustSpawned) {
                if (!hazard->IPWSUntilGround || (hazard->IPWSUntilGround && physicsObject->IsTouchingGround)) {
                    if (hazard->IPWSTime-- <= 0) {
                        hazard->JustSpawned = false;
                        f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = false;
                    }
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

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            // IdeaBulb Carry On Creation :TOTEST:
            if (spawnReason == SpawnReason.Bulb && f.Exists(owner)) {
                f.Unsafe.TryGetPointer(thisEntity, out Holdable* holdable);
                if (holdable != null) {
                    holdable->Holder = owner;
                }
            }

            hazard->JustSpawned = true;
            if (spawnReason == SpawnReason.Item) {
                hazard->IsCoinItem = true;
                hazard->BaseLifeTime = hazard->LifeTime = 600;
            } else {
                hazard->IsHazard = true;

                if ((hazard->IPWSTime != 0 || hazard->IPWSUntilGround) && f.Unsafe.TryGetPointer(thisEntity, out Interactable* inter)) {
                    if (inter != null) {
                        inter->ColliderDisabled = true;
                    } else {
                        hazard->IPWSTime = 0;
                        hazard->IPWSUntilGround = false;
                    }
                }

                //Set LifeTime
                hazard->BaseLifeTime = hazard->LifeTime = f.Global->Rules.HazardLifetime * 60;

                // Shoot in Random Direction
                transform->Position = spawnpoint;
                if (hazard->SpawningVelocityRange != FPVector2.Zero && f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject))
                    physicsObject->Velocity = new(hazard->SpawningVelocityRange.X * ((f.RNG->Next() * 2) - 1), hazard->SpawningVelocityRange.Y);

                // Create Icon on Map
                ChangeHazardIcon(f, thisEntity, true);
            }

            //Set hazard team
            //TODO: Actually Set Team, code general team mechanics
            hazard->Team = 255;
        }

        public static void DestroyHazard(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Hazard* hazard)) {
                if (hazard->IsHazard) {
                    if (hazard->IsHefty)
                        f.Global->HeftyCount--;
                    ChangeHazardIcon(f, entity, false);
                    f.Destroy(entity);
                } else if (hazard->IsCoinItem) {
                    f.Destroy(entity);
                } else {
                    if (f.Unsafe.TryGetPointer(entity, out Transform2D* transform))
                        transform->Position.Y = -255;
                    if (f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider))
                        collider->Enabled = false;
                    if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physics))
                        physics->IsFrozen = true;
                    if (f.Unsafe.TryGetPointer(entity, out Interactable* inter))
                        inter->ColliderDisabled = true;
                    if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                        enemy->IsDead = true;
                        f.Signals.OnEnemyDespawned(entity);
                    }
                }
            } else {
                UnityEngine.Debug.Log("Object Does NOT Have The Hazard Script Or Is Invalid.");
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Hazard, Transform2D>();

            while (filter.NextUnsafe(out EntityRef entity, out Hazard* hazard, out Transform2D* transform)) {
                //if (hazard->IsActive) {
                if (!hazard->IsHazard) { //this is a hazard, we can't respawn
                    continue;
                }
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
                /*} else {
                    // Check for respawns
                    if (hazard->IsHazard) { //this is a hazard, we can't respawn
                        continue;
                    }

                    if (full)
                        hazard->Team = 255;

                    //TODO: port notoss's enemy respawn system, or just not add the support for hazard in stage spawning

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
                }*/
            }
        }

        /*public void OnEnemyRespawned(Frame f, EntityRef entity) {
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
        }*/

        public static void ChangeHazardIcon(Frame f, EntityRef entity, bool Created) {
            HazardIconChanged?.Invoke(f, entity, Created);
        }

        public static bool IsCanInteractWithTeamHazard(Frame f, EntityRef marioEntity, EntityRef hazardEntity, bool IgnoresTeamates = false) {
            var hazard = f.Unsafe.GetPointer<Hazard>(hazardEntity);
            if (hazard->Team == 255 || IgnoresTeamates) {
                //invalid team or ignores teamates
                return true;
            }

            if (f.Unsafe.TryGetPointer<MarioPlayer>(hazardEntity, out var mario))
                return mario->GetTeam(f) == hazard->Team;

            //what.
            return true;
        }
    }
}