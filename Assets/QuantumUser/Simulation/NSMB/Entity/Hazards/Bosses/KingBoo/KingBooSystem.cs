using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Drawing.Drawing2D;
using System.Numerics;
using UnityEngine;
using UnityEngine.Windows;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class KingBooSystem : SystemMainThreadEntityFilter<KingBoo, KingBooSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnIceBlockBroken {
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
                                kingboo->RngFireball = (byte) f.RNG->Next(2, 4);
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
            void CreateProjectile(FPVector2 Direction) {
                FPVector2 spawnPos = transform->Position + new FPVector2(boss->FacingRight ? FP._0_50 : -FP._0_50, FP._0_33);
                EntityRef newEntity = f.Create(kingboo->BlueFire);
                var throwhazard = f.Unsafe.GetPointer<Hazard>(newEntity);
                FP radian = FPMath.Atan2(Direction.Y, Direction.X);
                Direction = new FPVector2(FPMath.Cos(radian), FPMath.Sin(radian));

                f.Unsafe.GetPointer<Transform2D>(newEntity)->Position = spawnPos;
                f.Unsafe.GetPointer<PhysicsObject>(newEntity)->Velocity = ((Direction * (4 + FP._0_10)) + FPVector2.Up);
                throwhazard->IsHazard = throwhazard->IsActive = true;
                throwhazard->LifeTime = 250;
                f.Unsafe.GetPointer<ThrowingObject>(newEntity)->Thrown = true;
                f.Unsafe.GetPointer<Holdable>(newEntity)->PreviousHolder = boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : entity;
                kingboo->ProjectileShots++;
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
                int CycleTimer = kingboo->ReusableTimer % 41;
                if (kingboo->ReusableTimer >= 123) {
                    //cannot shoot a 4th
                    if (kingboo->ReusableTimer >= 170) {
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
                        CreateProjectile(new FPVector2(boss->FacingRight ? 1 : -1, 0));
                    }
                }

                if (!FireballHeld && kingboo->ReusableTimer > 20 && (CycleTimer > 20 || CycleTimer < 6)) {
                    kingboo->ReusableTimer = 10;// (byte)(10 * (4 - kingboo->ProjectileShots));
                    kingboo->State = KingBooState.Sucking;
                    //Prepare Suck
                    var projectiles = f.Filter<ThrowingObject, Holdable, PhysicsObject>();
                    var disRef = boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : entity;
                    while (projectiles.NextUnsafe(out EntityRef projectileEntity, out ThrowingObject* throwable, out Holdable* throwholdable, out PhysicsObject* throwphys)) {
                        if (throwholdable->PreviousHolder == disRef) {
                            throwable->ReusableTimer = 3;
                        }
                    }
                }
                break;
            case KingBooState.Sucking:
                HandleMovement(DirectionalInput, false, 4);
                physicsObject->Velocity.Y *= Constants.BallSlowDownMultiplier;
                var disRef2 = boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : entity;
                if (QuantumUtils.Decrement(ref kingboo->ReusableTimer)) {
                    //Pull In All Projectiles We Own
                    var projectiles = f.Filter<ThrowingObject, Holdable, PhysicsObject, Hazard>();
                    byte Count = 0;
                    while (projectiles.NextUnsafe(out EntityRef throwableEntity, out ThrowingObject* throwable, out Holdable* throwholdable, out PhysicsObject* throwphys, out Hazard* throwhazard)) {
                        if (throwholdable->PreviousHolder == disRef2) {
                            Count++;
                            QuantumUtils.UnwrapWorldLocations(f, transform->Position + (FPVector2.Up * FP._0_33), f.Unsafe.GetPointer<Transform2D>(throwableEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                            var Direction = (ourPos - theirPos).Normalized;
                            FP TargetDirection = FPMath.Atan2(Direction.Y, Direction.X);

                            //Move Torwards King Boo
                            throwable->ReusableTimer -= f.DeltaTime * 8;
                            if (throwable->ReusableTimer <= 1) {
                                if (throwable->Varient < 3) {
                                    throwable->Varient = 4;
                                    throwphys->Velocity = FPVector2.Zero;
                                    throwphys->Gravity.Y = 0;
                                    throwable->HitSomething = false;
                                    if (!throwable->Thrown) {
                                        throwable->Thrown = true;
                                        f.Events.PlayComboSound(throwableEntity, 1);
                                    }
                                }
                                throwphys->Velocity = (new FPVector2(FPMath.Cos(TargetDirection), FPMath.Sin(TargetDirection))) * -FPMath.Max(throwable->ReusableTimer, -4 - ((throwable->ReusableTimer * throwable->ReusableTimer)/10));
                            } else {
                                throwphys->Velocity *= Constants._0_90;
                                throwphys->Gravity.Y *= Constants.BallSlowDownMultiplier;
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
                /*
            case BowserState.Attacking:
                FP clamper2 = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_10, FP._1_25);
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper2, clamper2);

                bowser->ReusableTimer++;
                if (bowser->ReusableTimer > 20) {
                    if (!bowser->IsDry) {
                        if (bowser->ReusableTimer == 21)
                            f.Events.BowserAttack(filter.Entity, BowserAttackType.MegaAttack);
                        bowser->ReusableTimer++;
                    }
                    //create multiple, if dry, create bones instead
                    int Mod = bowser->IsDry ? 26 : 16;
                    if (bowser->ReusableTimer % Mod == 0) {
                        if (bowser->IsDry) {
                            f.Events.BowserAttack(filter.Entity, BowserAttackType.BoneThrow);
                        }
                        f.Events.BowserShoot(filter.Entity, bowser->IsDry);
                        FP Direc = (((((FP) bowser->ReusableTimer) / Mod) - 4) / 3);
                        CreateProjectile(bowser->IsDry ? bowser->Bone : bowser->Fireball, new FPVector2(1, Direc), bowser->IsDry ? 12 + (2 * Direc) : 0);
                    }
                    if (bowser->ReusableTimer > 100 || !Sprint) {
                        bowser->ReusableTimer = 0;
                        bowser->State = BowserState.Walking;
                        bowser->AttackCooldown = 80;
                        bowser->VolleyCooldown = 80;
                    }
                } else if (!Sprint) {
                    //create one
                    f.Events.BowserShoot(filter.Entity, false);
                    CreateProjectile(bowser->IsDry ? bowser->BlueFire : bowser->Fireball, new FPVector2(1, updowninput / 3), 0);

                    if (bowser->VolleyCooldown > 0) {
                        bowser->AttackCooldown = 50;
                        bowser->VolleyCooldown = 50;
                    } else {
                        bowser->VolleyCooldown = 50;
                    }
                    bowser->ReusableTimer = 0;
                    bowser->State = BowserState.Walking;
                }

                void CreateProjectile(AssetRef<EntityPrototype> prototype, FPVector2 Direction, FP VerticalBonus) {
                    FPVector2 spawnPos = transform->Position + new FPVector2(boss->FacingRight ? FP._0_50 : -FP._0_50, Constants._0_66);
                    EntityRef newEntity = f.Create(prototype);
                    var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
                    projectile->Initialize(f, newEntity, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : entity, spawnPos, boss->FacingRight, false);
                    var projPhys = f.Unsafe.GetPointer<PhysicsObject>(newEntity);
                    FP radian = FPMath.Atan2(Direction.Y, Direction.X);
                    Direction = new FPVector2(FPMath.Cos(radian), FPMath.Sin(radian));
                    projPhys->Velocity = (Direction * projectile->Speed) + (FPVector2.Up * VerticalBonus);
                    projectile->Speed = projPhys->Velocity.X;
                }*/
                break;
            }
        }

        #region Interactions
        public void OnMarioKingBooInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var kingboo = f.Unsafe.GetPointer<KingBoo>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_50;

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;

            bool bossHarmed = false;
            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.Groundpound, true);
                bossHarmed = true;

            } else if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump, false);
                        bossHarmed = true;
                    }
                    mario->DoEntityBounce = true;
                } else {
                    boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump, false);
                    bossHarmed = true;
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;
                marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X + (((theirPos - ourPos) * 10).Normalized.X * 3), -5, 5);

            } else if (!mario->IsInKnockback) {
                // Bump
                if (marioPhysicsObject->IsTouchingGround) {
                    mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 1, KnockbackStrength.CollisionBump, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : thisEntity);
                    boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
                } else {
                    marioPhysicsObject->Velocity.X = (marioPhysicsObject->Velocity.X/2) + boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
                }
            }

            if (bossHarmed) {
                kingboo->NeedsNewTarget = true;
                if (groundpounded) {
                    f.Events.BowserKnockbacked(thisEntity);
                    kingboo->State = KingBooState.Knockback;
                    kingboo->ReusableTimer = 0;
                    boss->FacingRight = damageDirection.X > 0;
                    f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.X = damageDirection.X > 0 ? -7 : 7;
                }
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

            f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
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
                goomba->Kill(f, enemyEntity, thisEntity, KillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Koopa* koopa)) {
                if (koopa->IsKicked) {
                    boss->BossHarmed(f, thisEntity, f.Unsafe.GetPointer<Enemy>(enemyEntity)->FacingRight, KnockbackStrength.FireballBump, false);
                }
                koopa->Kill(f, enemyEntity, enemyEntity, KillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out BulletBill* bill)) {
                bill->Kill(f, enemyEntity, thisEntity, KillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Bobomb* bomb)) {
                bomb->Kill(f, enemyEntity, thisEntity, KillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out PiranhaPlant* plant)) {
                plant->Kill(f, enemyEntity, thisEntity, KillReason.Special);
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
            hazard->LifeTime = 130;

            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                mario->RelieveFromBoss(f, boss->ControllerPlayer);
            } else {
                //spawn star(s?)
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
                EntityRef newStarEntity = f.Create(gamemode.BigStarPrototype);
                var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                f.Unsafe.GetPointer<Transform2D>(newStarEntity)->Position = f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position;
                newStar->InitializeMovingStar(f, stage, newStarEntity, boss->FacingRight ? 1 : 2);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Interactable* inter)) {
                inter->ColliderDisabled = false;
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out KingBoo* kingboo)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            //relocate
            //boss->ControllerPlayer
            //bowser->IsDry = hazardata.SpecialValues[0].BaseValue == 1;
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
