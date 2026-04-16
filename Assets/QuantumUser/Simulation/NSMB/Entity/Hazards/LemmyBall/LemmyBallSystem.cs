using Photon.Deterministic;
using UnityEngine.Diagnostics;
using UnityEngine;
using Quantum.Collections;

namespace Quantum {
    
    public unsafe class LemmyBallSystem : SystemMainThreadFilterStage<LemmyBallSystem.Filter>, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public LemmyBall* LemmyBall;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;

            public Hazard* hazard;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<LemmyBall, PhysicsObject>(f, OnLemmyBallObjectInteraction);
            f.Context.Interactions.Register<LemmyBall, PhysicsObject>(f, OnLemmyBallObjectSolidInteraction);
            f.Context.RegisterPreContactCallback(f, OnLemmyBallObjectSolidPreContact);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var lemmyBall = filter.LemmyBall;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var hazard = filter.hazard;

            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                lemmyBall->FacingRight = physicsObject->IsTouchingLeftWall;
            }
            if (physicsObject->IsTouchingGround) {
                if (lemmyBall->BounceDelay == 25) {
                    f.Events.LemmyBallLand(filter.Entity);
                }
                QuantumUtils.Decrement(ref lemmyBall->BounceDelay);
                if (lemmyBall->BounceDelay <= 0) {
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity = new FPVector2(lemmyBall->Speed * (lemmyBall->FacingRight ? 1 : -1), 8);
                    lemmyBall->BounceDelay = 25;
                } else {
                    physicsObject->Velocity = FPVector2.Zero;
                }
            } else {
                FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, FPMath.Abs(lemmyBall->Speed));
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (lemmyBall->FacingRight ? 1 : -1), -clamper, clamper);
                lemmyBall->FacingRight = physicsObject->Velocity.X > 0;
            }
        }

        #region Interactions

        public static void OnLemmyBallObjectInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            TryLemmyBallPush(f, thisEntity, otherEntity, false);
        }
        public static bool OnLemmyBallObjectSolidInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, PhysicsContact contact) {
            return TryLemmyBallPush(f, anyEntity, thisEntity, true);
        }
        private void OnLemmyBallObjectSolidPreContact(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (f.Has<LemmyBall>(entity) && f.Has<PhysicsObject>(contact.Entity)) {
                keepContacts = TryLemmyBallPush(f, entity, contact.Entity, true);
            }
        }

        public static bool TryLemmyBallPush(Frame f, EntityRef thisEntity, EntityRef otherEntity, bool IsSolid) {
            f.Unsafe.TryGetPointer<MarioPlayer>(otherEntity, out var mario);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);
            if ((mario == null && otherPhys->WindImmune) || otherPhys->IsFrozen) {
                //This Object Is Immune To Push, Do Nothing
                return false;
            }
            #region SetValues
            var lemmyBall = f.Unsafe.GetPointer<LemmyBall>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;
            bool Resistant = mario != null && mario->InstakillsEnemies(otherPhys, false); //Make lemmy Ball Faster
            #endregion

            if (IsSolid && damageDirection.Y < -Constants._0_66) {
                //don't push it when if are bouncing on top
                return true;
            }

            if (attackedFromAbove) {
                if (Resistant) {
                    lemmyBall->Speed = lemmyBall->PlayerPushStrength * (damageDirection.X > 0 ? 1 : -1);
                }
                otherPhys->Velocity.Y =  7 + (FPMath.Abs(physicsObject->Velocity.Y) * FP._0_50);
            } else {
                var newVel = (mario != null ? lemmyBall->PlayerPushStrength : lemmyBall->PushStrength) * (damageDirection.X > 0 ? 1 : -1);
                if (Resistant) {
                    lemmyBall->Speed = -newVel;
                } else {
                    otherPhys->Velocity.X = newVel;
                }
            }
            f.Events.LemmyBallHitEntity(thisEntity);
            return true;
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out LemmyBall* lemmyBall)) {

                return;
            }

            f.Events.PlayComboSound(entity, 0);
            physicsObject->IsTouchingGround = false;
            physicsObject->Velocity.Y = 5;
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out LemmyBall* lemmyball)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }
        }
        #endregion
    }
}
