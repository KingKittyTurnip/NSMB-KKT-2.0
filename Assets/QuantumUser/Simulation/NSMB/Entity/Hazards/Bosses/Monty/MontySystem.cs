using Photon.Deterministic;
using Quantum.Collections;
using static UnityEngine.LowLevelPhysics2D.PhysicsShape;

namespace Quantum {
    
    public unsafe class MontyTankSystem : SystemMainThreadEntityFilter<Tank, MontyTankSystem.Filter>, ISignalInitializeHazard, ISignalBossDeath, ISignalBossToBossInteraction, ISignalOnBobombExplodeEntity, ISignalOnIceBlockBroken {

        public struct Filter {
            public EntityRef Entity;
            public Tank* Tank;
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

            //For EVERY Boss.
            f.Context.Interactions.Register<Enemy, Boss>(f, OnEnemyBossInteraction);
            f.Context.Interactions.Register<IceBlock, Boss>(f, OnIceblockBossInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.Collider;

            var tank = filter.Tank;
            if (!f.Exists(tank->SegmentEntity) || !f.Exists(tank->MoleEntity)) {
                return;
            }

            if (!f.Unsafe.TryGetPointer<Boss>(tank->MoleEntity, out var boss)) {
                return;
            }

            if (boss->Dead) {
                physicsObject->IsFrozen = true;
                return;
            }

            //Decide Action
            int leftrightinput = 0;
            int updowninput = 0;
            bool Jump = false;
            bool Jumpheld = true;
            bool AttackHeld = false;
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

                Jump = mario->JumpBufferFrames > 0;
                Jumpheld = inputs.Jump.IsDown;

                AttackHeld = inputs.PowerupAction.IsDown;

                mario->FacingRight = boss->FacingRight;
            } else {
                Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var TargetEntity, out var distance);

                QuantumUtils.Decrement(ref tank->waitTime);
                int decideSegmentTimerFrames = 1;

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
                        leftrightinput = damageDirection.X > 0 ? -1 : 1;
                    } else if (absDif > 6) {
                        //turn around he too far
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                    } else if (absDif > 8) {
                        //turn around he too far
                        leftrightinput = damageDirection.X > 0 ? 1 : -1;
                    } else if (tank->SegmentNumber < (boss->Health <= Constants.GeneralBossHealth/2 ? 2 : 1)) {
                        //somewhat close, increase it by 1 at least, or two if low
                        tank->decideSegmentTimer = decideSegmentTimerFrames;
                    }
                    if (tank->waitTime <= 0) {
                        //try attack
                        AttackHeld = true;
                        tank->waitTime = (byte) (60 + FPMath.RoundToInt(f.RNG->Next() * 30));
                        //tank->decideSegmentTimer = RanomDecideSegment();
                    }
                    if (tank->waitTime > 63 && (damageDirection.X > 0 == boss->FacingRight)) {
                        AttackHeld = true;
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
                physicsObject->Velocity.Y = 12;
                physicsObject->Velocity.X = 0;
                physicsObject->TerminalVelocity = -10;
            }

            if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                f.Events.BowserLanded(f, entity, tank->State == MontyState.ReadyUp);
            }

            void CannonBehavior(int addingDirection, bool NoAction = false) {
                if (NoAction) {
                    return;
                }
                var segmentCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(tank->SegmentEntity);
                var segmentTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
                FP tanksegmentMaxRot = 90;
                FP tankSegmentRotSpeed = Constants._7_50;
                FP baseVerticalOffset = Constants._0_48;
                FP bulletCreationOffset = FP._0_25;
                int addSegmentThreshold = 45;
                int maxSegments = 2;
                bool isAddingSegment = addingDirection == 1;

                segmentTransform->Position = transform->Position;
                f.Unsafe.GetPointer<MovingPlatform>(tank->SegmentEntity)->Velocity = physicsObject->Velocity;

                //do actions for each cannon
                for (byte i = 0; i < tank->SegmentNumber+1; i++) {
                    DoDecision:
                    switch (tank->CurrentAction[i]) {
                    case CannonDecision.None: { //decide action
                        if (i == 0 && boss->FacingRight != (tank->FacingDirection[i] > 0) && tank->AttackCooldown == 0) {
                            //turn!
                            tank->CurrentAction[i] = (tank->FacingDirection[i] > 0) ? CannonDecision.TurnLeft : CannonDecision.TurnRight;
                            f.Events.TankStartTurn(entity, i, true);
                            goto DoDecision;
                        }
                        break;
                    }
                    case CannonDecision.TurnLeft:
                    case CannonDecision.TurnRight: { //turn logic
                        tank->FacingDirection[i] = /*FPMath.Clamp(*/tank->FacingDirection[i] + (tank->CurrentAction[i] == CannonDecision.TurnRight ? tankSegmentRotSpeed : -tankSegmentRotSpeed)/*, -tanksegmentMaxRot, tanksegmentMaxRot)*/;
                        segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);
                        shapes[i].Centroid.X = (tank->FacingDirection[i] / tanksegmentMaxRot) * tank->CannonOffset[i].X;
                        if (FPMath.Abs(tank->FacingDirection[i]) == 90) {
                            tank->CurrentAction[i] = CannonDecision.None;
                            f.Events.TankStartTurn(entity, i, false);
                        }
                        break;
                    }
                    case CannonDecision.FireFin: { //Shoot
                        segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);
                        bool isRight = tank->FacingDirection[i] > 0;
                        FP Leftward = (shapes[i].Centroid.X > 0 ? -1 : 1);
                        FP BillOffset = FP._0_20;

                        var spawnpoint = new FPVector2((Leftward * FP._0_25) + shapes[i].Centroid.X + (shapes[i].Box.Extents.X * -Leftward), tank->CannonOffset[i].Y - BillOffset);
                        FireBullet(spawnpoint, isRight);
                        f.Events.TankCannonFire(entity, i);
                        if (i == 0) {
                            tank->CurrentAction[i] = CannonDecision.None;
                        } else {
                            tank->CurrentAction[i] = (tank->FacingDirection[i] > 0) ? CannonDecision.TurnDelayLA : CannonDecision.TurnDelayRA;
                            f.Events.TankStartTurn(entity, i, true);
                        }
                        break;
                    }
                    default: { //increment in any other state
                        tank->CurrentAction[i]++;
                        break;
                    }
                    }
                }

                //handle segment control
                if (addingDirection != 0) {
                    if (!((!isAddingSegment && tank->SegmentNumber == 0) || (isAddingSegment && tank->SegmentNumber >= maxSegments))) {
                        //add/subtractsegment
                        tank->SegmentCreationTimer++;
                        if (tank->SegmentCreationTimer == addSegmentThreshold) {
                            tank->SegmentCreationTimer = 0;
                            segmentCollider->Shape.Compound.GetShapes(f, out var shapes, out int shapecount);

                            if (isAddingSegment) {
                                //add segment
                                tank->SegmentNumber++;

                                tank->CurrentAction[tank->SegmentNumber] = CannonDecision.None;
                                tank->FacingDirection[tank->SegmentNumber] = tanksegmentMaxRot * (boss->FacingRight ? 1 : -1);
                                shapes[tank->SegmentNumber].Centroid = new FPVector2(tank->CannonOffset[tank->SegmentNumber].X * (boss->FacingRight ? 1 : -1), tank->CannonOffset[tank->SegmentNumber].Y);

                                f.Events.TankAddRemoveSegment(entity, tank->SegmentNumber, true);
                            } else {
                                //remove segment
                                FP arbitraryPitHitboxNumber = -578;

                                shapes[tank->SegmentNumber].Centroid.Y = arbitraryPitHitboxNumber;
                                tank->CurrentAction[tank->SegmentNumber] = CannonDecision.None;

                                f.Events.TankAddRemoveSegment(entity, tank->SegmentNumber, false);
                                tank->SegmentNumber--;
                            }
                            SetCollider();
                        }
                        void SetCollider() {
                            var newY = tank->CannonOffset[tank->SegmentNumber].Z;
                            collider->Shape.Box.Extents = new FPVector2(collider->Shape.Box.Extents.X, newY);
                            collider->Shape.Centroid.Y = newY;
                        }
                    }
                } else {
                    //decrement it fast
                    if (!QuantumUtils.Decrement(ref tank->SegmentCreationTimer))
                        QuantumUtils.Decrement(ref tank->SegmentCreationTimer);
                }

                void FireBullet(FPVector2 spawnpoint, bool right) {
                    EntityRef newBillEntity = f.Create(tank->BulletBillPrototype);
                    var newBill = f.Unsafe.GetPointer<BulletBill>(newBillEntity);
                    var billHazard = f.Unsafe.GetPointer<Hazard>(newBillEntity);
                    var newBillTransform = f.Unsafe.GetPointer<Transform2D>(newBillEntity);

                    newBill->Initialize(f, newBillEntity, tank->MoleEntity, right);
                    newBillTransform->Position = transform->Position + spawnpoint;
                    newBill->Speed = Constants._3_50 + (FPMath.Abs(physicsObject->Velocity.X)*FP._0_10);
                    billHazard->IsHazard = true;
                    billHazard->LifeTime = 360;//6 seconds
                }
            }

            void HandleMonty(bool CanAttack) {
                var montyTransform = f.Unsafe.GetPointer<Transform2D>(tank->MoleEntity);
                FP MontySpeed = FP._0_05;
                byte AttackStrikeFrame = 60;
                FP TargetSeat = tank->MoleSeats[tank->SegmentNumber];

                //place
                tank->LastMontyPos += FPMath.Clamp(TargetSeat - tank->LastMontyPos, -MontySpeed, MontySpeed);
                montyTransform->Position = transform->Position + new FPVector2(0, tank->LastMontyPos);

                //attack
                if (CanAttack) {
                    if (QuantumUtils.Decrement(ref tank->AttackCooldown) && AttackHeld) {
                        tank->AttackCooldown = 73;
                        f.Events.TankMontyAttack(entity, false);
                    }
                    if (tank->AttackCooldown == AttackStrikeFrame) {
                        tank->CannonActionCooldown = AttackStrikeFrame;
                        if (!AttackHeld) {
                            //make all cannons fire a bill, if possible
                            for (byte i = 0; i < tank->SegmentNumber+1; i++) {
                                if (tank->CurrentAction[i] == CannonDecision.None) {
                                    tank->CurrentAction[i] = (CannonDecision) ((int) CannonDecision.FireA + (i*2));
                                    f.Events.TankCannonPrepareFire(entity, i);
                                }
                            }
                        } else {
                            //make a bobomb
                            f.Events.TankMontyAttack(entity, true);
                            var bombEntity = f.Create(tank->BombudPrototype);
                            var newBomb = f.Unsafe.GetPointer<Bobomb>(bombEntity);
                            var bombHazard = f.Unsafe.GetPointer<Hazard>(bombEntity);
                            var newBombTransform = f.Unsafe.GetPointer<Transform2D>(bombEntity);

                            newBomb->Initialize(f, bombEntity, tank->MoleEntity, boss->FacingRight, true);
                            newBombTransform->Position = transform->Position + new FPVector2(boss->FacingRight ? FP._0_25 : -FP._0_25, tank->LastMontyPos);
                            newBomb->CurrentDetonationFrames = 40;
                            bombHazard->IsHazard = true;
                            bombHazard->LifeTime = 360;//6 seconds
                        }
                        if (physicsObject->Velocity.Y < 1)
                            physicsObject->Velocity.Y = 1;
                    }
                }
            }

            void ControlMovement(int direction) {
                if (direction != 0) {
                    if ((physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) && physicsObject->PreviousFrameVelocity.X != 0) {
                        //maintain speed
                        physicsObject->Velocity.X = physicsObject->PreviousFrameVelocity.X;
                    }

                    if (tank->AttackCooldown <= 60) {
                        boss->FacingRight = direction > 0;
                    }
                    FP accelerate = FP._0_10;
                    FP decelerate = FP._0_05;
                    FP BaseSpeed = Constants._2_50 + ((2-tank->SegmentNumber) * FP._0_50);
                    FP clamper = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - decelerate, BaseSpeed);

                    physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (direction * accelerate), -clamper, clamper);

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
                    physicsObject->Velocity.Y = 7;
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
            var tank = f.Unsafe.GetPointer<Tank>(thisEntity);
            if (!f.Exists(tank->MoleEntity))
                return;
            var boss = f.Unsafe.GetPointer<Boss>(tank->MoleEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
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
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteractWithPlayer(f, marioEntity))
                return;
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            //ALWAYS stomp this guy
            boss->BossHarmed(f, thisEntity, damageDirection.X < 0, mario->IsGroundpoundActive ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, false);
            mario->DoEntityBounce = mario->CurrentPowerupState == PowerupState.MiniMushroom || !mario->IsGroundpounding;
            mario->IsDrilling = false;
            marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X + ((damageDirection.X > 0 ? 1 : -1) * 3), -5, 5);
        }
        public void OnProjectileMontyInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var monty = f.Unsafe.GetPointer<Monty>(thisEntity);
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
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
                boss->BossHarmed(f, thisEntity, projectile->FacingRight, KnockbackStrength.FireballBump, false);
                //create unique particle effect for freeze?
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
        }
        public void OnBossMontyInteraction(Frame f, EntityRef bossEntity, EntityRef thisEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            var otherboss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (!boss->BossCanInteract() || !otherboss->BossCanInteract())
                return;
            f.Signals.BossToBossInteraction(thisEntity, bossEntity);
            f.Signals.BossToBossInteraction(bossEntity, thisEntity);
        }
        
        
        //global for all bosses
        public void OnEnemyBossInteraction(Frame f, EntityRef enemyEntity, EntityRef thisEntity) {
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
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out BulletBill* bill) && bill->Owner != thisEntity) {
                bill->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
                //boss->BossBump(f, monty->OwnerEntity, f.Unsafe.GetPointer<Enemy>(enemyEntity)->FacingRight, KnockbackStrength.None);
                //damage instead
                boss->BossHarmed(f, thisEntity, f.Unsafe.GetPointer<Enemy>(enemyEntity)->FacingRight, KnockbackStrength.FireballBump, true);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out Bobomb* bomb) && f.Unsafe.TryGetPointer<Holdable>(enemyEntity, out var holder) && holder->PreviousHolder != thisEntity) {
                bomb->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            } else if (f.Unsafe.TryGetPointer(enemyEntity, out PiranhaPlant* plant)) {
                plant->Kill(f, enemyEntity, thisEntity, EnemyKillReason.Special);
            }
        }
        public bool OnIceblockBossInteraction(Frame f, EntityRef iceblockEntity, EntityRef thisEntity, PhysicsContact contact) {
            var boss = f.Unsafe.GetPointer<Boss>(thisEntity);
            if (!boss->BossCanInteract())
                return true;

            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceblockEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(iceblockEntity);

            if (!iceBlock->IsSliding) {
                IceBlockSystem.Destroy(f, iceblockEntity, IceBlockBreakReason.InvincibleMario, thisEntity);
                return false;
            }

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (upDot >= Constants.PhysicsGroundMaxAngleCos) {
                // Top
            } else if (upDot <= -Constants.PhysicsGroundMaxAngleCos) {
                // Bottom
                boss->BossHarmed(f, thisEntity, iceBlock->FacingRight, KnockbackStrength.FireballBump, true);
                IceBlockSystem.Destroy(f, iceblockEntity, IceBlockBreakReason.HitWall, thisEntity);
                return false;
            } else {
                // Side
                bool rightContact = contact.Normal.X > 0;
                if (iceBlock->FacingRight == rightContact) {
                    boss->BossHarmed(f, thisEntity, iceBlock->FacingRight, KnockbackStrength.FireballBump, true);
                    IceBlockSystem.Destroy(f, iceblockEntity, IceBlockBreakReason.HitWall, thisEntity);
                    return false;
                }
            }
            //h
            return true;
        }
        #endregion

        #region Signals
        public void BossDeath(Frame f, EntityRef thisEntity) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Tank* tank) 
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* phys)) {
                return;
            }

            var boss = f.Unsafe.GetPointer<Boss>(tank->MoleEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(tank->MoleEntity);

            phys->IsFrozen = true;
            tank->State = MontyState.ReadyUp;
            boss->SetAttachmentsLifetime(f, thisEntity, tank, hazard);
            HazardSystem.DestroyHazard(f, tank->SegmentEntity);
            HazardSystem.DestroyHazard(f, tank->MoleEntity);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity, ExplosionType type) {
            //This handles bomb interactions for all bosses
            if (f.Unsafe.TryGetPointer(entity, out Boss* boss)) {
                 boss->BossHarmed(f, entity, boss->FacingRight, KnockbackStrength.Normal, true);
            }
        }
        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            EntityRef entity = iceBlock->Entity;
            if (!f.Unsafe.TryGetPointer(entity, out Boss* boss)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                return;
            }

            physicsObject->Velocity = FPVector2.Zero;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;


            bool hitFromRight = boss->FacingRight;
            if (f.Unsafe.TryGetPointer(attacker, out Transform2D* attackerTransform)) {
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(entity);
                QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, attackerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                hitFromRight = ourPos.X < theirPos.X;
            }

            bool damaged = true;
            KnockbackStrength strength = KnockbackStrength.Normal;
            switch (breakReason) {
            case IceBlockBreakReason.HitWall:
            case IceBlockBreakReason.Other:
            case IceBlockBreakReason.BlockBump:
                // that's the best you got?
                boss->BossHarmed(f, entity, iceBlock->FacingRight, KnockbackStrength.FireballBump, true);
                break;

            case IceBlockBreakReason.Shell:
                // shell derby
                boss->BossHarmed(f, entity, iceBlock->FacingRight, KnockbackStrength.Normal, true);
                break;

            case IceBlockBreakReason.Explosion:
            case IceBlockBreakReason.InvincibleMario:
            case IceBlockBreakReason.Groundpounded:
                // ow.
                boss->BossHarmed(f, entity, iceBlock->FacingRight, KnockbackStrength.Groundpound, true);
                break;

            case IceBlockBreakReason.Timer:
                // I'M FREEE
                boss->iframes = 121;
                damaged = false;
                break;

            default:
                Log.DebugWarn($"Unhandled IceBlockBreakReason {breakReason} in {nameof(OnIceBlockBroken)}! Defaulting to {IceBlockBreakReason.Other}");
                goto case IceBlockBreakReason.Other;
            }

            if (damaged) {
                FPVector2 particlePos = f.Unsafe.GetPointer<Transform2D>(brokenIceBlock)->Position;
                particlePos.Y += iceBlock->Size.Y / 2;
                f.Events.PlayKnockbackEffect(entity, brokenIceBlock, strength, particlePos, true);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, byte ExtraA, byte ExtraB, byte ExtraC, byte ExtraD) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Tank* tank)) {
                return;
            }

            //pre setup
            tank->LastMontyPos = tank->MoleSeats[0];
            for (int i = 0; i < 3; i++) {
                tank->FacingDirection[i] = -90;
            }

            //create parts
            tank->SegmentEntity = f.Create(tank->SegmentPrototype);
            var newSegmentTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
            newSegmentTransform->Position = spawnpoint;

            tank->MoleEntity = f.Create(tank->MolePrototype);
            f.Unsafe.GetPointer<Monty>(tank->MoleEntity)->OwnerEntity = thisEntity;
            var newMoleTransform = f.Unsafe.GetPointer<Transform2D>(tank->SegmentEntity);
            newMoleTransform->Position = spawnpoint;

            var boss = f.Unsafe.GetPointer<Boss>(tank->MoleEntity);
            boss->Health = Constants.GeneralBossHealth;

            //setup parts
            var molehaz = f.Unsafe.GetPointer<Hazard>(tank->MoleEntity);
            var seghaz = f.Unsafe.GetPointer<Hazard>(tank->SegmentEntity);
            //UGLY hardcode.
            switch (spawnReason) {
            case SpawnReason.Item:
                molehaz->IsCoinItem = seghaz->IsCoinItem = true;
                molehaz->LifeTime = seghaz->LifeTime = 600;
                break;
            default:
            case SpawnReason.Normal:
                molehaz->IsHazard = seghaz->IsHazard = true;
                molehaz->BaseLifeTime = seghaz->BaseLifeTime = molehaz->LifeTime = seghaz->LifeTime = f.Global->Rules.HazardLifetime * 60;
                break;
            }
            molehaz->Team = seghaz->Team = 255;
        }
        public void BossToBossInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            if ( !f.Unsafe.TryGetPointer(thisEntity, out Monty* monty)
                || !f.Unsafe.TryGetPointer(thisEntity, out Boss* boss)) {
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
