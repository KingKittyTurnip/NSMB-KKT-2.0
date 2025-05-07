using Photon.Deterministic;
using Quantum.Collections;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class FanSystem : SystemMainThreadFilterStage<FanSystem.Filter>, ISignalOnEntityBumped, ISignalInitializeHazard {
/*
 ---------------------------------------

Wind Particles (attach to the fan, make it the size of the stage and increase emmission to copensate for larger sizes)

Team interactions Make it Not Effect Teamate Objects

Pows Should Interact With The Fan, Breaking It

Interactions With heavystone Don't Work
Gp Interactions are weird

 ---------------------------------------
*/
        public struct Filter {
            public EntityRef Entity;
            public Fan* fan;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Fan>(f, OnFanMarioInteraction);
            f.Context.Interactions.Register<Fan, ThrowingObject>(f, OnFanThrowingObjectInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var fan = filter.fan;
            var entity = filter.Entity;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;

            //Velocity Reset
            if (physicsObject->IsTouchingGround) {
                physicsObject->Velocity.X = 0;
            }

            //Fan Turn Logic
            if (fan->FanTime != 0 && (hazard->LifeTime > 120 || hazard->LifeTime == 0)) {
                fan->FanTime -= 1; 
                if (fan->TurnEffectorDowntime != 0) {
                    QuantumUtils.Decrement(ref fan->TurnEffectorDowntime);
                    fan->CurrentStrength = (fan->FacingRight ? fan->Strength : -fan->Strength) * ((fan->TurnEffectorDowntime / (FP) 45) - 1);
                }
            } else if (!fan->Broken && !fan->FellOver) {
                fan->FanTime = 10 * 60;
                fan->TurnEffectorDowntime = 90;
                fan->FacingRight = !fan->FacingRight;
            }

            //Fan Push
            if (!(fan->FellOver && fan->TurnEffectorDowntime > 0)) {
                var Objects = f.Filter<PhysicsObject>();
                while (Objects.NextUnsafe(out EntityRef OtherEntity, out PhysicsObject* physobj)) {
                    if (physobj->DisableCollision || physobj->IsFrozen || physobj->WindImmune)
                        continue;
                    //Is Checking This Expensive?
                      f.Unsafe.TryGetPointer(OtherEntity, out MarioPlayer* mar);
                      if (mar != null && (mar->IsInShell || mar->IsCrouchedInShell || mar->MegaMushroomFrames > 0)) //TODO: Metal here
                        continue;
                    f.Unsafe.TryGetPointer(OtherEntity, out Transform2D* trans);
                    f.Unsafe.TryGetPointer(OtherEntity, out PhysicsCollider2D* col);
                    PhysicsObjectSystem.Filter physicsSystemFilter = new PhysicsObjectSystem.Filter {
                        Entity = OtherEntity,
                        Transform = trans,
                        PhysicsObject = physobj,
                        Collider = col,
                    };
                    if (fan->FellOver) {
                        if (!physobj->IsTouchingGround)
                            PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, new FPVector2(0, fan->Strength), ref physicsSystemFilter, stage, null, out _);
                    } else {
                        PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(fan->CurrentStrength, 0), ref physicsSystemFilter, stage, null, out _);
                    }
                }
            }
        }

        #region Interactions
        public static void OnFanMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            #region SetValues
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var fan = f.Unsafe.GetPointer<Fan>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            var mariophys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            #endregion

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom) { //TODO: Add Metal
                if (hazard->LifeTime > 1200)
                    hazard->LifeTime = 1200;
                physicsObject->IsFrozen = physicsObject->DisableCollision = f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                fan->FellOver = fan->Broken = true;
                fan->FanTime = fan->TurnEffectorDowntime = 90;
                f.Events.OnFanHit(thisEntity, true);
            } else if (upDot >= PhysicsObjectSystem.GroundMaxAngle && (mario->IsGroundpounding || mario->GroundpoundStandFrames > 0)) {
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = damageDirection.X > 0 ? -2 : 2;
                physicsObject->Velocity.Y = 3;
                fan->Broken = true;
                mario->IsGroundpounding = mariophys->IsTouchingGround = false;
                mariophys->Velocity.Y = 6;
                f.Events.OnFanHit(thisEntity, false);
            } else if (upDot <= -PhysicsObjectSystem.GroundMaxAngle) {
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = damageDirection.X > 0 ? -2 : 2;
                physicsObject->Velocity.Y = 3;
            }
        }

        public static void OnFanThrowingObjectInteraction(Frame f, EntityRef thisEntity, EntityRef throwEntity) {
            var throwobj = f.Unsafe.GetPointer<ThrowingObject>(throwEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(throwEntity);

            if (throwobj->Type == ThrowingObjectType.Stone && !f.Exists(holdable->Holder)) {
                var fan = f.Unsafe.GetPointer<Fan>(thisEntity);
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                var throwTransform = f.Unsafe.GetPointer<Transform2D>(throwEntity);
                var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
                var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

                QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), throwTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = damageDirection.X > 0 ? -2 : 2;
                physicsObject->Velocity.Y = 3;
                fan->Broken = true;
                
                f.Events.OnFanHit(thisEntity, false);
            }
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out Fan* Dis)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(blockBump, out Transform2D* DisTransform)
                || f.Exists(holdable->Holder)
                || holdable->IgnoreOwnerFrames > 0) {

                return;
            }

            physicsObject->IsTouchingGround = false; 
            QuantumUtils.UnwrapWorldLocations(f, transform->Position, position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 2 : -2,
                3
            );
        }
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Fan* fan)) {
                return;
            }

            //Set Constant Direction
            fan->Broken = false; // Enable if hazard rules allow (Use Smoke Particles To Indicate)

            //Starting Diraction
            fan->FacingRight = false; // Rng this unless specified by hazard rules

            //Set FanTime
            fan->FanTime = 10 * 59; // set to Basically 10 seconds
            fan->TurnEffectorDowntime = 45;
        }
        #endregion
    }
}
