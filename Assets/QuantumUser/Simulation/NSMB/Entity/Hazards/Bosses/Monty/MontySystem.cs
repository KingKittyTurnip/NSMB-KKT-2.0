using Photon.Deterministic;
using Quantum.Collections;
using System.Drawing;
using UnityEngine;
using UnityEngine.WSA;
using static LoopingMusicData.e;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class MontyTankSystem : SystemMainThreadEntityFilter<Tank, MontyTankSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnBobombExplodeEntity {

        private static FP MontyBottomSeatPos = FP._0_25;
        public struct Filter {
            public EntityRef Entity;
            public Tank* Tank;
            public Boss* Boss;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);

            f.Context.Interactions.Register<MarioPlayer, Tank>(f, OnMarioTankInteraction);
            f.Context.Interactions.Register<MarioPlayer, Monty>(f, OnMarioMontyInteraction);
            f.Context.Interactions.Register<Projectile, Monty>(f, OnProjectileMontyInteraction);
            f.Context.Interactions.Register<Boss, Monty>(f, OnBossMontyInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var boss = filter.Boss;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.Collider;

            var tank = filter.Tank;

            if (boss->Dead) {
                physicsObject->IsFrozen = true;
                if (hazard->LifeTime == 2) {
                    //make sure we remove these
                    f.Destroy(tank->SegmentEntity);
                    f.Destroy(tank->MoleEntity);
                }
                return;
            }

            if (!f.Exists(tank->SegmentEntity) || !f.Exists(tank->MoleEntity))
                return;

            //Decide Action
            int leftrightinput = 0;
            int updowninput = 0;
            bool Jump = false;
            bool Jumpheld = true;
            bool BombAttack = false;
            //bool Sprint = false;
            bool HasTarget = boss->BossHandleIframes(f);
            if (boss->ControllerPlayer != EntityRef.None) {
                //Controlled By Player
                var mario = f.Unsafe.GetPointer<MarioPlayer>(boss->ControllerPlayer);
                Input inputs = mario->GetPlayerInput(f, boss->ControllerPlayer);
                f.Unsafe.GetPointer<Transform2D>(boss->ControllerPlayer)->Position = transform->Position;

                if (inputs.Left.IsDown || inputs.Right.IsDown) {
                    leftrightinput = (inputs.Left.IsDown == inputs.Right.IsDown) ? 0 : (inputs.Left.IsDown ? -1 : 1);
                }
                if (inputs.Up.IsDown || inputs.Down.IsDown) {
                    updowninput = (inputs.Up.IsDown == inputs.Down.IsDown) ? 0 : (inputs.Down.IsDown ? -1 : 1);
                }

                Jump = inputs.Jump.WasPressed;
                Jumpheld = inputs.Jump.IsDown;

                BombAttack = inputs.PowerupAction.WasPressed;

                mario->FacingRight = boss->FacingRight;
            } else {
                Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var TargetEntity, out var distance);

                QuantumUtils.Decrement(ref tank->waitTime);
                int decideSegmentTimerFrames = 90;

                FPVector2 checkPosition = transform->Position + (FPVector2.Right * FP._0_20 * (boss->FacingRight ? 1 : -1));
                if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, 5, out var hit) || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    Jump = true;
                }
                leftrightinput = boss->FacingRight ? 1 : -1;

                //control segments
                if (physicsObject->IsTouchingCeiling) {
                    tank->decideSegmentTimer = RanomDecideSegment();
                }

                //Boss Ai
                if (distance > 10) {
                    //wander, minimize segments
                    tank->decideSegmentTimer = -decideSegmentTimerFrames;
                } else {
                    HasTarget = true;
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(TargetEntity);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(TargetEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(TargetEntity);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                    FP absDif = FPMath.Abs(ourPos.X - theirPos.X);

                    if (absDif < 1) {
                        //run from mario
                        //leftrightinput = damageDirection.X > 0 ? -1 : 1;
                    } else if (absDif > 6) {
                        //turn around he too far
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                    } else if (absDif > 8) {
                        //turn around he too far
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                        tank->decideSegmentTimer = -decideSegmentTimerFrames;
                    } else if (tank->SegmentNumber <= (boss->Health <= Constants.GeneralBossHealth/2 ? 2 : 1)) {
                        //somewhat close, increase it by 1 at least, or two if low
                        tank->decideSegmentTimer = decideSegmentTimerFrames;
                    }
                    if (tank->waitTime <= 0) {
                        //try attack
                        if (!tank->MontyOut) {
                            tank->decideSegmentTimer = decideSegmentTimerFrames;
                        } else if (f.Exists(tank->Bombud)) {
                            //check if it's near a player

                            //failed to get player near it, try again after  roughly 0.25 - 0.75 seconds
                            tank->waitTime = (byte) (FPMath.RoundToInt(f.RNG->Next(FP._0_25, FP._0_75) * 60));
                            //tank->decideSegmentTimer = RanomDecideSegment();
                        } else {
                            BombAttack = true;
                            tank->waitTime = (byte) (60 + FPMath.RoundToInt(f.RNG->Next() * 30));
                            //tank->decideSegmentTimer = RanomDecideSegment();
                        }
                    }
                }

                int RanomDecideSegment() {
                    FP rng = f.RNG->Next();
                    if (rng < FP._0_20) { 
                        return decideSegmentTimerFrames * (rng >= FP._0_10 ? -1 : 1); 
                    }
                    return tank->decideSegmentTimer;
                }

                updowninput = tank->decideSegmentTimer == 0 ? 0 : tank->decideSegmentTimer > 0 ? 1 : -1;
                tank->decideSegmentTimer -= updowninput;
            }

            if (transform->Position.Y < stage.StageWorldMin.Y) {
                f.Events.BowserFall(entity);
                boss->BossHarmed(f, entity, boss->FacingRight, KnockbackStrength.FireballBump, false);
                physicsObject->Velocity.Y = 20;
                tank->State = MontyState.Jumping;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.Y = 16;
                physicsObject->Velocity.X = 0;
                physicsObject->TerminalVelocity = -10;
            }

            if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                //ground puff
            }

            void CannonBehavior(int addingDirection, bool NoAction = false) {
                var segmentCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(tank->SegmentEntity);
                var segmentTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
                FP tanksegmentMaxRot = 90;
                FP tankSegmentRotSpeed = 4;
                FP baseVerticalOffset = Constants._0_48;
                FP bulletCreationOffset = FP._0_25;
                byte ActionCooldown = 45;
                int addSegmentThreshold = 30;
                int maxSegments = 2;
                bool isAddingSegment = addingDirection == 1;

                segmentTransform->Position = transform->Position; //TODO: add the velocity to it?

                if (addingDirection == 0 || tank->CannonActionCooldown != 1 || tank->SegmentCreationTimer == 255 || (!isAddingSegment && tank->SegmentNumber == 0 && !tank->MontyOut) || (isAddingSegment && tank->SegmentNumber >= maxSegments)) {
                    if (NoAction) {
                        return;
                    }

                    //we are not adding cannon, handle shoot behavior
                    if (QuantumUtils.Decrement(ref tank->CannonActionCooldown)) {
                        tank->Decision = CannonDecision.TryAction;
                        tank->SegmentCreationTimer = 0;
                    }

                    if (tank->CannonActionCooldown == 0) {
                        tank->ActionId++;
                        if (tank->ActionId > tank->SegmentNumber) {
                            tank->CannonActionCooldown = ActionCooldown;
                            tank->ActionId = 255;
                            tank->Decision = CannonDecision.None;
                        } else {
                            tank->CannonActionCooldown = ActionCooldown;
                        }
                    }

                    tank->SegmentCreationTimer = 0;
                    DoDecision:
                    switch (tank->Decision) {
                    case CannonDecision.None: {
                        break;
                    }
                    case CannonDecision.TryAction: {
                        //decide action
                        bool right = tank->FacingDirection[tank->ActionId] > 0;
                        FP rng = f.RNG->Next();


                        if (rng < ((right != boss->FacingRight) ^ (tank->ActionId == 1) ? FP._0_50 : FP._0_25)) {
                            //prefer turning in the direction we are moving in, unless we are the middle cannon
                            tank->Decision = right ? CannonDecision.TurnLeft : CannonDecision.TurnRight;
                        } else {
                            tank->Decision = CannonDecision.Fire;
                        }
                        goto DoDecision;
                    }
                    case CannonDecision.Fire: {
                        byte CannonCreateBulletFrame = 30;
                        if (tank->CannonActionCooldown == ActionCooldown) {
                            //play anim
                        } else if (tank->CannonActionCooldown == CannonCreateBulletFrame) {
                            bool isRight = tank->FacingDirection[tank->ActionId] > 0;
                            segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);
                            FP Leftward = (shapes[tank->ActionId].Centroid.X > 0 ? -1 : 1);
                            var spawnpoint = new FPVector2((Leftward * FP._0_25) + shapes[tank->ActionId].Centroid.X + (shapes[tank->ActionId].Box.Extents.X * -Leftward), (tank->HeightScale * (tank->ActionId * 2)) + baseVerticalOffset);
                            FireBullet(spawnpoint, isRight);
                        }
                        break;
                    }
                    case CannonDecision.TurnLeft:
                    case CannonDecision.TurnRight: {
                        //turn the cannon all the way within 60 frames
                        tank->FacingDirection[tank->ActionId] = FPMath.Clamp(tank->FacingDirection[tank->ActionId] + (tank->Decision == CannonDecision.TurnRight ? tankSegmentRotSpeed : -tankSegmentRotSpeed), -tanksegmentMaxRot, tanksegmentMaxRot);
                        segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);
                        shapes[tank->ActionId].Centroid.X = (tank->FacingDirection[tank->ActionId] / tanksegmentMaxRot) * tank->Offset[tank->ActionId];
                        break;
                    }
                    }
                } else if (tank->SegmentNumber == 0 && ((addingDirection == 1 && !tank->MontyOut) || (addingDirection == -1 && tank->MontyOut))) {
                    //handle monty mole before adding segments
                    tank->SegmentCreationTimer++;
                    if (tank->SegmentCreationTimer == addSegmentThreshold) {
                        tank->SegmentCreationTimer = 255;
                        tank->MontyOut = addingDirection == 1;
                        tank->AttackCooldown = 60;
                    }
                } else {
                    //add/subtractsegment
                    tank->SegmentCreationTimer++;
                    if (tank->SegmentCreationTimer == addSegmentThreshold) {
                        tank->SegmentCreationTimer = 255;
                        segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);

                        if (isAddingSegment) {
                            //add hitbox
                            tank->SegmentNumber++;
                            tank->FacingDirection[tank->SegmentNumber] = tanksegmentMaxRot * (boss->FacingRight ? 1 : -1);
                            shapes[tank->SegmentNumber].Centroid = new FPVector2(tank->Offset[tank->SegmentNumber] * (boss->FacingRight ? 1 : -1), tank->HeightScale * (tank->SegmentNumber * 2) + baseVerticalOffset);
                            SetCollider();
                        } else {
                            //remove hitbox
                            FP arbitraryPitHitboxNumber = -578;
                            shapes[tank->SegmentNumber].Centroid.Y = arbitraryPitHitboxNumber;
                            tank->SegmentNumber--;
                            SetCollider();
                        }
                    }
                    void SetCollider() {
                        var newY = Constants._0_44 * (tank->SegmentNumber+1);
                        collider->Shape.Box.Extents = new FPVector2(collider->Shape.Box.Extents.X, newY);
                        collider->Shape.Centroid.Y = newY;
                    }
                }

                void FireBullet(FPVector2 spawnpoint, bool right) {
                    EntityRef newBillEntity = f.Create(tank->BulletBillPrototype);
                    var newBill = f.Unsafe.GetPointer<BulletBill>(newBillEntity);
                    newBill->Speed += FPMath.Abs(physicsObject->Velocity.X);
                    var newBillTransform = f.Unsafe.GetPointer<Transform2D>(newBillEntity);
                    newBill->Initialize(f, newBillEntity, entity, right);
                    newBillTransform->Position = transform->Position + spawnpoint;
                }
            }

            void HandleMonty(bool CanAttack) {
                var montyTransform = f.Unsafe.GetPointer<Transform2D>(tank->MoleEntity);
                FP MontySpeed = FP._0_05;
                byte AttackCooldownFrames = 60;
                FP TargetSeat = tank->MontyOut ? ((Constants._0_40 * tank->SegmentNumber) + Constants._0_44) * 2 : MontyBottomSeatPos;

                //place
                tank->LastMontyPos += FPMath.Clamp(TargetSeat - tank->LastMontyPos, -MontySpeed, MontySpeed);
                montyTransform->Position = transform->Position + new FPVector2(0, tank->LastMontyPos);

                //attack
                if (tank->MontyOut && CanAttack) {
                    QuantumUtils.Decrement(ref tank->AttackCooldown);
                    if (BombAttack) {
                        if (f.Exists(tank->Bombud)) {
                            //do bombud rules
                        } else if (tank->AttackCooldown == 0) {
                            tank->AttackCooldown = AttackCooldownFrames;

                            tank->Bombud = f.Create(tank->BombudPrototype);
                            var newBomb = f.Unsafe.GetPointer<Bobomb>(tank->Bombud);
                            var newBombTransform = f.Unsafe.GetPointer<Transform2D>(tank->Bombud);
                            newBomb->Initialize(f, tank->Bombud, entity, boss->FacingRight, true);
                            newBombTransform->Position = transform->Position + new FPVector2(boss->FacingRight ? FP._0_25 : -FP._0_25, tank->LastMontyPos);
                        }
                    }
                }
            }

            void ControlMovement(int direction) {
                if (direction != 0) {
                    if ((physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) && physicsObject->PreviousFrameVelocity.X != 0) {
                        //maintain speed
                        physicsObject->Velocity.X = physicsObject->PreviousFrameVelocity.X;
                    }

                    boss->FacingRight = direction > 0;
                    FP accelerate = FP._0_10;
                    FP decelerate = FP._0_05;
                    FP BaseSpeed = FP._1_50 + ((2-tank->SegmentNumber) * FP._0_50);
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - decelerate, BaseSpeed);

                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (direction * accelerate), -clamper, clamper);
                    boss->FacingRight = physicsObject->Velocity.X > 0;

                } else {
                    physicsObject->Velocity.X *= Constants._0_95;
                }
            }

            //tank
            switch (tank->State) {
            case MontyState.ReadyUp:
                CannonBehavior(0, true);
                HandleMonty(false);
                physicsObject->Velocity.X *= Constants._0_95;
                if (physicsObject->IsTouchingGround || tank->ReusableTimer > 0) {
                    tank->ReusableTimer++;
                    if (tank->ReusableTimer > 100 || boss->iframes != 0) {
                        tank->ReusableTimer = 0;
                        tank->State = MontyState.Walking;
                    }
                }
                break;
            case MontyState.Walking:
                ControlMovement(leftrightinput);
                CannonBehavior(updowninput);
                HandleMonty(true);

                if (Jump) {
                    tank->State = MontyState.ChargeJump;
                    physicsObject->TerminalVelocity = -5;
                    f.Events.BowserJump(f, filter.Entity);
                }
                break;
            case MontyState.ChargeJump:
                FP chargeJumpThreshold = 16;

                ControlMovement(leftrightinput);
                CannonBehavior(updowninput);
                HandleMonty(true);

                tank->ReusableTimer++;
                if (tank->ReusableTimer > chargeJumpThreshold) {
                    tank->ReusableTimer = 0;
                    tank->State = MontyState.Jumping;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity.Y = 6;
                    physicsObject->TerminalVelocity = -10;
                }
                break;
            case MontyState.Jumping:
                ControlMovement(leftrightinput);
                CannonBehavior(updowninput);
                HandleMonty(true);

                if (!Jumpheld) {
                    physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, 5);
                }

                if (physicsObject->IsTouchingGround) {
                    tank->State = MontyState.Walking;
                }
                break;
            case MontyState.Knockbacked:
                CannonBehavior(0, true);
                HandleMonty(false);
                physicsObject->Velocity.X *= Constants._0_95;
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_20) {
                    tank->State = MontyState.Walking;
                    if (boss->iframes > 0)
                        boss->iframes = 30;
                }
                break;
            }
            BrickInteraction(f, ref filter);

            //set movingplatforms, do we actually want to do this?
            //var segmentPlatform = f.Unsafe.GetPointer<MovingPlatform>(tank->SegmentEntity);
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
        public void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContact) {
            //don't interact with our entities
            if (contact.Entity != EntityRef.None
                && f.Unsafe.TryGetPointer(entity, out Tank* tank2)
                && (contact.Entity == tank2->SegmentEntity || contact.Entity == tank2->MoleEntity)) {

                keepContact = false;
            }
        }

        public void OnMarioTankInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var tank = f.Unsafe.GetPointer<Tank>(thisEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (!physicsObject->IsTouchingGround && damageDirection.Y < -FP._0_10 && !mario->IsInKnockback) {
                //stomp mario, we don't want to crush them!
                tank->State = MontyState.Jumping;
                mario->DoKnockback(f, marioEntity, damageDirection.X < 0, 1, KnockbackStrength.Normal, boss->BossGetOwnerResponsible(thisEntity));
            } else {
                //push mario away
                marioPhysicsObject->Velocity.X = (damageDirection.X > 0 ? 1 : -1) * 3;
                f.Events.MarioPlayerBlueShellStomped(marioEntity);
            }
        }
        public void OnMarioMontyInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var monty = f.Unsafe.GetPointer<Monty>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(monty->OwnerEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var tank = f.Unsafe.GetPointer<Tank>(monty->OwnerEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            //ALWAYS stomp this guy
            boss->BossHarmed(f, monty->OwnerEntity, damageDirection.X < 0, mario->IsGroundpoundActive ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, false);
            mario->DoEntityBounce = mario->CurrentPowerupState == PowerupState.MiniMushroom || !mario->IsGroundpounding;
            mario->IsDrilling = false;
            marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X + ((damageDirection.X > 0 ? 1 : -1) * 3), -5, 5);
        }
        public void OnProjectileMontyInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var monty = f.Unsafe.GetPointer<Monty>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(monty->OwnerEntity);
            if (!boss->BossCanInteract())
                return;
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            if (projectile->Owner == monty->OwnerEntity || projectile->Owner == boss->ControllerPlayer) {
                return; //hang on, this is OUR projectile!
            }
            var projectileAsset = f.FindAsset(projectile->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire:
            case ProjectileEffectType.Freeze: {
                boss->BossHarmed(f, monty->OwnerEntity, projectile->FacingRight, KnockbackStrength.FireballBump, false);
                //create unique particle effect for freeze?
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
        }
        public void OnBossMontyInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var monty = f.Unsafe.GetPointer<Monty>(thisEntity);
            if (monty->OwnerEntity == bossEntity) {
                //wuh- that's us!
                return;
            }
            var boss = f.Unsafe.GetPointer<Boss>(monty->OwnerEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (!boss->BossCanInteract() || !otherboss->BossCanInteract())
                return;
            f.Signals.BossToBossInteraction(monty->OwnerEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, monty->OwnerEntity);
        }
        public void OnEnemyBowserInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteract())
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Tank* tank)) {
                return;
            }

            tank->State = MontyState.ReadyUp;
            f.Destroy(tank->SegmentEntity);
            f.Destroy(tank->MoleEntity);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity, ExplosionType type) {
            if (f.Unsafe.TryGetPointer(entity, out Boss* boss)) {
                boss->BossHarmed(f, entity, boss->FacingRight, KnockbackStrength.Normal, true);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, byte ExtraA, byte ExtraB, byte ExtraC, byte ExtraD) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Tank* tank)) {
                return;
            }

            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            boss->Health = Constants.GeneralBossHealth;

            tank->ActionId = 255;
            tank->LastMontyPos = MontyBottomSeatPos;

            //create parts
            tank->SegmentEntity = f.Create(tank->SegmentPrototype);
            var newSegmentTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
            newSegmentTransform->Position = spawnpoint;

            tank->MoleEntity = f.Create(tank->MolePrototype);
            f.Unsafe.GetPointer<Monty>(tank->MoleEntity)->OwnerEntity = thisEntity;
            var newMoleTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
            newMoleTransform->Position = spawnpoint;
        }
        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if ( !f.Unsafe.TryGetPointer(thisEntity, out Monty* monty)
                || !f.Unsafe.TryGetPointer(monty->OwnerEntity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(monty->OwnerEntity, out Tank* tank)) {
                return;
            }

            var otherboss = f.Unsafe.GetPointer<Boss>(otherEntity);
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            boss->BossHarmed(f, thisEntity, damageDirection.X < 0, KnockbackStrength.Groundpound, true);
        }
        #endregion
    }
}
