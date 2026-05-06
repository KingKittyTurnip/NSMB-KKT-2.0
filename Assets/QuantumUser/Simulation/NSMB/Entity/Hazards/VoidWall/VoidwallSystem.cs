using Photon.Deterministic;
using Quantum.Collections;
using System.Numerics;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.LowLevelPhysics2D.PhysicsShape;

namespace Quantum {
    
    public unsafe class VoidwallSystem : SystemMainThreadEntityFilter<Voidwall, VoidwallSystem.Filter>, ISignalInitializeHazard {
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
         * fix the "fake collision" bug, so mario's velocity stops when he makes contact (this happens cuz the hitbox is too big, use compouned colliders like water)
        */
        
        public override void OnInit(Frame f) {
            //f.Context.Interactions.Register<Voidwall, MarioPlayer>(f, OnVoidwallMarioInteraction);
            f.Context.Interactions.Register<Voidwall, MarioPlayer>(f, OnVoidwallMarioSolidInteraction);
            //f.Context.RegisterPreContactCallback(f, OnCauldronObjectSolidPreContact);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var voidwall = filter.voidwall;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;
            var interactable = filter.Interactable;

            if (voidwall->increment < 50) {
                voidwall->increment += f.DeltaTime * 10;
                if (voidwall->increment >= 50) {
                    voidwall->increment = 50;
                }

                //this code ain't perfect but still achieves the goal very well

                FP Quarter = voidwall->increment;
                int i = 0;
                collider->Shape.Compound.GetShapes(f, out var shape, out int count);
                //place old shape positions
                while (i < count) {
                    shape[i].Centroid.Y = (Quarter - (voidwall->increment/2))*2;
                    shape[i].Box.Extents = new FPVector2(FP._0_20, FPMath.Min(Quarter, 2));
                    i++;
                    Quarter -= 2;
                }
                //create new shapes
                while (Quarter > 0) {
                    Shape2D newShape = Shape2D.CreateBox(new FPVector2(FP._0_20, FPMath.Min(Quarter, 2)), new FPVector2(0, ((Quarter - (voidwall->increment/2)) - (2 - FPMath.Min(Quarter, 2))) * 2));
                    collider->Shape.Compound.AddShape(f, ref newShape);
                    Quarter -= 2;
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
        /*public static void OnVoidwallMarioInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            OnVoidwallMario(f, thisEntity, otherEntity);
        }*/
        public static bool OnVoidwallMarioSolidInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity, PhysicsContact contact) {
            return OnVoidwallMario(f, thisEntity, otherEntity);
        }
        /*private void OnCauldronObjectSolidPreContact(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (f.Has<Voidwall>(entity) && f.Has<PhysicsObject>(contact.Entity)) {
                keepContacts = OnVoidwallMario(f, entity, contact.Entity);
            }
        }*/
        public static bool OnVoidwallMario(Frame f, EntityRef thisEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);

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
            return false;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Voidwall* voidwall)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                return;
            }
        }
        #endregion
    }
}
