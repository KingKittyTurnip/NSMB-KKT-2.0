using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Drawing.Drawing2D;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class BowserSystem : SystemMainThreadEntityFilter<Bowser, BowserSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnIceBlockBroken {
        public struct Filter {
            public EntityRef Entity;
            public Bowser* Bowser;
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
            f.Context.Interactions.Register<MarioPlayer, Bowser>(f, OnMarioBowserInteraction);
            f.Context.Interactions.Register<Projectile, Bowser>(f, OnProjectileBowserInteraction);
            f.Context.Interactions.Register<Boss, Bowser>(f, OnBossBowserInteraction);
            f.Context.Interactions.Register<Enemy, Bowser>(f, OnEnemyBowserInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var bowser = filter.Bowser;
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
            FP updowninput = 0;
            //bool Groundpounding = false;
            bool Fireball = false;
            bool Jump = false;
            bool Jumpheld = true;
            bool Sprint = false;
            bool Groundpounding = false;
            //bool Crouching = false;
            bool HasTarget = !QuantumUtils.Decrement(ref boss->iframes);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                //Groundpounding = inputs.Down.WasPressed;
                Jump = inputs.Jump.WasPressed;
                Jumpheld = inputs.Jump.IsDown;
                Groundpounding = inputs.Down.WasPressed;
                Fireball = (inputs.PowerupAction.WasPressed || bowser->AttackQuery) && bowser->AttackCooldown == 0;
                Sprint = inputs.PowerupAction.IsDown ;
                if (Sprint && bowser->AttackCooldown > 0) {
                    bowser->AttackQuery = inputs.PowerupAction.IsDown;
                } else {
                    bowser->AttackQuery = false;
                }
                if (inputs.Left.IsDown || inputs.Right.IsDown) {
                    leftrightinput = (inputs.Left.IsDown == inputs.Right.IsDown) ? -(physicsObject->Velocity.X * FP._0_10) : (inputs.Left.IsDown ? -1 : 1);
                    HasTarget = true;
                }
                if (inputs.Up.IsDown || inputs.Down.IsDown) {
                    updowninput = (inputs.Up.IsDown == inputs.Down.IsDown) ? 0 : (inputs.Down.IsDown ? -1 : 1);
                }
                mario->FacingRight = boss->FacingRight;
            } else {
                Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var TargetEntity, out var distance);

                Sprint = bowser->waitTime > 90;
                if (Sprint)
                    Fireball = true;
                if ((bowser->waitTime > 90 && bowser->State == BowserState.Attacking) || bowser->waitTime <= 90)
                    QuantumUtils.Decrement(ref bowser->waitTime);

                if (bowser->JumpFromAttackCounter > 2) {
                    Jump = true;
                    bowser->JumpFromAttackCounter = 0;
                }

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
                    FP absDif = FPMath.Abs(ourPos.X - theirPos.X);

                    if (absDif < 2) {
                        bowser->waitTime = 30;
                        leftrightinput = damageDirection.X > 0 ? -1 : 1;
                        Jump |= absDif < 1;
                    } else if ((absDif > 6 || boss->FacingRight != damageDirection.X > 0) && physicsObject->IsTouchingGround) {
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                    } else if (absDif > 8) {
                        Jump = true;
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                    }
                    if (absDif <= 9 && absDif >= 2) {
                        updowninput = FPMath.RoundToInt(((ourPos.Y - theirPos.Y) + FP._0_50) * -FP._0_33);
                        if (bowser->waitTime <= 0) {
                            Fireball = true;
                            if (bowser->BigAttackCounter > 0) {
                                bowser->BigAttackCounter--;
                                bowser->waitTime = (byte) (60 + FPMath.RoundToInt(f.RNG->Next() * 30));
                            } else {
                                bowser->BigAttackCounter = (byte) (2 + FPMath.RoundToInt(f.RNG->Next() * 2));
                                bowser->waitTime = 150;
                            }
                        }
                    }
                }
                if (leftrightinput != 0 && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)) {
                    Jump = true;
		}
            }

            QuantumUtils.Decrement(ref bowser->VolleyCooldown);
            QuantumUtils.Decrement(ref bowser->AttackCooldown);

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.BowserFall(entity);
                boss->BossHarmed(f, entity, boss->FacingRight, KnockbackStrength.FireballBump, false);
                physicsObject->Velocity.Y = 20;
                bowser->ReusableTimer = 0;
                bowser->State = BowserState.Jumping;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.Y = 16;
                physicsObject->Velocity.X = 0;
                physicsObject->TerminalVelocity = -20;
            }

            void TryGroundpound() {
                if (Groundpounding && !physicsObject->IsTouchingGround) {
                    bowser->State = BowserState.Groundpound;
                    bowser->ReusableTimer = 0;
                }
            }
            void TryFireball(EntityRef Entity) {
                if (Fireball) {
                    bowser->State = physicsObject->IsTouchingGround ? BowserState.Attacking : BowserState.AttackingInJump;
                    bowser->ReusableTimer = 0;
                    f.Events.BowserAttack(Entity, BowserAttackType.FireBall);
                }
            }
            //State Calcs
            switch (bowser->State) {
            case BowserState.Roaring:
                physicsObject->Velocity.X *= Constants._0_95;
                if (physicsObject->IsTouchingGround && bowser->ReusableTimer <= 0) {
                    f.Events.BowserLanded(f, filter.Entity, true);
                    bowser->ReusableTimer++;
                } else if (bowser->ReusableTimer > 0) {
                    bowser->ReusableTimer++;
                    if (bowser->ReusableTimer > 100 || boss->iframes != 0) {
                        bowser->ReusableTimer = 0;
                        bowser->BigAttackCounter = 3;
                        bowser->waitTime = 30;
                        bowser->State = BowserState.Walking;
                    }
                }
                break;
            case BowserState.Walking:
                if (leftrightinput != 0) {
                    boss->FacingRight = leftrightinput > 0;
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 2 + FP._0_75);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper, clamper);
                } else {
                    physicsObject->Velocity.X *= Constants._0_90;
                }
                if (Jump) {
                    bowser->State = BowserState.ChargeJump;
                    physicsObject->Velocity.X *= FP._0_50;
                    physicsObject->TerminalVelocity = -5;
                    f.Events.BowserJump(f, filter.Entity);
                } else {
                    TryFireball(filter.Entity);
                    TryGroundpound();
                }

                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround)
                    f.Events.BowserLanded(f, filter.Entity, false);
                break;
            case BowserState.ChargeJump:
                bowser->ReusableTimer++;
                if (bowser->ReusableTimer > 16) {
                    bowser->ReusableTimer = 0;
                    bowser->State = BowserState.Jumping;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.Y = 12;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * 3), -7, 7);
                    physicsObject->TerminalVelocity = -20;
                } else {
                    TryFireball(filter.Entity);
                }
                break;
            case BowserState.Jumping:
                if (leftrightinput != 0) {
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_10), -6, 6);
                }
                if (!Jumpheld) {
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, 8);
                }
                TryFireball(filter.Entity);
                TryGroundpound();

                if (physicsObject->IsTouchingGround) {
                    bowser->State = BowserState.Walking;
                    if (!physicsObject->WasTouchingGround)
                     f.Events.BowserLanded(f, filter.Entity, false);
                }
                break;
            case BowserState.Knockbacked:
                physicsObject->Velocity.X *= Constants._0_95;
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_20) {
                    bowser->State = BowserState.Walking;
                    if (boss->iframes > 0)
                        boss->iframes = 30;
                }
                break;
            case BowserState.AttackingInJump:
            case BowserState.Attacking:
                    FP clamper2 = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_10, bowser->State == BowserState.AttackingInJump ? 2 + FP._0_75 : FP._1_25);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper2, clamper2);

                bowser->State = physicsObject->IsTouchingGround ? BowserState.Attacking : BowserState.AttackingInJump;

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
                            bowser->State = bowser->State == BowserState.AttackingInJump ? BowserState.Jumping : BowserState.Walking;
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
                        bowser->State = bowser->State == BowserState.AttackingInJump ? BowserState.Jumping : BowserState.Walking;
                    } else if (leftrightinput != 0) {
                        //Allow Turnaround In Startup Phase
                        boss->FacingRight = leftrightinput > 0;
                    }

                    void CreateProjectile(AssetRef<EntityPrototype> prototype, FPVector2 Direction, FP VerticalBonus) {
                        FPVector2 spawnPos = transform->Position + new FPVector2(boss->FacingRight ? FP._0_50 : -FP._0_50, Constants._0_66);
                        EntityRef newEntity = f.Create(prototype);
                        var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
                        projectile->Initialize(f, newEntity, boss->BossGetOwnerResponsible(entity), spawnPos, boss->FacingRight, false);
                        var projPhys = f.Unsafe.GetPointer<PhysicsObject>(newEntity);
                        FP radian = FPMath.Atan2(Direction.Y, Direction.X);
                        Direction = new FPVector2(FPMath.Cos(radian), FPMath.Sin(radian));
                        projPhys->Velocity = (Direction * projectile->Speed) + (FPVector2.Up * VerticalBonus);
                        projectile->Speed = projPhys->Velocity.X;
                    }
                    break;
                case BowserState.Groundpound:
                    if (bowser->ReusableTimer <= 15) {
                        bowser->ReusableTimer++;
                        FP Cap = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_20, 0);
                        physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_20), -Cap, Cap);
                        physicsObject->Velocity.Y = 1;
                    } else {
                        physicsObject->Velocity.X = 0;
                        physicsObject->Velocity.Y = -30;
                        if (physicsObject->IsTouchingGround) {
                            bowser->ReusableTimer++;
                            if (bowser->ReusableTimer > 45) {
                                bowser->ReusableTimer = 0;
                                bowser->State = BowserState.Walking;
                            } else if (bowser->ReusableTimer == 17) {
                                f.Events.BowserLanded(f, filter.Entity, false);
                            }
                        }
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
        public void OnMarioBowserInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var bowser = f.Unsafe.GetPointer<Bowser>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            switch (boss->BossMarioContact(f, thisEntity, marioEntity, damageDirection, bowser->State == BowserState.Groundpound && bowser->ReusableTimer == 16)) {
            case bossMarioContactResult.Above:
                bowser->State = BowserState.Jumping;
                break;
            case bossMarioContactResult.Harm:
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump, false);
                break;
            case bossMarioContactResult.SuperHarm:
                boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.Normal, false);
                bowser->State = BowserState.Knockbacked;
                bowser->ReusableTimer = 0;
                boss->FacingRight = damageDirection.X > 0;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.X = damageDirection.X > 0 ? -7 : 7;
                f.Events.BowserKnockbacked(thisEntity);
                break;
            case bossMarioContactResult.Bump:
                boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
                break;
            case bossMarioContactResult.Special:
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 3, KnockbackStrength.Groundpound, boss->BossGetOwnerResponsible(thisEntity));
                break;
            }
        }
        public void OnProjectileBowserInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->Dead)
                return;
            var bowser = f.Unsafe.GetPointer<Bowser>(thisEntity);
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == thisEntity || projectile->Owner == boss->ControllerPlayer) {
                return; //hang on, this is OUR projectile!
            }
            var projectileAsset = f.FindAsset(projectile->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                boss->BossHarmed(f, thisEntity, projectile->FacingRight, KnockbackStrength.FireballBump, false);
                bowser->JumpFromAttackCounter++;
                break;
            }
            case ProjectileEffectType.Freeze: {
                bowser->ReusableTimer = 0;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity = FPVector2.Zero;
                IceBlockSystem.Freeze(f, thisEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
        }
        public void OnBossBowserInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->Dead || otherboss->Dead)
                return;
            f.Signals.BossToBossInteraction(thisEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, thisEntity);
        }
        public void OnEnemyBowserInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Bowser* bowser)) {
                return;
            }

            bowser->State = BowserState.Roaring;
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

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Interactable* inter)) {
                inter->ColliderDisabled = false;
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Bowser* bowser)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            //relocate
            //boss->ControllerPlayer
            UnityEngine.Debug.Log(index);
            bowser->IsDry = hazardata.SpecialValues[0].BaseValue == 1;
        }
        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(thisEntity, out Bowser* bowser)) {
                return;
            }

            var otherboss = f.Unsafe.GetPointer<Boss>(otherEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var thisPhys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            if (damageDirection.Y < 0) {
                bowser->State = BowserState.Jumping;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.Y = 6;
                otherboss->BossHarmed(f, otherEntity, damageDirection.X < 0, (bowser->State == BowserState.Groundpound && bowser->ReusableTimer == 16) ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, true);
            } else {
                boss->BossBump(f, thisEntity, damageDirection.X < 0, KnockbackStrength.FireballBump);
            }
        }
        #endregion
    }
}
