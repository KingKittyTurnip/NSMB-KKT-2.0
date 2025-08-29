using Photon.Deterministic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class StarballSystem : SystemMainThreadFilterStage<StarballSystem.Filter>, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Starball* Starball;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Starball>(f, OnStarballMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var starball = filter.Starball;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            bool Deccel = true;
            // Despawn off bottom of stage
            if (filter.Transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                physicsObject->IsFrozen = true;
                if (starball->Rider != EntityRef.None) {
                    f.Unsafe.GetPointer<MarioPlayer>(starball->Rider)->RidingStarball = false;
                }

                f.Destroy(filter.Entity);
                return;
            }
            QuantumUtils.Decrement(ref starball->JumpBufferFrames);
            QuantumUtils.Decrement(ref starball->CoyoteTimeFrames);

            //Rider Logic
            if (starball->Rider != EntityRef.None) {
                var marphys = f.Unsafe.GetPointer<PhysicsObject>(starball->Rider);
                var mario = f.Unsafe.GetPointer<MarioPlayer>(starball->Rider);
                if (mario->CurrentKnockback == KnockbackStrength.None && mario->RidingStarball && mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                    //(Make Controlled By The Mouse)
                    Input inputs = *f.GetPlayerInput(mario->PlayerRef);
                    if (inputs.Jump.WasPressed) { // Jump buffer
                        starball->JumpBufferFrames = 12;
                    }
                    if (physicsObject->IsTouchingGround) { // Coyote Time
                        starball->CoyoteTimeFrames = 5;
                    }
                    //Left/Right
                    if (inputs.Left.IsDown || inputs.Right.IsDown) {
                        physicsObject->Velocity.X += (inputs.Left.IsDown ? -1 : 1) * (FPMath.Abs(physicsObject->Velocity.X) > 3 ? FPMath.Abs(physicsObject->Velocity.X) > 6 ? FP._0_03 : FP._0_10 : Constants._0_1875) * (mario->FacingRight == physicsObject->Velocity.X > 0 ? 1 : 2); //Move
                        Deccel = false;
                    }

                    //Transfer Collision Logic
                    if (marphys->IsTouchingLeftWall)
                        physicsObject->IsTouchingLeftWall = true;
                    if (marphys->IsTouchingRightWall)
                        physicsObject->IsTouchingRightWall = true;
                    if (marphys->IsTouchingCeiling)
                        physicsObject->IsTouchingCeiling = true;

                    //Jump
                    if (starball->JumpBufferFrames > 0 && starball->CoyoteTimeFrames > 0 && physicsObject->WasTouchingGround) {
                        physicsObject->Velocity.Y = 10 + (physicsObject->Velocity.X * FP._0_05);
                        physicsObject->IsTouchingGround = false;
                        starball->CoyoteTimeFrames = 0;
                        starball->JumpBufferFrames = 0;
                    }
                    if (inputs.Jump.IsDown && physicsObject->Velocity.Y >= -1) {
                        physicsObject->Gravity.Y = -20;
                    } else {
                        physicsObject->Gravity.Y = -31;
                    }

                    //Misc Actions
                    f.Unsafe.GetPointer<Transform2D>(starball->Rider)->Position = filter.Transform->Position + new FPVector2(0, FP._0_50 + (starball->CoyoteTimeFrames == 0 ? FP._0_20 : 0));
                    //collider->Shape.Box.Extents = new FPVector2(Constants._0_40, Constants._0_40);
                    marphys->Velocity = physicsObject->Velocity;
                } else {
                    starball->Rider = EntityRef.None;
                    mario->RidingStarball = false;
                }
            } else {
                //collider->Shape.Box.Extents = new FPVector2(Constants._0_40, Constants._0_40); //new FPVector2(FP._0_01, FP._0_50);
                //collider->Shape.Circle.Radius = FP._0_50;
                physicsObject->Gravity.Y = -31;
            }

            //Physics
            if (physicsObject->IsTouchingGround) {
                if (physicsObject->FloorAngle != 0) {
                    if (!physicsObject->WasTouchingGround) {
                        physicsObject->Velocity.X = FPMath.Clamp(physicsObject->FloorAngle, -8, 8);
                    }
                    physicsObject->Velocity.X -= (Constants.WeirdSlopeConstant * physicsObject->FloorAngle) * FP._0_20;
                    Deccel = false;
                }
                if (!physicsObject->WasTouchingGround) {
                    f.Events.StarBallLand(f, filter.Entity, physicsObject->FloorAngle != 0);
                }
            } else {
                Deccel = false;
            }
            physicsObject->BreakMegaObjects = FPMath.Abs(physicsObject->Velocity.X) > 6;
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                physicsObject->Velocity.X = FPMath.Abs(physicsObject->Velocity.X) * (physicsObject->IsTouchingLeftWall ? 1 : -1) * FP._0_75;
            }
            if (physicsObject->IsTouchingCeiling) {
                physicsObject->Velocity.Y = 0;
            }
            if (Deccel)
                physicsObject->Velocity.X *= Constants._0_95;
            physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -10, 10);
        }

        #region Interactions
        public static void OnStarballMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var starball = f.Unsafe.GetPointer<Starball>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (starball->Rider == marioEntity) {
                //Wait, This is OUR Mario!
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            //Try Make Rider
            if (starball->Rider == EntityRef.None && FPVector2.Dot(damageDirection, FPVector2.Down) > FP._0_25 && mario->KnockbackGetupFrames <= 0 && mario->CurrentKnockback == KnockbackStrength.None) {
                starball->Rider = marioEntity;
                mario->RidingStarball = true;
                return;
            }

            //Try Bonk Other Players
            if (attackFromAbove) {
                physicsObject->Velocity.Y = 7;
            } else {
                physicsObject->Velocity.X = (damageDirection.X < 0 ? -3 : 3);
            }
            if (mario->IsDamageable) {
                mario->DoKnockback(f, marioEntity, damageDirection.X >= 0, 1, attackFromAbove ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, thisEntity, false);
            }

        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set Container
        }
        #endregion
    }
}
