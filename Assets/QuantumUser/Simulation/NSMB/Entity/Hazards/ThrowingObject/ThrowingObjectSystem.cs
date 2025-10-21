using Photon.Deterministic;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class ThrowingObjectSystem : SystemMainThreadFilterStage<ThrowingObjectSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {
        /*
        ---------------------------------------

        Make Player Treat These As semi Solids If Stuck inside If It's A Solid Carryable

        Add The Ability For These To Collide With Tiles And Break Them (Not All Of Them)
        Make Them Not Stump all velocity on carry (heavystone is suposed to kinda do this dw about that)
        make them uncollidable when carried (unless from above)


        PropellerBox - animate mario
        BillBlock - animate billblock & mario
        baseball - animate, make unsolid

        Spring - (Actually Implement)
        RedPow - (Actually Implement)
        BluePow - (Actually Implement)
        Barrel - (Actually Implement)
        Freezie - (Actually Implement) (players frozen by this are frozen for a long while, with a unique sprite)
        CannonBox - (Actually Implement)
        fridge - (Actually Implement)

        Make Cannonbox & Coinbox Change Texutre Depending On Player

        ---------------------------------------
        */
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public ThrowingObject* DisObject;
            public Holdable* holdable;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;

            public Hazard* hazard;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, ThrowingObject>(f, OnThrowingObjectMarioInteraction);
            f.Context.Interactions.Register<MarioPlayer, ThrowingObject>(f, OnThrowingObjectMarioSolidInteraction);
//add pre interaction like the breakable object does

            f.Context.Interactions.Register<ThrowingObject, Coin>(f, OnThrowingObjectCoinInteraction);
            f.Context.Interactions.Register<ThrowingObject, Goomba>(f, OnThrowingObjectGoombaInteraction);
            f.Context.Interactions.Register<ThrowingObject, Koopa>(f, OnThrowingObjectKoopaInteraction);
            f.Context.Interactions.Register<ThrowingObject, Bobomb>(f, OnThrowingObjectBobombInteraction);
            f.Context.Interactions.Register<ThrowingObject, BulletBill>(f, OnThrowingObjectBulletBillInteraction);
            f.Context.Interactions.Register<ThrowingObject, PiranhaPlant>(f, OnThrowingObjectPiranhaPlantInteraction);
            f.Context.Interactions.Register<ThrowingObject, Boo>(f, OnThrowingObjectBooInteraction);
            f.Context.Interactions.Register<ThrowingObject, IceBlock>(f, OnThrowingObjectIceBlockInteraction);
            f.Context.Interactions.Register<ThrowingObject, IceBlock>(f, OnThrowingObjectIceBlockInteractionStationary);
            f.Context.Interactions.Register<ThrowingObject, Boss>(f, OnThrowingObjectBossInteraction);
            f.Context.Interactions.Register<Projectile, ThrowingObject>(f, OnThrowingObjectProjectileInteraction);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var Dis = filter.DisObject;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var holdable = filter.holdable;
            var hazard = filter.hazard;

            if (Dis->IsFlying) {
                // Slowly float downwards
                physicsObject->IsTouchingGround = false;
                bool closeToGround;
                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, stage: stage, entity: filter.Entity)) {
                    closeToGround = false;
                } else {
                    closeToGround = physicsObject->IsTouchingGround
                        || (transform->Position.Y - stage.StageWorldMin.Y) <= 4
                        || PhysicsObjectSystem.Raycast(f, stage, transform->Position + (FPVector2.Left / 4), FPVector2.Down, FP._1_20, out _)
                        || PhysicsObjectSystem.Raycast(f, stage, transform->Position + (FPVector2.Right / 4), FPVector2.Down, FP._1_20, out _);
                }
                FP targetVel = closeToGround ? Constants._2_50 : -Constants._2_50;

                physicsObject->Velocity.X *= Constants._0_95;
                physicsObject->Velocity.Y = QuantumUtils.MoveTowards(physicsObject->Velocity.Y - (physicsObject->Gravity.Y * f.DeltaTime), targetVel, 6 * f.DeltaTime);
            } else {
                // Bounce Logic
                if ((Dis->Thrown || Dis->BounceTimes > 0) && physicsObject->IsTouchingGround) {
                    Dis->HitSomething = true;
                    if (Dis->GroundBounce && Dis->BounceTimes < 3) {
                        Dis->BounceTimes += 1;
                        physicsObject->IsTouchingGround = false;
                        physicsObject->Velocity.Y = 4 - Dis->BounceTimes;
                        if (!Dis->IsBall)
                            physicsObject->Velocity.X *= Constants._0_66;
                    } else {
                        if (!Dis->IsBall)
                            physicsObject->Velocity.X = 0;
                        Dis->BounceTimes = 0;
                        Dis->Thrown = Dis->HitSomething = false;
                    }
                } else if (physicsObject->Velocity.Y < -6) {
                    Dis->BounceTimes = 1;
                } else if (physicsObject->IsTouchingGround && physicsObject->Velocity.X != 0 && !Dis->IsBall) {
                    physicsObject->Velocity.X = 0;
                }
                if (Dis->IsBall) {
                    if (physicsObject->IsTouchingLeftWall) {
                        physicsObject->Velocity.X = FPMath.Abs(physicsObject->PreviousFrameVelocity.X);
                    } else if (physicsObject->IsTouchingRightWall) {
                        physicsObject->Velocity.X = FPMath.Abs(physicsObject->PreviousFrameVelocity.X) * -1;
                    }
                    if (physicsObject->IsTouchingGround) {
                        if (physicsObject->IsOnSlideableGround) {
                            physicsObject->Velocity.X += physicsObject->FloorAngle * Constants.SmoothSlowdownmultiplier;
                        } else {
                            physicsObject->Velocity.X *= Constants._0_90;
                        }
                        if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_01) {
                            physicsObject->Velocity.X = 0;
                        } else {
                            physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -10, 10);
                        }
                    }
                }
            }

            //physicsObject->DisableCollision = false;

            if (hazard->IsHazard && holdable->Holder != EntityRef.None && hazard->LifeTime < hazard->BaseLifeTime) { //?
                hazard->LifeTime = hazard->BaseLifeTime;
            } else if (holdable->PreviousHolder != EntityRef.None && hazard->LifeTime > 1 && !Dis->Thrown) {
                //if it was already interacted with and ignored, despawn faster
                //UnityEngine.Debug.Log("Despawning Fast");
                hazard->LifeTime--;
            }

            // Special Updates
            switch (Dis->Type) {
            case ThrowingObjectType.Basic:
                break;
            case ThrowingObjectType.Stone: {
                #region HeavyStone
                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                    var entity = filter.Entity;
                    f.Events.ThrowObjSimple(entity, f.Unsafe.GetPointer<Transform2D>(entity)->Position);
                }
                break;
                #endregion
            }
            case ThrowingObjectType.Spring:
                break;
            case ThrowingObjectType.Pow:
                if (!physicsObject->IsFrozen) {
                    if (Dis->HitSomething /*|| (hazard->IPWSUntilGround && hazard->IPWSTime == 0 && physicsObject->IsTouchingGround)*/ || (Dis->Thrown &&
                    (physicsObject->IsTouchingGround || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->IsTouchingCeiling))) {
                        f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                        hazard->LifeTime = 15;
                        physicsObject->IsFrozen = true;
                    }
                } else {
                    if (hazard->LifeTime <= 10 && hazard->LifeTime > 5) {
                        Shape2D shape = Shape2D.CreateCircle(Dis->ReusableValue == 0 ? 1 : 4);
                        var hits = f.Physics2D.OverlapShape(*transform, shape);
                        for (int i = 0; i < hits.Count; i++) {
                            var hit = hits[i];
                            if (hit.Entity == filter.Entity) {
                                continue;
                            }

                            f.Signals.OnBobombExplodeEntity(filter.Entity, hit.Entity, Dis->ReusableValue == 0 ? ExplosionType.Shockwave : ExplosionType.GroundedShockwave);
                        }
                    }
                    if (!hazard->IsHazard) {
                        QuantumUtils.Decrement(ref hazard->LifeTime);
                        if (hazard->LifeTime <= 1) {
                            HazardSystem.DestroyHazard(f, filter.Entity);
                            hazard->LifeTime = 0;
                            Dis->HitSomething = Dis->Thrown = false;
                        }
                    }
                }

                break;
            case ThrowingObjectType.Barrel:
            case ThrowingObjectType.Freezie:
                break;
            case ThrowingObjectType.CoinBox: {
                #region CoinBox
                if (!Dis->Thrown && !f.Exists(holdable->Holder)) {
                    Dis->ReusableTimer = 5;
                    break;
                }
                if (Dis->ReusableTimer > 0) {
                    FP Distance = Dis->Thrown ? FP._0_50 : f.Unsafe.TryGetPointer(holdable->Holder, out PhysicsObject* marioPhysicsObject) ? (FPMath.Abs(marioPhysicsObject->Velocity.X) / 10) : 0;
                    Dis->ReusableTimer -= Distance;
                } else {
                    var entity = filter.Entity;

                    var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                    byte newCoins = (byte) (mario->Coins + 1);
                    bool item = newCoins == f.Global->Rules.CoinsForPowerup;
                    if (item) {
                        mario->Coins = 0;
                        MarioPlayerSystem.SpawnItem(f, holdable->PreviousHolder, mario, default, false);
                        Dis->ReusableTimer = 50;
                    } else {
                        mario->Coins = newCoins;
                        Dis->ReusableTimer = 5;
                    }

                    f.Events.ThrowObjSimple(entity, f.Unsafe.GetPointer<Transform2D>(entity)->Position + (FPVector2.Up / 2));
                    f.Events.MarioPlayerCollectedCoin(holdable->PreviousHolder, newCoins, item, f.Unsafe.GetPointer<Transform2D>(entity)->Position + FPVector2.Up, false, false);
                }
                break;
                #endregion
            }
            case ThrowingObjectType.PropellerBox:
                break;
            case ThrowingObjectType.BillBlock: {
                #region BillBlock
                bool holderexists = f.Exists(holdable->Holder);
                if (Dis->Thrown && Dis->ReusableTimer > 0) {
                    Dis->ReusableTimer -= 1;
                    //Throw Hovers for a bit
                    physicsObject->Velocity.Y = -FP._0_10;
                    if (Dis->ReusableTimer <= 0) {
                        f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                        physicsObject->Velocity.X = Dis->Facing ? FP._1_50 : -FP._1_50;
                    }
                } else if (!holderexists) {
                    //Not hovering, fall
                    if (Dis->IsFlying) {
                        //if has wings, fly
                        if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)
                            Dis->Facing = physicsObject->IsTouchingLeftWall;
                        physicsObject->Velocity.X = QuantumUtils.MoveTowards(physicsObject->Velocity.X, Dis->Facing ? FP._1_50 : -FP._1_50, 2 * f.DeltaTime);
                    }
                }

                if (!Dis->Thrown && !holderexists) {
                    Dis->ReusableTimer = 240;
                    break;
                }
                var entity = filter.Entity;
                if (!f.Exists(holdable->PreviousHolder)) {
                    break;
                }
                var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                var marioPhys = f.Unsafe.GetPointer<PhysicsObject>(holdable->PreviousHolder);
                byte newCoins = (byte) (mario->Coins + 1);
                bool item = newCoins == f.Global->Rules.CoinsForPowerup;
                /*if (marioPhys->IsTouchingGround || marioPhys->WasTouchingGround) { //Check if the "wastouchingground" being set to false when mario jumps is needed
                    Dis->ReusableTimer = 180;
                } else */
                if (Dis->ReusableTimer > 0 && f.GetPlayerInput(mario->PlayerRef)->Jump.IsDown && (marioPhys->Velocity.Y <= 0 || Dis->ReusableTimer == 1)) { //get inputs
                    Dis->ReusableTimer -= 1;
                    if (Dis->ReusableTimer <= 0)
                        f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                }
                break;
                #endregion
            }
            case ThrowingObjectType.CannonBox:
            case ThrowingObjectType.Fridge:
                break;
            }
        }

        #region Interactions
        public static bool OnThrowingObjectMarioSolidInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            if (holdable->IsSolidCarryable) {
                return OnMarInteraction(f, marioEntity, thisEntity);
            } else {
                return false;
            }
        }
        public static void OnThrowingObjectMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            if (!holdable->IsSolidCarryable) {
                OnMarInteraction(f, marioEntity, thisEntity);
            }
        }

        public static bool OnMarInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (f.Exists(holdable->Holder)) {
                //Force The player To Drop This Item if it's GroundPounded While They Are Carrying It
                if (mario->IsGroundpoundActive) {
                    holdable->DropWithoutThrowing(f, thisEntity);
                    f.Events.PlayComboSound(thisEntity, 0);
                }
                return false;
            }
            if (holdable->PreviousHolder == marioEntity && holdable->IgnoreOwnerFrames > 0) {
                return false;
            }
            #region SetValues
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position; var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            bool hitRight = Dis->Thrown ? !Dis->Facing : damageDirection.X > 0;
            #endregion

            if (Dis->IsFlying && (mario->IsGroundpoundActive || damageDirection.Y < -Constants._0_66)) {
                Dis->IsFlying = false;
                physicsObject->Velocity.Y = 5;
            } else  if ((Dis->Thrown || (!physicsObject->IsTouchingGround && Dis->Type == ThrowingObjectType.Stone)) && mario->IsDamageable && !(holdable->PreviousHolder == marioEntity && Dis->IgnoreTeamates)) {
                // Hit Player (Unless Not)
                if (Dis->BouceOffPlayer) {
                    Dis->HitSomething = true;
                    Dis->Thrown = false;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity = new FPVector2(hitRight ? 1 : -1, 4);
                }
                if (Dis->GroundBounce)
                    Dis->BounceTimes = 1;
                if (Dis->StarsToDrop != 0) {
                    bool TeamateItem = Dis->IgnoreTeamates && (mario->GetTeam(f) == hazard->Team);
                    mario->DoKnockback(f, marioEntity, hitRight, TeamateItem ? 0 : Dis->StarsToDrop, /*TeamateItem*/ KnockbackStrength.FireballBump, thisEntity);
                    f.Events.PlayKnockbackEffect(marioEntity, thisEntity, KnockbackStrength.FireballBump,
                        (f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position + f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position) / 2);
                }
                return false;
            } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom && damageDirection.Y <= Constants._0_66) {
                // HOMERUN
                f.Events.PlayComboSound(thisEntity, 0);
                Dis->Thrown = true;
                Dis->HitSomething = false;
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity = new FPVector2(hitRight ? -8 : 8, 5);
                return false;
            } else if (damageDirection.Y <= Constants._0_66 && FPMath.Abs(damageDirection.X) < Constants._0_90 && !(physicsObject->IsTouchingGround && upDot <= -Constants.PhysicsGroundMaxAngleCos)) {
                //PlayerInsideObject
                /*
                if (Dis->BouceOffPlayer) {
                    // Bouce Off Player
                    Dis->HitSomething = true;
                    Dis->Thrown = false;
                    physicsObject->Velocity = new FPVector2(Dis->Facing ? -1 : 1, 4);
                    physicsObject->IsTouchingGround = false;
                    if (Dis->GroundBounce)
                        Dis->BounceTimes = 1;
                } else {
                    //physicsObject->DisableCollision = true;
                }*/
            }
            if ((!Dis->Thrown || mario->GetTeam(f) == f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder)->GetTeam(f)) 
              && (damageDirection.Y <= Constants._0_66 || Dis->HitSomething)) {
                //Only Allow Carry If No Team Or Same Team --- TOTEST
                if (hazard->Team != 255 && mario->GetTeam(f) != hazard->Team) { //Can only pickup if it's on our team... or no team
                    return false;
                }

                // Attempt pickup
                if (mario->CanPickupItem(f, marioEntity, thisEntity)) {
                    // Pickup successful
                    holdable->Pickup(f, thisEntity, marioEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                    marioPhysicsObject->Velocity.X = marioPhysicsObject->PreviousFrameVelocity.X;
                    if (!holdable->HoldAboveHead) {
                        DisCollider->Enabled = false;
                        //f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                        physicsObject->DisableCollision = true;
                    }
                    Dis->HitSomething = Dis->IsFlying = false;
                    // Enable Carryabilites
                    switch (Dis->Type) {
                    case ThrowingObjectType.Stone: {
                        marioPhysicsObject->Velocity.X /= 2;
                        mario->StoneBux = true;
                        break;
                    }
                    case ThrowingObjectType.CoinBox:
                        Dis->ReusableTimer = 5;
                        break;
                    case ThrowingObjectType.PropellerBox:
                        mario->PropellerBux = true;
                        break;
                    case ThrowingObjectType.BillBlock:
                        mario->BillBux = true;
                        Dis->ReusableTimer = 240;
                        break;
                    }
                }
            }
            return false;
        }
        
        public static void OnThrowingObjectCoinInteraction(Frame f, EntityRef thisEntity, EntityRef coinEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            if (!(Dis->Thrown && f.Exists(holdable->PreviousHolder))) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public static void OnThrowingObjectGoombaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                goomba->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }

        public static void OnThrowingObjectKoopaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var koopa = f.Unsafe.GetPointer<Koopa>(otherEntity);

            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                koopa->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }

        public static void OnThrowingObjectBobombInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                bobomb->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectBulletBillInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bill = f.Unsafe.GetPointer<BulletBill>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                bill->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectPiranhaPlantInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var plant = f.Unsafe.GetPointer<PiranhaPlant>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                plant->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectBooInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var boo = f.Unsafe.GetPointer<Boo>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                boo->Kill(f, otherEntity, thisEntity, KillReason.Special);
            }
        }
        public static void OnThrowingObjectIceBlockInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var ice = f.Unsafe.GetPointer<IceBlock>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                IceBlockSystem.Destroy(f, otherEntity, IceBlockBreakReason.Other);
            }
        }
        public static void OnThrowingObjectIceBlockInteractionStationary(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var ice = f.Unsafe.GetPointer<IceBlock>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                IceBlockSystem.Destroy(f, otherEntity, IceBlockBreakReason.Other);
            }
        }
        public static void OnThrowingObjectBossInteraction(Frame f, EntityRef thisEntity, EntityRef bossEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->iframes > 0 || boss->Dead)
                return;

            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            if (Dis->Thrown && Dis->StarsToDrop != 0) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                Dis->HitSomething = true;
                Dis->Thrown = false;
                physicsObject->Velocity = new FPVector2(Dis->Facing ? -3 : 3, 4);
                physicsObject->IsTouchingGround = false;
                if (Dis->GroundBounce)
                    Dis->BounceTimes = 1;

                f.Events.PlayKnockbackEffect(bossEntity, thisEntity, KnockbackStrength.FireballBump, 
                    (f.Unsafe.GetPointer<Transform2D>(bossEntity)->Position + f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position) / 2);

                //Damage Boss
                boss->BossHarmed(f, bossEntity, Dis->StarsToDrop == 1 ? KnockbackStrength.FireballBump : Dis->StarsToDrop == 3 ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, Dis->StarsToDrop > 1);
                f.Events.PlayBossHitSound(bossEntity);
            }
        }

        public static void OnThrowingObjectProjectileInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            if (f.Exists(holdable->Holder) && Dis->Type != ThrowingObjectType.Stone) {
                //Force The player To Drop This Item if it's hit While They Are Carrying It
                holdable->DropWithoutThrowing(f, thisEntity);
                f.Events.PlayComboSound(thisEntity, 0);
                f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
            }
        }
        #endregion

        #region Signals
        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out ThrowingObject* Dis)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysicsObject)) {
                return;
            }

            //TODO: Up key
            Dis->Thrown = !dropped && !crouching;
	        Dis->HitSomething = false;
            Dis->Facing = mario->FacingRight;
            FP bonusSpeed = FPMath.Abs(marioPhysicsObject->Velocity.X / 2);
            if (FPMath.Sign(marioPhysicsObject->Velocity.X) != (mario->FacingRight ? 1 : -1)) {
                bonusSpeed *= -1;
            }
            physicsObject->Velocity.X = (Dis->ThrowForce + bonusSpeed) * (mario->FacingRight ? 1 : -1);
            physicsObject->Velocity.Y = 2;
            holdable->IgnoreOwnerFrames = 20; //15

            f.Unsafe.GetPointer<PhysicsCollider2D>(entity)->Enabled = true;
            //f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
            physicsObject->DisableCollision = false;

            // Disable Carryabilites
            switch (Dis->Type) {
            case ThrowingObjectType.Stone: {
                Dis->Thrown = true;
                marioPhysicsObject->Velocity.X /= 4;
                mario->StoneBux = false;
                break;
            }
            case ThrowingObjectType.CoinBox:
                Dis->ReusableTimer = 5;
                break;
            case ThrowingObjectType.PropellerBox:
                mario->PropellerBux = false;
                mario->IsPropellerFlying = false;
                mario->IsSpinnerFlying = false;
                mario->UsedPropellerThisJump = false;
                break;
            case ThrowingObjectType.BillBlock:
                mario->BillBux = false;
                physicsObject->Velocity.X = Dis->ThrowForce * (mario->FacingRight ? 1 : -1);
                marioPhysicsObject->Velocity.Y += 3;
                Dis->ReusableTimer = 60;
                break;
            }

            if (Dis->Thrown) {
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
        }

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
            case ThrowingObjectType.Pow:
                // Activate These
                Dis->HitSomething = true;
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

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out ThrowingObject* Dis)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            switch (Dis->Type) {
            case ThrowingObjectType.Basic:
            case ThrowingObjectType.Stone:
            case ThrowingObjectType.Spring:
                break;
            case ThrowingObjectType.Pow:
                Dis->ReusableValue = (byte) (hazardata.SpecialValues[0].BaseValue == 1 ? 1 : 0);
                if (hazardata.SpecialValues[1].BaseValue == 1) {
                    Dis->HitSomething = true;
                    hazard->IPWSTime = 0;
                }
                if (hazardata.SpecialValues[1].BaseValue == 2) {
                    physicsObject->Velocity.Y = 20;
                    //idk maybe play a fling sound
                }
                break;
            case ThrowingObjectType.Barrel:
            case ThrowingObjectType.Freezie:
                break;
            case ThrowingObjectType.CoinBox:
            case ThrowingObjectType.PropellerBox:
            case ThrowingObjectType.BillBlock:
            case ThrowingObjectType.CannonBox:
            case ThrowingObjectType.Fridge:
                break;
            }

            if (Dis->IsFlying) {
                hazard->IPWSUntilGround = false;
            }
        }
        #endregion
    }
}
