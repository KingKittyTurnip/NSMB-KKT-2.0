using Photon.Deterministic;
using Quantum.Collections;
using static IInteractableTile;

namespace Quantum {
    
    public unsafe class PeteySystem : SystemMainThreadFilterStage<PeteySystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnIceBlockBroken {
        public struct Filter {
            public EntityRef Entity;
            public Petey* Petey;
            public Boss* Boss;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Petey>(f, OnMarioPeteyInteraction);
            f.Context.Interactions.Register<Projectile, Petey>(f, OnProjectilePeteyInteraction);
            f.Context.Interactions.Register<Boss, Petey>(f, OnBossPeteyInteraction);
            f.Context.Interactions.Register<Enemy, Petey>(f, OnEnemyPeteyInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var petey = filter.Petey;
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
            bool Groundpounding = false;
            bool HasTarget = !QuantumUtils.Decrement(ref boss->iframes);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                Groundpounding = inputs.Down.WasPressed;
                petey->Flying = inputs.Jump.IsDown;
                if (inputs.Left.IsDown || inputs.Right.IsDown) {
                    leftrightinput = (inputs.Left.IsDown == inputs.Right.IsDown) ? -(physicsObject->Velocity.X * FP._0_10) : (inputs.Left.IsDown ? -1 : 1);
                    HasTarget = true;
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

                //Boss Ai
                if (distance > 10) {
                    //wander
                    Groundpounding = false;
                    petey->Flying = false;
                    petey->JumpCounter = 0;
                    if ((physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) && physicsObject->IsTouchingGround) {
                        boss->FacingRight = physicsObject->IsTouchingLeftWall;
                    }
                } else {
                    HasTarget = true;
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(TargetEntity);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(TargetEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(TargetEntity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                    if ((FPMath.Abs(ourPos.X - theirPos.X) > 3 || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) && petey->State < PeteyState.Diving) {
                        boss->FacingRight = damageDirection.X > 0;
                    }
                    if (petey->Flying) {
                        if (FPMath.Abs(ourPos.X - theirPos.X) < 1 && (ourPos.Y - theirPos.Y) > 2) {
                            Groundpounding = true;
                            petey->JumpCounter = 0;
                        }
                    } else if (petey->JumpCounter >= 4 && physicsObject->Velocity.Y < 0) {
                        petey->Flying = true;
                    } else if (physicsObject->IsTouchingGround && petey->State == PeteyState.Jumping) {
                        petey->JumpCounter++;
                    }
                }
                leftrightinput = boss->FacingRight ? 1 : -1;
            }

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.PeteyGetUp(filter.Entity);
                physicsObject->Velocity.Y = 12;
                petey->PreviousLandLevel = stage.StageWorldMin.Y + 7;
                petey->State = PeteyState.Flying;
                petey->Flying = true;
                physicsObject->BreakMegaObjects = false;
                petey->JumpCounter = 0;
                petey->HitATarget = false;
            }

            //State Calcs
            switch (petey->State) {
            case PeteyState.Idling:
                physicsObject->Velocity.X *= Constants._0_95;
                if ((HasTarget && physicsObject->IsTouchingGround) || petey->ReusableTimer != 0) {
                    if (petey->ReusableTimer == 0)
                        f.Events.PeteyWakeup(filter.Entity, false);
                    petey->ReusableTimer++;
                    if (petey->ReusableTimer > 180 || boss->Health != Constants.GeneralBossHealth) {
                        collider->Shape.Centroid.X = 0;
                        collider->Shape.Centroid.Y = petey->Hitbox.Y;
                        collider->Shape.Box.Extents = petey->Hitbox;
                        petey->ReusableTimer = 0;
                        petey->State = PeteyState.Jumping;
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
                BrickInteraction(f, ref filter);
                break;
            case PeteyState.Flying:
                if (leftrightinput != 0)
                    boss->FacingRight = leftrightinput > 0;
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (leftrightinput * FP._0_10), -3, 3);
                physicsObject->Velocity.Y = FPMath.Clamp(petey->Flying ? physicsObject->Velocity.Y + (physicsObject->Velocity.Y < 0 ? FP._0_50 : FP._0_33) : physicsObject->Velocity.Y-FP._0_33, -6, FPMath.Min((petey->PreviousLandLevel - transform->Position.Y) + physicsObject->Velocity.Y, 5));
                if (Groundpounding) {
                    f.Events.PeteyDive(filter.Entity);
                    petey->State = PeteyState.Diving;
                    petey->HitATarget = false;
                    physicsObject->BreakMegaObjects = true;
		    physicsObject->Velocity.Y = 4;
                    petey->ReusableTimer = 0;
                } else if (physicsObject->IsTouchingGround) {
                    petey->State = PeteyState.Jumping;
                    petey->ReusableTimer = 0;
                    physicsObject->Gravity.Y = -10;
                }
                BrickInteraction(f, ref filter);
                break;
            case PeteyState.Diving:
                if (petey->ReusableTimer++ < 10) {
                    physicsObject->Velocity.X *= FP._0_50;
                    physicsObject->Velocity.Y = FP._0_10;
                } else {
                    if (physicsObject->IsTouchingGround) {
                        f.Events.PeteyLanded(filter.Entity, !petey->HitATarget);
                        physicsObject->Velocity.X = 0;
                        physicsObject->Velocity.Y = 0;
                        petey->ReusableTimer = 0;
                        petey->State = PeteyState.Fallen;
                        collider->Shape.Centroid.Y = petey->FallenBox.Y;
                        collider->Shape.Box.Extents = petey->FallenBox;
                    } else {
                        physicsObject->Velocity.X = boss->FacingRight ? 2 : -2;
                        physicsObject->Velocity.Y = -12;
                    }
                }
                BrickInteraction(f, ref filter);
                break;
            case PeteyState.Fallen:
                physicsObject->Velocity.X *= Constants._0_95;
                petey->ReusableTimer++;
                if (petey->ReusableTimer > (petey->HitATarget ? 0 : 100)) {
                    if (physicsObject->Gravity.Y == 0) {
                        f.Events.PeteyGetUp(filter.Entity);
                        physicsObject->Gravity.Y = -10;
                        physicsObject->BreakMegaObjects = false;
                    }
                }
                if (petey->ReusableTimer > 180 || (petey->HitATarget && petey->ReusableTimer > 30)) {
                    petey->HitATarget = false;
                    collider->Shape.Centroid.Y = petey->Hitbox.Y;
                    collider->Shape.Box.Extents = petey->Hitbox;
                    petey->State = PeteyState.Jumping;
                    petey->ReusableTimer = 0;
                    petey->Flying = false;
                }
                break;
            }

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
        public void OnMarioPeteyInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var petey = f.Unsafe.GetPointer<Petey>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->iframes > 0 || boss->Dead)
                return;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            bool peteyDiving = petey->State == PeteyState.Diving && petey->ReusableTimer >= 28;
            bool attackedFromAbove = !peteyDiving && FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25 && !mario->IsInKnockback;
            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            bool vulnrable = petey->State == PeteyState.Fallen;
            bool peteyHarmed = false;

            if (mario->InstakillsEnemies(marioPhysicsObject, true) || groundpounded) {
                boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, true);
                peteyHarmed = true;
                vulnrable |= groundpounded;

            } else if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump, true);
                        peteyHarmed = true;
                    }
                    mario->DoEntityBounce = true;
                } else {
                    boss->BossHarmed(f, thisEntity, vulnrable ? KnockbackStrength.Normal : KnockbackStrength.FireballBump, true);
                    peteyHarmed = true;
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsDamageable && mario->DoKnockback(f, marioEntity, damageDirection.X < 0, peteyDiving ? 2 : 1, peteyDiving ? KnockbackStrength.Groundpound : KnockbackStrength.CollisionBump, boss->ControllerPlayer != EntityRef.None ? boss->ControllerPlayer : thisEntity)) {
                petey->HitATarget = true;
            }

            if (peteyHarmed) {
                if (vulnrable) {
                    f.Events.PeteyStomped(thisEntity, boss->Health <= 0);
                    petey->ReusableTimer = 140;
                } else {
                    f.Events.PlayBossHitSound(thisEntity);
                }
            }
        }
        public void OnProjectilePeteyInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (boss->iframes > 0 || boss->Dead)
                return;
            var petey = f.Unsafe.GetPointer<Petey>(thisEntity);
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == boss->ControllerPlayer) {
                return; //hang on, this is OUR projectile!
            }
            var projectileAsset = f.FindAsset(projectile->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                boss->BossHarmed(f, thisEntity, KnockbackStrength.FireballBump, false);
                f.Events.PlayBossHitSound(thisEntity);
                break;
            }
            case ProjectileEffectType.Freeze: {
                f.Events.PeteyJump(thisEntity);
                petey->State = PeteyState.Flying;
                petey->Flying = true;
                petey->ReusableTimer = 0;
                petey->HitATarget = false;
                f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity = FPVector2.Zero;
                IceBlockSystem.Freeze(f, thisEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
        }
        public void OnBossPeteyInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->iframes > 0 || boss->Dead || otherboss->iframes > 0 || otherboss->Dead)
                return;
            f.Signals.BossToBossInteraction(thisEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, thisEntity);
        }
        public void OnEnemyPeteyInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Petey* petey)) {
                return;
            }

            petey->State = PeteyState.Fallen;
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Petey* petey)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            //relocate
            //boss->ControllerPlayer
        }

        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(thisEntity, out Petey* petey)) {
                return;
            }

            var otherboss = f.Unsafe.GetPointer<Boss>(otherEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var thisPhys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (petey->State == PeteyState.Diving) {
                petey->HitATarget = true;
                otherboss->BossHarmed(f, otherEntity, KnockbackStrength.Groundpound, true);
                f.Events.PeteyStomped(thisEntity, boss->Health <= 0);
            } else {
                thisPhys->Velocity.X = damageDirection.X > 0 ? -4 : 4;
            }
        }
        #endregion
    }
}
