using Photon.Deterministic;
using System.Numerics;
using UnityEngine;

namespace Quantum {
    
    public unsafe class VoidwallSystem : SystemMainThreadFilterStage<VoidwallSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Voidwall* voidwall;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Interactable* Interactable;
        }

        /* TODO:
         * Make teamable, teamates ignore the voidwall
         * fix loop point bug (fix with whatever solution is found for the bellow thing)
         * fix the "fake collision" bug, so mario's velocity stops when he makes contact (this happens cuz the hitbox is too big, use compouned colliders like water)
        */
        
        public override void OnInit(Frame f) {
            //f.Context.Interactions.Register<Voidwall, MarioPlayer>(f, OnVoidwallMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var voidwall = filter.voidwall;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;
            var interactable = filter.Interactable;

            if (voidwall->increment < 50) {
                voidwall->increment += f.DeltaTime * 10;
                if (voidwall->increment >= 50) {
                    collider->Shape.Box.Extents = new FPVector2(FP._0_20, 50);
                } else {
                    collider->Shape.Box.Extents = new FPVector2(FP._0_20, voidwall->increment);
                }
            }
            if (voidwall->DamageCooldown > 0) {
                physicsObject->Velocity.X *= Constants._0_95;
                physicsObject->Velocity.Y = 0;

                voidwall->DamageCooldown -= f.DeltaTime;
                if (voidwall->DamageCooldown <= 0) {
                    voidwall->DamageCooldown = 0;

                    physicsObject->Velocity.X = 0;
                    physicsObject->DisableCollision = interactable->ColliderDisabled = false;
                    physicsObject->IsFrozen = true;
                    collider->Shape.Centroid.Y = 0;
                }
            }
        }

        #region Interactions
        public static bool OnVoidwallMarioInteraction(Frame f, EntityRef thisEntity, EntityRef marioEntity, PhysicsContact contact) {
            #region SetValues
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            #endregion

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom) { //TODO: Add Metal
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                var interactable = f.Unsafe.GetPointer<Interactable>(thisEntity);
                var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

                physicsObject->Velocity.X = (theirPos.X - ourPos.X) > 0 ? -10 : 10;
                physicsObject->Velocity.Y = 0;
                f.Unsafe.GetPointer<Voidwall>(thisEntity)->DamageCooldown = FP._1_20;
                physicsObject->DisableCollision = interactable->ColliderDisabled = true;
                physicsObject->IsFrozen = false;
                collider->Shape.Centroid.Y = -999;
                return false;
            }
            return true;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Voidwall* voidwall)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            //collider->Shape.Compound.
        }
        #endregion
    }
}
