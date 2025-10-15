using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Drawing.Drawing2D;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class BowserSystem : SystemMainThreadFilterStage<BowserSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalOnIceBlockBroken {
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
            bool Sprint = false;
            bool HasTarget = !QuantumUtils.Decrement(ref boss->iframes);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                //Groundpounding = inputs.Down.WasPressed;
                Jump = inputs.Jump.WasPressed;
                Fireball = (inputs.FireballPowerupAction.WasPressed || bowser->AttackQuery) && bowser->AttackCooldown <= 1;
                Sprint = inputs.FireballPowerupAction.IsDown ;
                if (Sprint && bowser->AttackCooldown > 0) {
                    bowser->AttackQuery = inputs.FireballPowerupAction.IsDown;
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
            } else {
                //Find Closest Player
                EntityRef TargetEntity = EntityRef.None;
                FP distance = 999;
                var players = f.Filter<MarioPlayer>();

                while (players.NextUnsafe(out EntityRef OtherEntity, out MarioPlayer* mar)) {
                    if (mar->IsDead)
                        continue;
                    //Find Closest Player
                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(OtherEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FP e = FPVector2.Distance(ourPos, theirPos);
                    if (e < distance) {
                        TargetEntity = OtherEntity;
                        distance = e;
                    }
                }

                Sprint = bowser->waitTime > 90;
                if (Sprint)
                    Fireball = true;
                if ((bowser->waitTime > 90 && bowser->State == BowserState.Attacking) || bowser->waitTime <= 90)
                    QuantumUtils.Decrement(ref bowser->waitTime);

                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)
                    Jump = true;
                if (bowser->JumpFromAttackCounter > 2) {
                    Jump = true;
                    bowser->JumpFromAttackCounter = 0;
                }

                //Boss Ai
                if (distance > 10) {
                    //wander
                    FPVector2 checkPosition = transform->Position + filter.Collider->Shape.Centroid + (FPVector2.Right * FP._0_05 * (boss->FacingRight ? 1 : -1));
                    if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, 1, out var hit)) {
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
                        leftrightinput = damageDirection.X > 0 ? -1 : 1;
                        Jump = absDif < 1;
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
                                bowser->waitTime = 200;
                            }
                        }
                    }
                }
            }

            QuantumUtils.Decrement(ref bowser->AttackCooldown);

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.BowserFall(entity);
                boss->BossHarmed(f, entity, KnockbackStrength.FireballBump, false);
                physicsObject->Velocity.Y = 20;
                bowser->ReusableTimer = 0;
                bowser->State = BowserState.Jumping;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.Y = 16;
                physicsObject->Velocity.X = 0;
                physicsObject->TerminalVelocity = -20;
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
                    if (bowser->ReusableTimer > 140) {
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
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 2 + FP._0_50);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper, clamper);
                } else {
                    physicsObject->Velocity.X *= Constants._0_90;
                }
                if (Jump) {
                    bowser->State = BowserState.ChargeJump;
                    physicsObject->Velocity.X *= FP._0_50;
                    physicsObject->TerminalVelocity = -5;
                    f.Events.BowserJump(f, filter.Entity);
                } else if (Fireball) {
                    bowser->State = BowserState.Attacking;
                    f.Events.BowserAttack(filter.Entity, BowserAttackType.FireBall);
                }

                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround)
                    f.Events.BowserLanded(f, filter.Entity, false);
                break;
            case BowserState.ChargeJump:
                bowser->ReusableTimer++;
                if (bowser->ReusableTimer > 25) {
                    bowser->ReusableTimer = 0;
                    bowser->State = BowserState.Jumping;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.Y = 16;
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * 4), -7, 7);
                    physicsObject->TerminalVelocity = -20;
                }
                break;
            case BowserState.Jumping:
                if (leftrightinput != 0) {
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_20), -7, 7);
                }

                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                    bowser->State = BowserState.Walking;
                    f.Events.BowserLanded(f, filter.Entity, false);
                }
                break;
            case BowserState.Knockbacked:
                physicsObject->Velocity.X *= Constants._0_95;
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_10) {
                    bowser->State = BowserState.Walking;
                    if (boss->iframes > 0)
                        boss->iframes = 30;
                }
                break;
            case BowserState.Attacking:
                if (leftrightinput != 0) {
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, FP._1_50);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper, clamper);
                }
                bowser->ReusableTimer++;
                if (bowser->ReusableTimer > 10) {
                    if ((Sprint || bowser->ReusableTimer > 21)/* && !bowser->JumpFire*/) {
                        if (bowser->ReusableTimer == 22 && !bowser->IsDry)
                            f.Events.BowserAttack(filter.Entity, BowserAttackType.MegaAttack);
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
                            bowser->AttackCooldown = 70;
                        }
                    } else if (bowser->ReusableTimer <= 21) {
                        //create one
                        f.Events.BowserShoot(filter.Entity, false);

                        CreateProjectile(bowser->IsDry ? bowser->BlueFire : bowser->Fireball, new FPVector2(1, updowninput / 3), 0);

                        bowser->AttackCooldown = 25;
                        bowser->ReusableTimer = 0;
                        bowser->State = BowserState.Walking;
                    }
                    physicsObject->Velocity.X *= Constants._0_95;

                    void CreateProjectile(AssetRef<EntityPrototype> prototype, FPVector2 Direction, FP VerticalBonus) {
                        FPVector2 spawnPos = transform->Position + new FPVector2(boss->FacingRight ? FP._0_50 : -FP._0_50, Constants._0_66);
                        EntityRef newEntity = f.Create(prototype);
                         var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
                         projectile->Initialize(f, newEntity, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : entity, spawnPos, boss->FacingRight);
                         var projPhys = f.Unsafe.GetPointer<PhysicsObject>(newEntity);
                        FP radian = FPMath.Atan2(Direction.Y, Direction.X);
                        Direction = new FPVector2(FPMath.Cos(radian), FPMath.Sin(radian));
                         projPhys->Velocity = (Direction * projectile->Speed) + (FPVector2.Up * VerticalBonus);
                         projectile->Speed = projPhys->Velocity.X;
                    }
                } else {
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_25, 2);
                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_50), -clamper, clamper);
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
            var bowser = f.Unsafe.GetPointer<Bowser>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->iframes > 0 || boss->Dead)
                return;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;

            bool bossHarmed = false;
            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                boss->BossHarmed(f, thisEntity, KnockbackStrength.Normal, true);
                bossHarmed = true;

            } else if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        boss->BossHarmed(f, thisEntity, KnockbackStrength.FireballBump, false);
                        bossHarmed = true;
                    }
                    mario->DoEntityBounce = true;
                } else {
                    boss->BossHarmed(f, thisEntity, KnockbackStrength.FireballBump, false);
                    bossHarmed = true;
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsDamageable) {
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 1, KnockbackStrength.Normal, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : thisEntity);
                
            }

            if (bossHarmed) {
                if (groundpounded) {
                    f.Events.BowserKnockbacked(thisEntity);
                    bowser->State = BowserState.Knockbacked;
                    bowser->ReusableTimer = 0;
                    boss->FacingRight = damageDirection.X > 0;
                    f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.X = damageDirection.X > 0 ? -7 : 7;
                } else {
                    f.Events.PlayBossHitSound(thisEntity);
                }
            }
        }
        public void OnProjectileBowserInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->iframes > 0 || boss->Dead)
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
                boss->BossHarmed(f, thisEntity, KnockbackStrength.FireballBump, false);
                f.Events.PlayBossHitSound(thisEntity);
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

            f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
        }
        public void OnBossBowserInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->iframes > 0 || boss->Dead || otherboss->iframes > 0 || otherboss->Dead)
                return;
            var bowser = f.Unsafe.GetPointer<Bowser>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(bossEntity);
            var thisPhys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(bossEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            f.Events.BowserKnockbacked(thisEntity);
            bowser->State = BowserState.Knockbacked;
            bowser->ReusableTimer = 0;
            boss->FacingRight = damageDirection.X > 0;
            thisPhys->Velocity.X = damageDirection.X > 0 ? -7 : 7;

            //use signals for boss interactions instead
        }
        public void OnEnemyBowserInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->iframes > 0 || boss->Dead)
                return;

            if (f.Unsafe.TryGetPointer(enemyEntity, out Goomba* goomba)) {
                goomba->Kill(f, enemyEntity, thisEntity, KillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Koopa* koopa)) {
                if (koopa->IsKicked) {
                    boss->BossHarmed(f, thisEntity, KnockbackStrength.FireballBump, false);
                    f.Events.PlayBossHitSound(thisEntity);
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Bowser* bowser)) {
                return;
            }

            bowser->State = BowserState.Roaring;
            hazard->LifeTime = 130;

            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                mario->RelieveFromBoss(f, boss->ControllerPlayer);
                mario->DoKnockback(f, boss->ControllerPlayer, !boss->FacingRight, 2, KnockbackStrength.Groundpound, EntityRef.None, true);
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Bowser* bowser)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            //relocate
            //boss->ControllerPlayer
            bowser->IsDry = hazardata.SpecialValues[0].BaseValue == 1;
        }
        #endregion
    }
}
