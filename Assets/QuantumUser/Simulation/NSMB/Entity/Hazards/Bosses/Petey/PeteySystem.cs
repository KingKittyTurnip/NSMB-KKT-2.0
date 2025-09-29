using JetBrains.Annotations;
using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using static Quantum.CurrentHazards.HazardDataList;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class PeteySystem : SystemMainThreadFilterStage<PeteySystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Petey* Petey;
            public Boss* Boss;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Petey>(f, OnMarioPeteyInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var petey = filter.Petey;
            var entity = filter.Entity;
            var boss = filter.Boss;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.Collider;

            //Decide Action
            FP leftrightinput = 0;
            bool Groundpounding = false;
            bool HasTarget = false;
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                Input inputs = *f.GetPlayerInput(f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer)->PlayerRef);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;
                f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer)->IsBoss = true;


                Groundpounding = inputs.Down.WasPressed;
                petey->Flying = inputs.Jump.IsDown;
                if (inputs.Left.IsDown || inputs.Right.IsDown) {
                    leftrightinput = (inputs.Left.IsDown == inputs.Right.IsDown) ? -(physicsObject->Velocity.X * FP._0_10) : (inputs.Left.IsDown ? -1 : 1);
                    HasTarget = true;
                }
            } else {
                //Boss Ai

                var Objects = f.Filter<MarioPlayer>();
                Objects.NextUnsafe(out EntityRef OtherEntity, out MarioPlayer* mar);
                boss->ControllerPlayer = OtherEntity;
            }

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.PeteyGetUp(filter.Entity);
                physicsObject->Velocity.Y = 12;
                petey->PreviousLandLevel = stage.StageWorldMin.Y + 7;
                petey->State = PeteyState.Flying;
            }

            //State Calcs
            switch (petey->State) {
            case PeteyState.Idling:
                if (HasTarget || petey->ReusableTimer != 0) {
                    if (petey->ReusableTimer == 0)
                        f.Events.PeteyWakeup(filter.Entity, false);
                    petey->ReusableTimer++;
                    if (petey->ReusableTimer > 180) {
                        petey->ReusableTimer = 0;
                        petey->State = petey->Flying ? PeteyState.Flying : PeteyState.Jumping;
                        physicsObject->Velocity.Y = 6;
                        physicsObject->IsTouchingGround = false;
                    }
                }
                break;
            case PeteyState.Jumping:
                if (leftrightinput != 0)
                    boss->FacingRight = leftrightinput > 0;
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_20), -6, 6);
                if (physicsObject->IsTouchingGround) {
                    f.Events.PeteyJump(filter.Entity);
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.Y = 6;
                    physicsObject->Velocity.X = FPMath.Clamp(leftrightinput * FPMath.Max(FPMath.Abs(physicsObject->Velocity.X), 1), -6, 6);
                    petey->PreviousLandLevel = FPMath.Min(transform->Position.Y + 4, stage.StageWorldMax.Y - 1);
                }
                if (petey->Flying) {
                    petey->State = PeteyState.Flying;
                    petey->ReusableTimer = 0;
                    physicsObject->Gravity.Y = 0;
                }
                break;
            case PeteyState.Flying:
                if (leftrightinput != 0)
                    boss->FacingRight = leftrightinput > 0;
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_10), -3, 3);
                physicsObject->Velocity.Y = FPMath.Clamp(petey->Flying ? physicsObject->Velocity.Y + (physicsObject->Velocity.Y < 0 ? FP._0_50 : FP._0_33) : physicsObject->Velocity.Y-FP._0_33, -6, FPMath.Min((petey->PreviousLandLevel - transform->Position.Y) + physicsObject->Velocity.Y, 5));
                if (Groundpounding) {
                    f.Events.PeteyDive(filter.Entity);
                    petey->State = PeteyState.Diving;
                    petey->ReusableTimer = 0;
                } else if (physicsObject->IsTouchingGround) {
                    petey->State = PeteyState.Jumping;
                    petey->ReusableTimer = 0;
                    physicsObject->Gravity.Y = -10;
                }
                break;
            case PeteyState.Diving:
                if (petey->ReusableTimer++ < 30) {
                    physicsObject->Velocity.X *= FP._0_50;
                    physicsObject->Velocity.Y = FP._0_10;
                } else {
                    collider->Shape.Centroid.Y = petey->FallenBox.Y;
                    collider->Shape.Box.Extents = petey->FallenBox;
                    if (physicsObject->IsTouchingGround) {
                        f.Events.PeteyLanded(filter.Entity);
                        physicsObject->Velocity.X = 0;
                        physicsObject->Velocity.Y = 0;
                        petey->ReusableTimer = 0;
                        petey->State = PeteyState.Fallen;
                    } else {
                        physicsObject->Velocity.X = boss->FacingRight ? 2 : -2;
                        physicsObject->Velocity.Y = -10;
                    }
                }
                break;
            case PeteyState.Fallen:
                petey->ReusableTimer++;
                if (petey->ReusableTimer < 120) {
                } else {
                    if (physicsObject->Gravity.Y == 0)
                        f.Events.PeteyGetUp(filter.Entity);
                    physicsObject->Gravity.Y = -10;
                    if (petey->ReusableTimer > 180) {
                        collider->Shape.Centroid.Y = petey->Hitbox.Y;
                        collider->Shape.Box.Extents = petey->Hitbox;
                        petey->State = PeteyState.Jumping;
                        petey->ReusableTimer = 0;
                    }
                }
                break;
            case PeteyState.MeleeAttack:
                break;
            }

        }

        #region Interactions
        public void OnMarioPeteyInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var petey = f.Unsafe.GetPointer<Petey>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > Constants._0_95;

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            bool vulnrable = petey->State == PeteyState.Fallen;
            bool peteyDiving = petey->State == PeteyState.Diving && petey->ReusableTimer >= 28;
            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Groundpound : KnockbackStrength.Normal);
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsDamageable) {
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, peteyDiving ? 2 : 1, peteyDiving ? KnockbackStrength.Groundpound : KnockbackStrength.CollisionBump, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : thisEntity);
            }
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Petey* petey)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            //relocate
            //boss->ControllerPlayer
        }
        #endregion
    }
}
