using Photon.Deterministic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {

    public unsafe class HazardSystem : SystemMainThreadFilterStage<HazardSystem.Filter>, ISignalOnEnemyDespawned, ISignalInitializeHazard {
        /*
         ---------------------------------------
         
           THIS... is The base Hazard Script
                 Say hi!

           This Script Will only Do Things If 
               It's Spawned As A Hazard 
         
         ---------------------------------------
        */
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
            if (!hazard->IsHazard)
                return;

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
                Object.Instantiate(Resources.Load("Prefabs/Particle/SpawnPoof"), new Vector3((float) position.X, (float) position.Y, -5), Quaternion.identity);
                f.Destroy(filter.Entity);
            }

            // allow interactions
            if (hazard->IPWSUntilGround) {
                if (physicsObject->IsTouchingGround)
                    hazard->Inactive = false;
            } else if (hazard->IPWSTime > 0) {
                //if (physicsObject->IsTouchingGround) TODO: Do The Countdowncode
                hazard->Inactive = false;
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            f.Unsafe.TryGetPointer(entity, out Hazard* hazard);
            if (hazard != null && hazard->IsHazard) {
                f.Destroy(entity);
                //TODO: Remove From Hazard List
            }
        }

        public void OnDestroy() {

        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            //var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

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
            //TODO: Set Lifetime
            hazard->BaseLifeTime = hazard->LifeTime = 80 * 60;

            // Create Icon on Map
            if (hazard->IsHefty) {
                //TODO: Placeicon creation code here
            }

            // Shot in Random Diraction
            transform->Position = spawnpoint;
            //physicsObject->Velocity = new(hazard->SpawningVelocityRange.X /*Insert RNG Calculator*/, hazard->SpawningVelocityRange.Y);
        }
    }
}