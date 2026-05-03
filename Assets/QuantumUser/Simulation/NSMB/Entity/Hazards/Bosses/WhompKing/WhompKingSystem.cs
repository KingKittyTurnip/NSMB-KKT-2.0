using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;
using static IInteractableTile;

namespace Quantum {
    
    public unsafe class WhompKingSystem : SystemMainThreadEntityFilter<WhompKing, WhompKingSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnIceBlockBroken, ISignalOnBobombExplodeEntity {
        public struct Filter {
            public EntityRef Entity;
            public WhompKing* WhompKing;
            public Boss* Boss;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, WhompKing>(f, OnMarioWhompKingInteraction);
            f.Context.Interactions.Register<Projectile, WhompKing>(f, OnProjectileWhompKingInteraction);
            f.Context.Interactions.Register<Boss, WhompKing>(f, OnBossWhompKingInteraction);
            f.Context.Interactions.Register<Enemy, WhompKing>(f, OnEnemyWhompKingInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var whompking = filter.WhompKing;
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
            FP leftrightinput = 0;
            bool Jump = false;
            bool Jumpheld = false;
            bool Slamming = false;
            bool Pounding = false;
            bool HasTarget = !QuantumUtils.Decrement(ref boss->iframes);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                Jump = inputs.Jump.WasPressed;
                Jumpheld = inputs.Jump.IsDown;
                Slamming = inputs.PowerupAction.IsDown;
                Pounding = inputs.Down.IsDown;
                if (inputs.Left.IsDown || inputs.Right.IsDown) {
                    leftrightinput = (inputs.Left.IsDown == inputs.Right.IsDown) ? -(physicsObject->Velocity.X * FP._0_10) : (inputs.Left.IsDown ? -1 : 1);
                    HasTarget = true;
                }
                mario->FacingRight = boss->FacingRight;
            } else {
                Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var TargetEntity, out var distance);

                //Boss Ai
                if (distance > 10) {
                    //wander
                    FPVector2 checkPosition = transform->Position + (FPVector2.Right * FP._0_20 * (boss->FacingRight ? 1 : -1));
                    if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, 5, out var hit)) {
                        Jump = true;
                    }
                    leftrightinput = boss->FacingRight ? 1 : -1;
                } else {
                    HasTarget = true;
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(TargetEntity);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(TargetEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(TargetEntity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                    FP absDif = FPMath.Abs(theirPos.X - ourPos.X);

                    Jumpheld = true;

                    if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || ((theirPos.Y - ourPos.Y) > 1 && absDif < 5)) {
                        Jump = true;
                    }
                    if ((boss->FacingRight ^ (damageDirection.X < 0)) && FPMath.Abs(theirPos.Y - ourPos.Y) < FP._1_25 && absDif < 2 && boss->iframes == 0) {
                        Slamming = true;
                    } else if (absDif < 2) {
                        whompking->SlamCooldown = 20;
                        leftrightinput = damageDirection.X > 0 ? -1 : 1;
                        Jump |= absDif < 1;
                    } else if (absDif > 4) {
                        leftrightinput = damageDirection.X < 0 ? -1 : 1;
                    } else {
                        leftrightinput = boss->FacingRight ? 1 : -1;
                    }
                }
                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    Jump = true;
                }
            }
            if (!Pounding && !Slamming && whompking->State != WhompKingState.SlamAttacking)
                whompking->PrevSlamPounded = false;

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.WhompKingpitfall(filter.Entity);
                boss->BossHarmed(f, entity, !boss->FacingRight, KnockbackStrength.FireballBump, false);
                physicsObject->Velocity.Y = 16;
                whompking->ReusableTimer = 0;
                whompking->State = WhompKingState.Jumping;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = 0;
                physicsObject->TerminalVelocity = -20;
            }

            QuantumUtils.Decrement(ref whompking->SlamCooldown);

            //State Calcs
            switch (whompking->State) {
            case WhompKingState.Walling:
                physicsObject->Velocity.X *= Constants._0_95;
                if (physicsObject->IsTouchingGround || whompking->ReusableTimer != 0) {
                    //if (whompking->ReusableTimer == 0)
                    //    f.Events.PeteyWakeup(filter.Entity, false);
                    whompking->ReusableTimer++;
                    if (whompking->ReusableTimer > 100 || boss->iframes != 0) {
                        collider->Shape.Centroid.X = 0;
                        collider->Shape.Centroid.Y = whompking->Hitbox.Y;
                        collider->Shape.Box.Extents = whompking->Hitbox;
                        whompking->ReusableTimer = 0;
                        whompking->State = WhompKingState.Idling;
                    }
                }
                break;
            case WhompKingState.Idling:
                if (leftrightinput != 0) {
                    boss->FacingRight = leftrightinput > 0;
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 2 + FP._0_50);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper, clamper);
                } else {
                    physicsObject->Velocity.X *= Constants._0_90;
                }
                if (Jump || !physicsObject->IsTouchingGround) {
                    whompking->State = WhompKingState.Jumping;
                    if (Jump) {
                        physicsObject->IsTouchingGround = false;
                        physicsObject->Velocity.Y = 12;
                        physicsObject->TerminalVelocity = -20;
                        f.Events.WhompKingJump(filter.Entity);
                    }
                } else if (!whompking->PrevSlamPounded) {
                    if ((Pounding || Slamming) && whompking->SlamCooldown == 0) {
                        whompking->State = WhompKingState.SlamAttacking;
                        physicsObject->IsTouchingGround = whompking->HitATarget = false;
                        physicsObject->Velocity.X = Pounding ? 0 : boss->FacingRight ? 10 : -10;
                        physicsObject->Velocity.Y = 6;
                        whompking->PrevSlamPounded = true;
                    }
                }

                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround)
                    f.Events.WhompKingLand(filter.Entity, false);
                break;
            case WhompKingState.Jumping:
                if (leftrightinput != 0) {
                    boss->FacingRight = leftrightinput > 0;
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 4);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_20), -clamper, clamper);
                }
                if (!Jumpheld) {
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, 4);
                }
                if (!whompking->PrevSlamPounded) {
                    if ((Slamming || Pounding) && whompking->SlamCooldown == 0) {
                        whompking->State = WhompKingState.SlamAttacking;
                        physicsObject->IsTouchingGround = whompking->HitATarget = false;
                        physicsObject->Velocity.X = Pounding ? 0 : boss->FacingRight ? 10 : -10;
                        physicsObject->Velocity.Y = 0;
                        whompking->PrevSlamPounded = true;
                    }
                }

                if (physicsObject->IsTouchingGround) {
                    whompking->State = WhompKingState.Idling;
                    f.Events.WhompKingLand(filter.Entity, false);
                }
                break;
            case WhompKingState.SlamAttacking:
                physicsObject->BreakMegaObjects = true;
                if (whompking->HitATarget) {
                    whompking->HitATarget = false;
                    if (!physicsObject->IsFrozen) {
                        whompking->State = WhompKingState.SlamHit;
                        whompking->ReusableTimer = 0;
                        physicsObject->Gravity.Y = -8;
                        physicsObject->Velocity.Y = 6;
                        physicsObject->BreakMegaObjects = false;
                        break;
                    }
                }
                whompking->ReusableTimer++;
                if (whompking->ReusableTimer < 8) {
                    collider->Shape.Centroid.Y = whompking->HurtingHitbox.Y;
                    collider->Shape.Box.Extents = whompking->HurtingHitbox;
                } else {
                    if (physicsObject->IsFrozen) {
                        if (whompking->ReusableTimer >= 120 || (whompking->HitATarget && whompking->ReusableTimer > 30)) {
                            whompking->State = WhompKingState.Idling;
                            whompking->ReusableTimer = 0;
                            physicsObject->IsFrozen = false;
                            collider->Shape.Centroid.Y = whompking->Hitbox.Y;
                            collider->Shape.Box.Extents = whompking->Hitbox;
                            whompking->SlamCooldown = 20;
                            physicsObject->BreakMegaObjects = false;
                        }
                    } else if (physicsObject->IsTouchingGround) {
                        f.Events.WhompKingLand(filter.Entity, true);
                        physicsObject->Velocity.X = 0;
                        physicsObject->Velocity.Y = 0;
                        physicsObject->IsFrozen = true;
                        whompking->ReusableTimer = 30;
                        collider->Shape.Centroid.Y = whompking->FallenBox.Y;
                        collider->Shape.Box.Extents = whompking->FallenBox;
                    } else {
                        physicsObject->Velocity.X *= Constants.BallSlowDownMultiplier;
                    }
                }
                break;
            case WhompKingState.SlamHit:
                if (leftrightinput != 0) {
                    boss->FacingRight = leftrightinput > 0;
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 8);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_20), -clamper, clamper);
                }
                if (physicsObject->IsTouchingGround) {
                    whompking->State = WhompKingState.Idling;
                    physicsObject->Gravity.Y = -30;
                    whompking->HitATarget = false;
                }
                break;
            case WhompKingState.Knockbacked:
                physicsObject->Velocity.X *= Constants._0_95;
                physicsObject->IsFrozen = false;
                collider->Shape.Centroid.Y = whompking->Hitbox.Y;
                collider->Shape.Box.Extents = whompking->Hitbox;
                if (FPMath.Abs(physicsObject->Velocity.X) < 1) {
                    whompking->State = WhompKingState.Idling;
                    if (boss->iframes > 0)
                        boss->iframes = 30;
                }
                break;
            }
            BrickInteraction(f, ref filter);
        }

        public static void BrickInteraction(Frame f, ref Filter filter) {
            var physicsObject = filter.PhysicsObject;

            if (physicsObject->IsTouchingCeiling || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->BreakMegaObjects) {

                QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                foreach (var contact in contacts) {
                    FP dot = FPVector2.Dot(contact.Normal, FPVector2.Down);
                    if (dot < -FP._0_75 && !physicsObject->BreakMegaObjects) {
                        continue;
                    }

                    // Floor tiles.
                    var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                    var tileInstance = stage.GetTileRelative(f, contact.Tile);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        it.Interact(f, filter.Entity, InteractionDirection.Up,
                            contact.Tile, tileInstance, out bool tempPlayBumpSound);
                    }
                }
            }
        }

        #region Interactions
        public void OnMarioWhompKingInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var whompking = f.Unsafe.GetPointer<WhompKing>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            bool slamming = whompking->State == WhompKingState.SlamAttacking && !physicsObject->IsFrozen;
            bool attackedFromAbove = !slamming && FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25 && !mario->IsInKnockback;
            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            bool vulnrable = whompking->State == WhompKingState.SlamAttacking && physicsObject->IsFrozen;
            bool kingHarmed = false;

            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, vulnrable ? groundpounded ? KnockbackStrength.Groundpound : KnockbackStrength.Normal : KnockbackStrength.FireballBump, true);
                kingHarmed = true;
                vulnrable |= groundpounded;

            } else if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        boss->BossHarmed(f, thisEntity, damageDirection.X < 0, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump, true);
                        kingHarmed = true;
                    }
                    mario->DoEntityBounce = true;
                } else {
                    boss->BossHarmed(f, thisEntity, damageDirection.X < 0, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump, true);
                    kingHarmed = true;
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;
                marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X + (((theirPos - ourPos) * 10).Normalized.X * 3), -5, 5);

            } else if (mario->IsDamageable && mario->DoKnockback(f, marioEntity, damageDirection.X < 0, slamming ? 2 : 1, slamming ? KnockbackStrength.Groundpound : KnockbackStrength.CollisionBump, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : thisEntity)) {
                whompking->HitATarget = true;
                if (damageDirection.Y < 0)
                    physicsObject->Velocity.Y = 6;
            }

            if (kingHarmed) {
                if (vulnrable) {
                    whompking->State = WhompKingState.Knockbacked;
                    physicsObject->BreakMegaObjects = false;
                    whompking->ReusableTimer = 0;
                    physicsObject->IsFrozen = false;
                    boss->FacingRight = damageDirection.X > 0;
                    physicsObject->Velocity.X = damageDirection.X > 0 ? -7 : 7;
                    f.Events.WhompKingKnockbacked(thisEntity);
                }
            }
        }
        public void OnProjectileWhompKingInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;
            var whompking = f.Unsafe.GetPointer<WhompKing>(thisEntity);
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == boss->ControllerPlayer) {
                return; //hang on, this is OUR projectile!
            }
            var projectileAsset = f.FindAsset(projectile->Asset);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            if (whompking->State == WhompKingState.SlamAttacking && whompking->ReusableTimer < 10) {
            } else {

                switch (projectileAsset.Effect) {
                case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
                case ProjectileEffectType.Fire: {
                    boss->BossHarmed(f, thisEntity, projectile->FacingRight, KnockbackStrength.FireballBump, false);
                    break;
                }
                case ProjectileEffectType.Freeze: {
                    //f.Events.PeteyJump(thisEntity);
                    whompking->State = WhompKingState.Idling;
                    whompking->ReusableTimer = 0;
                    whompking->HitATarget = false;
                    physicsObject->IsFrozen = false;
                    IceBlockSystem.Freeze(f, thisEntity);
                    break;
                }
                }
                f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
            }

        }
        public void OnBossWhompKingInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->Dead || otherboss->Dead)
                return;
            f.Signals.BossToBossInteraction(thisEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, thisEntity);
        }
        public void OnEnemyWhompKingInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;
            var whompking = f.Unsafe.GetPointer<WhompKing>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            bool Invulnrable = whompking->State == WhompKingState.SlamAttacking && !physicsObject->IsFrozen;
            if (Invulnrable) {
                whompking->HitATarget = true;
            }

            if (f.Unsafe.TryGetPointer(enemyEntity, out Goomba* goomba)) {
                goomba->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Koopa* koopa)) {
                if (koopa->IsKicked && !Invulnrable) {
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
                || !f.Unsafe.TryGetPointer(thisEntity, out WhompKing* whompking)) {
                return;
            }

            whompking->State = WhompKingState.SlamAttacking;
            whompking->ReusableTimer = 30;
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
                || !f.Unsafe.TryGetPointer(thisEntity, out WhompKing* whompking)) {
                return;
            }

            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;
        }

        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(thisEntity, out WhompKing* whompking)) {
                return;
            }

            var otherboss = f.Unsafe.GetPointer<Boss>(otherEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var thisPhys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (whompking->State == WhompKingState.SlamAttacking && !thisPhys->IsFrozen) {
                whompking->HitATarget = true;
                otherboss->BossHarmed(f, otherEntity, damageDirection.X < 0, KnockbackStrength.Groundpound, true);
            } else {
                if (damageDirection.Y < 0) {
                    f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.Y = 6;
                    otherboss->BossHarmed(f, otherEntity, damageDirection.X < 0, KnockbackStrength.Normal, true);
                } else {
                    thisPhys->Velocity.X = damageDirection.X > 0 ? -4 : 4;
                }
                if (whompking->State == WhompKingState.SlamAttacking && thisPhys->IsFrozen)
                    whompking->ReusableTimer = 200;
            }
            thisPhys->Gravity.Y = -30;
        }
        #endregion
    }
}
