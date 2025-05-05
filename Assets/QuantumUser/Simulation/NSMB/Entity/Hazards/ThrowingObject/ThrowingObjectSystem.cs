using Photon.Deterministic;
using Quantum;
using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class ThrowingObjectSystem : SystemMainThreadFilterStage<ThrowingObjectSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {
/*
---------------------------------------

Make Player Treat These As semi Solids If Stuck inside

stone - Add Tarnish Movement - (Playersystem script)

---------------------------------------
*/
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public ThrowingObject* DisObject;
            public Holdable* holdable;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<ThrowingObject, Coin>(f, OnThrowingObjectCoinInteraction);
            f.Context.Interactions.Register<ThrowingObject, Goomba>(f, OnThrowingObjectGoombaInteraction);
            f.Context.Interactions.Register<ThrowingObject, Koopa>(f, OnThrowingObjectKoopaInteraction);
            f.Context.Interactions.Register<ThrowingObject, Bobomb>(f, OnThrowingObjectBobombInteraction);
            f.Context.Interactions.Register<ThrowingObject, BulletBill>(f, OnThrowingObjectBulletBillInteraction);
            f.Context.Interactions.Register<ThrowingObject, PiranhaPlant>(f, OnThrowingObjectPiranhaPlantInteraction);
            f.Context.Interactions.Register<ThrowingObject, Boo>(f, OnThrowingObjectBooInteraction);
            f.Context.Interactions.Register<ThrowingObject, IceBlock>(f, OnThrowingObjectIceBlockInteraction);
            //f.Context.Interactions.Register<ThrowingObject, IceBlock>(f, OnThrowingObjectIceBlockInteractionStationary);
            f.Context.Interactions.Register<MarioPlayer, ThrowingObject>(f, OnThrowingObjectMarioInteraction);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var Dis = filter.DisObject;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                physicsObject->IsFrozen = true;

                f.Destroy(filter.Entity);
                return;
            }

            // Bounce Logic
            if ((Dis->Thrown || Dis->BounceTimes > 0) && physicsObject->IsTouchingGround) {
                Dis->Thrown = false;
                if (Dis->GroundBounce && Dis->BounceTimes < 3) {
                    Dis->BounceTimes += 1;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.Y = 4 - Dis->BounceTimes;
                    physicsObject->Velocity.X *= Constants._0_66;
                } else {
                    physicsObject->Velocity.X = 0;
                    Dis->BounceTimes = 0;
                }
            } else if (physicsObject->Velocity.Y < -6) {
                Dis->BounceTimes = 1;
            } else if (physicsObject->IsTouchingGround && physicsObject->Velocity.X != 0) {
                physicsObject->Velocity.X = 0;
            }

            // Special Updates
            switch (Dis->Type) {
            case ThrowingObjectType.Basic:
                break;
            case ThrowingObjectType.Stone: {
                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                    var entity = filter.Entity;
                    f.Events.HeavyStoneLand(entity, f.Unsafe.GetPointer<Transform2D>(entity)->Position);
                }
                break;
            }
            case ThrowingObjectType.Spring:
            case ThrowingObjectType.RedPow:
            case ThrowingObjectType.BluePow:
            case ThrowingObjectType.Barrel:
            case ThrowingObjectType.Freezie:
            case ThrowingObjectType.CoinBox:
            case ThrowingObjectType.PropellerBox:
            case ThrowingObjectType.BillBlock:
            case ThrowingObjectType.CannonBox:
                break;
            }
        }

        #region Interactions
        public static void OnThrowingObjectMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            if (f.Exists(holdable->Holder))
                return;
            #region SetValues
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            bool hitRight = Dis->Thrown ? !Dis->Facing : damageDirection.X > 0;
            #endregion

            if ((Dis->Thrown || (!physicsObject->IsTouchingGround && Dis->BounceTimes == 0)) && mario->IsDamageable) {
                // Hit Player (Unless Not)
                Dis->Thrown = false;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity = new FPVector2(hitRight ? 1 : -1, 4);
                if (Dis->GroundBounce) Dis->BounceTimes = 1;
                if (Dis->StarsToDrop != 0)
                    mario->DoKnockback(f, marioEntity, hitRight, Dis->IgnoreTeamates ? 0 : Dis->StarsToDrop, Dis->IgnoreTeamates, thisEntity);
                return;
            } else if (!(upDot >= PhysicsObjectSystem.GroundMaxAngle || upDot <= -PhysicsObjectSystem.GroundMaxAngle) && mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    // HOMERUN
                    f.Events.PlayComboSound(thisEntity, 0);
                    Dis->Thrown = true;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity = new FPVector2(hitRight ? -8 : 8, 5);
                    return;
                }
            } else if (upDot < PhysicsObjectSystem.GroundMaxAngle && FPMath.Abs(damageDirection.X) < 1 && !(physicsObject->IsTouchingGround && upDot <= -PhysicsObjectSystem.GroundMaxAngle)) {
                //PlayerInsideObject
                if (Dis->BouceOffPlayer) {
                    // Bouce Off Player
                    Dis->Thrown = false;
                    physicsObject->Velocity = new FPVector2(hitRight ? -1 : 1, 4);
                    physicsObject->IsTouchingGround = false;
                    if (Dis->GroundBounce) Dis->BounceTimes = 1;
                } else {
                    //uhhhh
                }
            }

            if (!Dis->Thrown && upDot < PhysicsObjectSystem.GroundMaxAngle) {
                //Only Allow Carry If No Team Or Same Team --- TOTEST
                /*var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
                var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
                if (hazard->Team != 64 && mario->GetTeam(f) != hazard->Team) {
                    return;
                } */

                // Attempt pickup
                if (mario->CanPickupItem(f, marioEntity, thisEntity)) {
                    // Pickup successful
                    holdable->Pickup(f, thisEntity, marioEntity);
                }
            }
        }
        
        public static void OnThrowingObjectCoinInteraction(Frame f, EntityRef thisEntity, EntityRef coinEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);

            if (!(f.Exists(holdable->PreviousHolder) && Dis->Thrown)) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public static void OnThrowingObjectGoombaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                goomba->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }

        public static void OnThrowingObjectKoopaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var koopa = f.Unsafe.GetPointer<Koopa>(otherEntity);

            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                koopa->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }

        public static void OnThrowingObjectBobombInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                bobomb->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectBulletBillInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bill = f.Unsafe.GetPointer<BulletBill>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                bill->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectPiranhaPlantInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var plant = f.Unsafe.GetPointer<PiranhaPlant>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                plant->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectBooInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var boo = f.Unsafe.GetPointer<Boo>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                boo->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectIceBlockInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var ice = f.Unsafe.GetPointer<IceBlock>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                IceBlockSystem.Destroy(f, otherEntity, IceBlockBreakReason.Other);
            }
        }
        #endregion

        #region Signals
        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out ThrowingObject* Dis)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysicsObject)) {
                return;
            }

            //TODO: Up key
            Dis->Thrown = true;
            Dis->Facing = mario->FacingRight;
            FP bonusSpeed = FPMath.Abs(marioPhysicsObject->Velocity.X / 3);
            if (FPMath.Sign(marioPhysicsObject->Velocity.X) != (mario->FacingRight ? 1 : -1)) {
                bonusSpeed *= -1;
            }
            physicsObject->Velocity.X = (Constants._3_50 + bonusSpeed) * (mario->FacingRight ? 1 : -1);
            physicsObject->Velocity.Y = 1;
            holdable->IgnoreOwnerFrames = 15;

            if (!dropped) {
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out ThrowingObject* Dis)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || f.Exists(holdable->Holder)
                || holdable->IgnoreOwnerFrames > 0) {

                return;
            }

            f.Events.PlayComboSound(entity, 0);
            physicsObject->IsTouchingGround = false;
            physicsObject->Velocity.Y = 5;
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            //uhh i would put specific hazard spawn data here
            //Like The Activate heavyDis stuff so it hurts on spawn
        }
        #endregion
    }
}
