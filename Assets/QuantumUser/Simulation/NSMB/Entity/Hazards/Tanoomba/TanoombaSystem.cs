using Photon.Deterministic;
using Quantum;
using Quantum.Collections;
using Quantum.Physics2D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.UIElements;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class TanoombaSystem : SystemMainThreadFilterStage<TanoombaSystem.Filter>, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard, ISignalOnEnemyRespawned {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Tanoomba* Tanoomba;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Tanoomba, MarioPlayer>(f, OnTanoombaMarioInteraction);
            f.Context.Interactions.Register<Tanoomba, Projectile>(f, OnTanoombaProjectileInteraction);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var tanoomba = filter.Tanoomba;
            var enemy = filter.Enemy;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;

            QuantumUtils.Decrement(ref tanoomba->DamageInvincibilityFrames);

            switch (tanoomba->State) {
            #region Base Tanoomba
            //Tanoomba Wanders
            case TanoombaState.Idling:
                physicsObject->Velocity.X = (physicsObject->IsTouchingGround ? 1 : FP._1_75) * (enemy->FacingRight ? 1 : -1);

                //Check For Level Geometry
                if (physicsObject->IsTouchingGround) {
                    if ((physicsObject->IsTouchingLeftWall && !enemy->FacingRight) || (physicsObject->IsTouchingRightWall && enemy->FacingRight)) {
                        FPVector2 checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, FP._1_33);
                        if (PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out var thewallbro)) {
                            enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
                        } else {
                            checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, 1);
                            physicsObject->Velocity.Y = PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out thewallbro) ? tanoomba->JumpVelocity : 4;
                            physicsObject->IsTouchingGround = false;
                        }
                    }

                    FPVector2 checkPosition = transform->Position + (FPVector2.Right * FP._0_05 * (enemy->FacingRight ? 1 : -1));
                    if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_33, out var hit)) {
                        // Failed to hit a raycast, but check to make sure we don't have a contact point instead.

                        bool turnaround = true;
                        QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                        foreach (var contact in contacts) {
                            if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                                // Not on the ground
                                continue;
                            }

                            // Is a ground contact
                            QuantumUtils.UnwrapWorldLocations(stage, transform->Position, contact.Position, out FPVector2 ourPos, out FPVector2 contactPos);
                            if ((enemy->FacingRight && ourPos.X < contactPos.X)
                                || (!enemy->FacingRight && ourPos.X > contactPos.X)) {
                                turnaround = false;
                                break;
                            }
                        }

                        if (turnaround) {
                            //Jump The Gap? or turn back
                            FPVector2 checkPositione = transform->Position + new FPVector2((enemy->FacingRight ? FP._1_75 : -FP._1_75), 2);
                            if (PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, 3, out hit)) {
                                if (hit.Position.Y <= transform->Position.Y) {
                                    physicsObject->Velocity.Y = tanoomba->JumpVelocity;
                                    physicsObject->IsTouchingGround = false;
                                    break;
                                }
                            }
                            if (PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, 3, out hit)) {
                                break;
                            }
                            enemy->ChangeFacingRight(f, filter.Entity, !enemy->FacingRight);
                        }
                    }
                }

                //Check For Player
                if (false) {

                }
                break;
            //Tanoomba Flees And Searches The World Something To Turn into, Away From players Ofc
            //If he Can't He Waits Idle For A Few Frames And Checks Again
            case TanoombaState.Searching: {
                var newForm = GetForm(f, ref filter, stage);
                if (newForm != TanoombaFormState.Max) {
                } else {
                    //Do Nothing Ig...
                    //tanoomba->State = TanoombaState.Idling;
                }
                break;
            }
            //Tanoomba Runs Up To Attack The Player
            case TanoombaState.Attacking: {
                break;
            }
            //This Might Be Removed To Make it Easier To Defeat
            case TanoombaState.KnockedBack: {
                if (physicsObject->IsTouchingGround)
                    physicsObject->Velocity.X += physicsObject->Velocity.X > 0 ? -FP._0_10 : FP._0_10;
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_20) {
                    physicsObject->Velocity.X = 0;
                    tanoomba->GetupFrames--;
                    if (tanoomba->GetupFrames <= 0) {
                        tanoomba->State = TanoombaState.Searching;
                        tanoomba->LastKnockback = 255;
                        tanoomba->DamageInvincibilityFrames = 120;
                    }
                }
                break;
            }
            case TanoombaState.Transformed: {
                break;
            }
            #endregion
            }
        }

        public TanoombaFormState GetForm(Frame f, ref Filter filter, VersusStageData stage) {
            List<TanoombaFormState> AvailibleForms = new List<TanoombaFormState>();
            for (int i = 0; i < (int) TanoombaFormState.Max; i++) {
                AvailibleForms.Add((TanoombaFormState) i);
            }
            bool Decided = false;
            TanoombaFormState TryForm = 0;

            while (!Decided || AvailibleForms.Count <= 0) {
                TryForm = (TanoombaFormState) FPMath.RoundToInt(f.RNG->Next() * AvailibleForms.Count);
                switch (TryForm) {
                #region Level Tranforms
                case TanoombaFormState.Coin: {
                    var coins = f.Filter<Coin>();
                    while (coins.NextUnsafe(out EntityRef OtherEntity, out Coin* coin)) {
                        //Pick A Random Coin
                    }
                    break;
                }
                case TanoombaFormState.Block: {
                    break;
                }
                case TanoombaFormState.Star: {
                    //Be Sure To Create The Starspawn Icon
                    break;
                }
                #endregion
                #region Enemy Transforms
                case TanoombaFormState.Goomba: {
                    break;
                }
                case TanoombaFormState.KoopaShell: {
                    break;
                }
                #endregion
                #region Hazard Transforms
                //Check If Hazards Contains This Object
                case TanoombaFormState.HeavyStone:
                case TanoombaFormState.LemmyBall: {
                    //Check If Hazards Contains This Object
                    break;
                }
                #endregion
                }
            }
            return TryForm;
        }

        #region Interactions
        public static void OnTanoombaMarioInteraction(Frame f, EntityRef thisEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->HitFrame > f.Number || (f.Number - mario->KnockbackTick) < 12 || mario->DamageInvincibilityFrames > 0 || tanoomba->DamageInvincibilityFrames > 0) {
                return;
            }
            //var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var mariophys = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            //FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            if (tanoomba->State == TanoombaState.Attacking && !attackedFromAbove) {
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.Normal, thisEntity, false);
            } else if (attackedFromAbove) {
                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                mario->DoEntityBounce = !groundpounded;
                tanoomba->HurtTanoomba(f, thisEntity, marioEntity, damageDirection.X > 0, (byte) (groundpounded ? KnockbackStrength.Groundpound : KnockbackStrength.Normal));
            } else {
                tanoomba->HurtTanoomba(f, thisEntity, marioEntity, damageDirection.X > 0, (byte) KnockbackStrength.CollisionBump);
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.CollisionBump, thisEntity, false);
            }
            return;
        }
        public static void OnTanoombaProjectileInteraction(Frame f, EntityRef thisEntity, EntityRef projectileEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->LastKnockback != 255 || tanoomba->DamageInvincibilityFrames > 0) {
                return;
            }

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                f.Unsafe.GetPointer<Tanoomba>(thisEntity)->HurtTanoomba(f, thisEntity, projectileEntity, !projectile->FacingRight, (byte) KnockbackStrength.FireballBump);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, thisEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
        }
        #endregion

        #region Signals

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
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

            switch (Dis->Type) {
            case ThrowingObjectType.RedPow:
            case ThrowingObjectType.BluePow:
                // Activate These
                break;
            case ThrowingObjectType.Freezie:
                // Break This
                break;
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Tanoomba* tanoomba)) {
                tanoomba->Respawn(f, entity);
            }
        }
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Tanoomba* tanoomba)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            //uhh i would put specific hazard spawn data here

            /*switch (Dis->Type) {
            case ThrowingObjectType.Basic:
            case ThrowingObjectType.Stone:
            case ThrowingObjectType.Spring:
            case ThrowingObjectType.RedPow:
            case ThrowingObjectType.BluePow: //Bluepow And Red Pow Are Considerd Varients Of Eachother
            case ThrowingObjectType.Barrel:
            case ThrowingObjectType.Freezie:
            case ThrowingObjectType.CoinBox:
            case ThrowingObjectType.PropellerBox:
            case ThrowingObjectType.BillBlock:
            case ThrowingObjectType.CannonBox:
            case ThrowingObjectType.Fridge:
                break;
            }*/
        }
        #endregion
    }
}
