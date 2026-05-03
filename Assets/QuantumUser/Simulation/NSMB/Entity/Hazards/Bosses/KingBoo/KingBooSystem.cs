using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Numerics;
using UnityEngine;
using UnityEngine.Windows;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class KingBooSystem : SystemMainThreadEntityFilter<KingBoo, KingBooSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnIceBlockBroken, ISignalOnBobombExplodeEntity {
        public struct Filter {
            public EntityRef Entity;
            public KingBoo* KingBoo;
            public Boss* Boss;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* freezable;
        }

        //TODO:
        //stop cyote sound if bowser fell in the pit
        //make boss with boss interactions better!


        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, KingBoo>(f, OnMarioKingBooInteraction);
            f.Context.Interactions.Register<Projectile, KingBoo>(f, OnProjectileKingBooInteraction);
            f.Context.Interactions.Register<Boss, KingBoo>(f, OnBossKingBooInteraction);
            f.Context.Interactions.Register<Enemy, KingBoo>(f, OnEnemyKingBooInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var kingboo = filter.KingBoo;
            var entity = filter.Entity;
            var boss = filter.Boss;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.Collider;

            if (boss->Dead) {
                physicsObject->IsFrozen = true;
                return;
            }
            if (filter.freezable->IsFrozen(f)) {
                return;
            }

            //Decide Action
            FPVector2 DirectionalInput = FPVector2.Zero;
            bool Jumpheld = true;
            bool FireballHeld = false;
            bool HasTarget = !QuantumUtils.Decrement(ref boss->iframes);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                Jumpheld = inputs.Jump.IsDown;
                FireballHeld = inputs.PowerupAction.IsDown;
                if (inputs.Left.IsDown ^ inputs.Right.IsDown) {
                    DirectionalInput.X = (inputs.Left.IsDown ? -1 : 1);
                }
                if (inputs.Down.IsDown ^ inputs.Up.IsDown) {
                    DirectionalInput.Y = (inputs.Down.IsDown ? -1 : 1);
                }
                DirectionalInput = DirectionalInput.Normalized;
                mario->FacingRight = boss->FacingRight;
            } else {
                Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var TargetEntity, out var distance);

                if (kingboo->waitTime > 0) {
                    kingboo->waitTime--;
                }

                //Boss Ai
                if (distance > 10) {
                    //Wander
                    DirectionalInput.X = boss->FacingRight ? 1 : -1;
                    DirectionalInput.Y = physicsObject->Velocity.Y;
                    kingboo->NeedsNewTarget = true;
                } else {
                    HasTarget = true;
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(TargetEntity);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(TargetEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(TargetEntity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                    FP absDif = FPMath.Abs(ourPos.X - theirPos.X);

                    //Get New Location Somewhere Around Our Target
                    if (kingboo->NeedsNewTarget) {
                        kingboo->NeedsNewTarget = false;
                        kingboo->waitTime = 30;
                        kingboo->TargetPosition = marioTransform->Position + new FPVector2((damageDirection.X > 0 ? 1 : -1) * f.RNG->Next(FP._0_50, FP._3), f.RNG->Next(-FP._1, FP._3));
                        kingboo->TargetPosition.Y = FPMath.Clamp(kingboo->TargetPosition.Y, stage.CameraMinPosition.Y + FP._0_50, stage.CameraMaxPosition.Y - 2);
                    }

                    //Move To Target Position
                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, kingboo->TargetPosition, out FPVector2 a, out FPVector2 b);
                    if (FPVector2.Distance(a, b) > 0) {
                        var input2 = (b - a).Normalized;
                        if (input2.X != 0) {
                            DirectionalInput.X = (input2.X > 0 ? 1 : -1);
                        }
                        if (input2.Y != 0) {
                            DirectionalInput.Y = (input2.Y > 0 ? 1 : -1);
                        }
                    }

                    //Other Behaviors
                    if (kingboo->waitTime == 0) {
                        if (absDif < Constants._0_66 || absDif > 7) {
                            //Too Close
                            kingboo->NeedsNewTarget = true;
                        } else {
                            if (kingboo->ProjectileShots == 0) {
                                //Refresh Attack Duration
                                kingboo->RngFireball = (byte) f.RNG->Next(3, 4);
                            }
                            if (kingboo->RngFireball >= kingboo->ProjectileShots) {
                                //Attack
                                DirectionalInput.X = damageDirection.X < 0 ? -1 : 1;
                                FireballHeld = true;
                                if (kingboo->RngFireball < kingboo->ProjectileShots+1) {
                                    kingboo->NeedsNewTarget |= f.RNG->Next(0, 2) == 1;
                                }
                            }
                        }
                    }
                }
            }

            if (transform->Position.Y < stage.CameraMinPosition.Y + FP._0_50) {
                //transform->Position.Y = stage.CameraMinPosition.Y + FP._0_50;
                physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y + (FP._0_20 * ((stage.CameraMinPosition.Y + FP._0_50) - transform->Position.Y)), 4);
            } else if (transform->Position.Y > stage.CameraMaxPosition.Y - 2) {
                //transform->Position.Y = stage.CameraMaxPosition.Y - 2;
                physicsObject->Velocity.Y = FPMath.Max(physicsObject->Velocity.Y + (FP._0_20 * ((stage.CameraMaxPosition.Y - 2) - transform->Position.Y)), -4);
            }

            //State Calcs
            switch (kingboo->State) {
            case KingBooState.Laughing:
                physicsObject->Velocity *= Constants._0_95;
                kingboo->ReusableTimer++;
                if (kingboo->ReusableTimer > 100 || boss->iframes != 0) {
                    kingboo->ReusableTimer = 0;
                    kingboo->waitTime = 30;
                    kingboo->State = KingBooState.Floating;
                }
                break;
            case KingBooState.Floating:
                HandleMovement(DirectionalInput, true, 4);

                if (FireballHeld) {
                    //Start Barf
                    kingboo->ProjectileShots = 0;
                    kingboo->State = KingBooState.Barfing;
                }
                break;
            case KingBooState.Barfing:
                HandleMovement(FPVector2.Zero, false, 4);
                kingboo->ReusableTimer++;
                int CycleTimer = kingboo->ReusableTimer % 35;
                if (kingboo->ReusableTimer >= 105) {
                    //cannot shoot a 4th
                    if (kingboo->ReusableTimer >= 150) {
                        //if you try to shoot a 5th sucking won't be an option
                        kingboo->ReusableTimer--;
                        if (!FireballHeld) {
                            kingboo->ReusableTimer = 0;
                            kingboo->State = KingBooState.Floating;
                        }
                        break;
                    }
                } else {
                    if (CycleTimer == 6) {
                        CreateProjectile(new FPVector2(boss->FacingRight ? 1 : -1, 0), kingboo->ReusableTimer/35);
                    }
                }

                if (!FireballHeld && kingboo->ReusableTimer > 20 && (CycleTimer > 20 || CycleTimer < 6)) {
                    kingboo->ReusableTimer = 10;// (byte)(10 * (4 - kingboo->ProjectileShots));
                    kingboo->State = KingBooState.Sucking;
                }
                break;
            case KingBooState.Sucking:
                HandleMovement(DirectionalInput, false, 4);
                physicsObject->Velocity.Y *= Constants.BallSlowDownMultiplier;
                var disRef2 = boss->BossGetOwnerResponsible(entity);
                if (QuantumUtils.Decrement(ref kingboo->ReusableTimer)) {
                    //Pull In All Projectiles We Own
                    var projectiles = f.Filter<ThrowingObject, Holdable, PhysicsObject, Hazard>();
                    byte Count = 0;
                    while (projectiles.NextUnsafe(out EntityRef throwableEntity, out ThrowingObject* throwable, out Holdable* throwholdable, out PhysicsObject* throwphys, out Hazard* throwhazard)) {
                        if (throwholdable->PreviousHolder == disRef2 && f.Exists(throwableEntity)) {
                            Count++;
                            QuantumUtils.UnwrapWorldLocations(f, transform->Position + (FPVector2.Up * FP._0_33), f.Unsafe.GetPointer<Transform2D>(throwableEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                            var Direction = (ourPos - theirPos).Normalized;
                            FP TargetDirection = FPMath.Atan2(Direction.Y, Direction.X);

                            //Move Torwards King Boo
                            throwable->ReusableTimer -= f.DeltaTime;
                            if (throwable->Varient < 3) {
                                if (throwable->ReusableTimer < 0) {
                                    throwable->Varient = 4;
                                    throwphys->Velocity = FPVector2.Zero;
                                    throwphys->Gravity.Y = 0;
                                    throwphys->TerminalVelocity = -12;
                                    throwable->HitSomething = false;
                                    f.Events.PlayComboSound(throwableEntity, 1);
                                }
                            } else if (throwable->Varient == 4) {
                                throwphys->Velocity = (new FPVector2(FPMath.Cos(TargetDirection), FPMath.Sin(TargetDirection))) * -(FPMath.Max(throwable->ReusableTimer, -4 - ((throwable->ReusableTimer * throwable->ReusableTimer)/10)) * 16);
                                if (throwable->ReusableTimer < -FP._0_25 && !throwable->Thrown) {
                                    throwable->Thrown = true;
                                }
                            }

                            if ((ourPos - theirPos).Magnitude < FP._0_50) {
                                throwhazard->LifeTime = 1;
                            } else {
                                throwhazard->LifeTime = 2;
                            }
                        }
                    }
                    if (Count == 0) {
                        kingboo->ReusableTimer = 0;
                        kingboo->State = KingBooState.Floating;
                    }
                }
                break;
            case KingBooState.Teleporting:
                HandleMovement(DirectionalInput, true, 4);
                break;
            case KingBooState.Knockback:
                HandleMovement(FPVector2.Zero, false, 4);
                kingboo->ReusableTimer++;
                if (kingboo->ReusableTimer > 90) {
                    kingboo->ReusableTimer = 0;
                    kingboo->State = KingBooState.Floating;
                }
                break;
            }

            void HandleMovement(FPVector2 Direction, bool AllowTurnaround, FP max) {
                //get our speed stage
                bool overSpeed = physicsObject->Velocity.Magnitude > Constants._2_50 && kingboo->State != KingBooState.Teleporting;
                FP acc = FP._0_10;

                if (Direction == FPVector2.Zero) {
                    if (FPMath.Abs(physicsObject->Velocity.X) <= FP._0_05 && FPMath.Abs(physicsObject->Velocity.Y) <= FP._0_05) {
                        physicsObject->Velocity = FPVector2.Zero;
                        return;
                    } else {
                        Direction = physicsObject->Velocity.Normalized * -1;
                    }
                } else if (AllowTurnaround && Direction.X != 0) {
                    boss->FacingRight = Direction.X > 0;
                }
                //Convert Our Vector Direction Into Radian
                FP TargetDirection = FPMath.Atan2(Direction.Y, Direction.X);

                // accelerate
                physicsObject->Velocity += (new FPVector2(FPMath.Cos(TargetDirection), FPMath.Sin(TargetDirection))) * acc;

                //clamp our velocity angled
                FP CurrentDirection = FPMath.Atan2(physicsObject->Velocity.Y, physicsObject->Velocity.X);
                var TruMax = new FPVector2(FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_50, max * FPMath.Abs(FPMath.Cos(CurrentDirection))),
                    FPMath.Max(FPMath.Abs(physicsObject->Velocity.Y) - FP._0_50, max * FPMath.Abs(FPMath.Sin(CurrentDirection))));
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -TruMax.X, TruMax.X);
                physicsObject->Velocity.Y = FPMath.Clamp(physicsObject->Velocity.Y, -TruMax.Y, TruMax.Y);
            }
            void CreateProjectile(FPVector2 Direction, FP Bonus) {
                FPVector2 spawnPos = transform->Position + new FPVector2(boss->FacingRight ? FP._0_50 : -FP._0_50, FP._0_33);
                EntityRef newEntity = f.Create(kingboo->BlueFire);
                var throwhazard = f.Unsafe.GetPointer<Hazard>(newEntity);
                FP radian = FPMath.Atan2(Direction.Y, Direction.X);
                Direction = new FPVector2(FPMath.Cos(radian), FPMath.Sin(radian));

                f.Unsafe.GetPointer<Transform2D>(newEntity)->Position = spawnPos;
                f.Unsafe.GetPointer<PhysicsObject>(newEntity)->Velocity = ((Direction * (4 + FP._0_10)) + (FPVector2.Up*Bonus*3));
                throwhazard->IsHazard = true;
                throwhazard->LifeTime = 250;
                f.Unsafe.GetPointer<ThrowingObject>(newEntity)->Thrown = true;
                f.Unsafe.GetPointer<Holdable>(newEntity)->PreviousHolder = boss->BossGetOwnerResponsible(entity);
                kingboo->ProjectileShots++;

                f.Events.KingBooBarf(entity);
            }
        }

        #region Interactions
        public void OnMarioKingBooInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var kingboo = f.Unsafe.GetPointer<KingBoo>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            switch (boss->BossMarioContact(f, thisEntity, marioEntity, damageDirection, false)) {
            case bossMarioContactResult.Harm:
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump, false);
                physicsObject->Velocity.Y -= 1;
                kingboo->NeedsNewTarget = true;
                break;
            case bossMarioContactResult.SuperHarm:
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.Groundpound, false);
                f.Events.KingBooKnockbacked(thisEntity);
                physicsObject->Velocity.Y -= 1;
                kingboo->State = KingBooState.Knockback;
                kingboo->ReusableTimer = 0;
                boss->FacingRight = damageDirection.X > 0;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.X = damageDirection.X > 0 ? -7 : 7;
                break;
            case bossMarioContactResult.Bump:
                boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
                physicsObject->Velocity.Y -= 1;
                break;
            }
        }
        public void OnProjectileKingBooInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;
            var kingBoo = f.Unsafe.GetPointer<KingBoo>(thisEntity);
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == thisEntity || projectile->Owner == boss->ControllerPlayer) {
                return; //hang on, this is OUR projectile!
            }
            var projectileAsset = f.FindAsset(projectile->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                boss->BossHarmed(f, thisEntity, projectile->FacingRight, KnockbackStrength.FireballBump, false);
                kingBoo->NeedsNewTarget = true;
                break;
            }
            case ProjectileEffectType.Freeze: {
                kingBoo->ReusableTimer = 0;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity = FPVector2.Zero;
                IceBlockSystem.Freeze(f, thisEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
        }
        public void OnBossKingBooInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->Dead || otherboss->Dead)
                return;
            f.Signals.BossToBossInteraction(thisEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, thisEntity);
        }
        public void OnEnemyKingBooInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;

            if (f.Unsafe.TryGetPointer(enemyEntity, out Goomba* goomba)) {
                goomba->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Koopa* koopa)) {
                if (koopa->IsKicked) {
                    boss->BossHarmed(f, thisEntity, f.Unsafe.GetPointer<Enemy>(enemyEntity)->FacingRight, KnockbackStrength.FireballBump, false);
                }
                koopa->Kill(f, enemyEntity, enemyEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out BulletBill* bill)) {
                bill->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Bobomb* bomb)) {
                bomb->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out PiranhaPlant* plant)) {
                plant->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            }
        }
        #endregion

        #region Signals
        public void BossDeath(Frame f, EntityRef thisEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out KingBoo* kingboo)) {
                return;
            }

            kingboo->State = KingBooState.Laughing;
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Interactable* inter)) {
                inter->ColliderDisabled = false;
            }
        }
        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity, ExplosionType type) {
            if (f.Unsafe.TryGetPointer(entity, out Boss* boss)) {
                boss->BossHarmed(f, entity, boss->FacingRight, KnockbackStrength.Normal, true);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out KingBoo* kingboo)) {
                return;
            }

            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;
        }
        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(thisEntity, out KingBoo* kingkoo)) {
                return;
            }

            var otherboss = f.Unsafe.GetPointer<Boss>(otherEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var thisPhys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
        }
        #endregion
    }
}
