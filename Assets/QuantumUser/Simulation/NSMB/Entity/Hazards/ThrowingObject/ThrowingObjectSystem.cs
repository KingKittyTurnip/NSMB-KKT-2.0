using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Drawing.Drawing2D;
using UnityEngine;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.LowLevelPhysics2D.PhysicsShape;

namespace Quantum {
    
    public unsafe class ThrowingObjectSystem : SystemMainThreadEntityFilter<ThrowingObject, ThrowingObjectSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {
        /*
        ---------------------------------------

        Make Player Treat These As semi Solids If Stuck inside If It's A Solid Carryable
        Add The Ability For These To Collide With Tiles And Break Them (Not All Of Them)
        Make Them Not Stump all velocity on carry (heavystone is suposed to kinda do this dw about that) (did i fix this?)


        PropellerBox - animate mario
        BillBlock - mario anims
        ChainPost - Fix the Connected Boss Defeated Bug, make connecter ignore the post's collision
        springboard - make enemies collide, for some reason mario can sometimes pick it up after jumping off it

        Barrel - (Actually Implement)
        fridge - (Actually Implement)

        For Later:
        Purple Coin Block


        ---------------------------------------
        */

        //Magic numbers

        FP CannonBoxCooldown = 2;
        FP CannonBoxShootDelay = FP._0_20;
        FP CannonBoxChargeLimit = FP._0_50;
        FP CannonBoxPlayerCooldown = 1;

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
            f.Context.Interactions.Register<MarioPlayer, ThrowingObject>(f, OnThrowingObjectMarioSolidInteraction);
            f.Context.RegisterPreContactCallback(f, OnThrowingObjectMarioSolidPreContact);
            f.Context.Interactions.Register<MarioPlayer, ThrowingObject>(f, OnThrowingObjectMarioInteraction);

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
            f.Context.Interactions.Register<ThrowingObject, ThrowingObject>(f, OnThrowingObjectThrowingObjectInteraction);

            //springboard interactions
            f.Context.Interactions.Register<PhysicsObject, SpringBoard>(f, OnSpringboardAnythingInteraction);
            f.Context.Interactions.Register<PhysicsObject, SpringBoard>(f, OnSpringboardSolidInteraction);

            //exclusivly used for the chainpost's connection to ignore the chainpost collision
            f.Context.RegisterPreContactCallback(f, OnPreContactCallback);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var Dis = filter.DisObject;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var holdable = filter.holdable;
            var hazard = filter.hazard;
            var entity = filter.Entity;
            var coinitem = f.Unsafe.GetPointer<CoinItem>(entity);

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
                // Slide Logic
                if (Dis->SlideAlong) {
                    //sliding projectiles don't bounce at all
                    if (!Dis->Thrown && physicsObject->IsTouchingGround && physicsObject->WasTouchingGround) {
                        physicsObject->Velocity.X = 0;
                    }
                } else
                // Bounce Logic
                if ((Dis->Thrown || Dis->BounceTimes > 0) && physicsObject->IsTouchingGround) {
                    Dis->HitSomething = Dis->Thrown;
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
                //Roll Logic
                if (Dis->IsBall) {
                    if (physicsObject->IsTouchingLeftWall) {
                        physicsObject->Velocity.X = FPMath.Abs(physicsObject->PreviousFrameVelocity.X);
                    } else if (physicsObject->IsTouchingRightWall) {
                        physicsObject->Velocity.X = FPMath.Abs(physicsObject->PreviousFrameVelocity.X) * -1;
                    }
                    if (physicsObject->IsTouchingGround) {
                        if (physicsObject->IsOnSlideableGround) {
                            physicsObject->Velocity.X += physicsObject->FloorAngle * Constants.BallSlopeVelocityMultiplier;
                            Dis->BounceTimes = 0;
                        } else {
                            physicsObject->Velocity.X *= Constants.BallSlowDownMultiplier;
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

            if (holdable->Holder != EntityRef.None && hazard->LifeTime < hazard->BaseLifeTime && (hazard->IsHazard || hazard->IsCoinItem)) {
                 hazard->LifeTime = hazard->BaseLifeTime;
            } else if (holdable->PreviousHolder != EntityRef.None && hazard->LifeTime > 1 && !Dis->Thrown) {
                //if it was already interacted with and ignored, despawn faster
                //UnityEngine.Debug.Log("Despawning Fast");
                hazard->LifeTime--;
            }

            bool holderExists = f.Exists(holdable->Holder);

            if (coinitem->SpawnAnimationFrames > 0) {
                return;
            }

            // Special Updates
            switch (Dis->Type) {
            case ThrowingObjectType.Basic:
            case ThrowingObjectType.PropellerBox:
                //no special code
                break;
            case ThrowingObjectType.Stone: {
                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                    f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                }
                break;
            }
            case ThrowingObjectType.Spring:
                if (f.Exists(Dis->ConnectedObject)) {
                    var TargetPhysics = f.Unsafe.GetPointer<PhysicsObject>(Dis->ConnectedObject);
                    var TargetTransform = f.Unsafe.GetPointer<Transform2D>(Dis->ConnectedObject);
                    var TargetCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(Dis->ConnectedObject);
                    FP Offset = TargetCollider->Shape.Centroid.Y - TargetCollider->Shape.Box.Extents.Y;

                    Dis->ReusableTimer += f.DeltaTime;
                    if (Dis->ReusableTimer > FP._0_20) {
                        //weee! - springboards get 0 x velocity when jumping off springboards for silly spring towers
                        TargetTransform->Position.Y = transform->Position.Y + FP._0_05 - Offset;
                        TargetPhysics->Velocity.X = f.Has<SpringBoard>(Dis->ConnectedObject) ? 0 : FPMath.Clamp(TargetPhysics->Velocity.X * 5, -3, 3);
                        TargetPhysics->Velocity.Y = f.Has<MarioPlayer>(Dis->ConnectedObject) ? 9 : 12;
                        physicsObject->IsFrozen = false;
                        Dis->ConnectedObject = EntityRef.None;
                        Dis->ReusableTimer += f.DeltaTime * 3;

                        physicsObject->Velocity.Y = 0;
                    } else {
                        //halt
                        TargetPhysics->Velocity.X = FPMath.Clamp(TargetPhysics->Velocity.X, -FP._0_50, FP._0_50);
                        TargetPhysics->Velocity.Y = -1;

                        TargetTransform->Position.Y = FPMath.Max(TargetTransform->Position.Y + Offset, transform->Position.Y + FP._0_05) - Offset;
                    }
                    if (holderExists && holdable->Holder == Dis->ConnectedObject) {
                        holdable->DropWithoutThrowing(f, holdable->Holder);
                    }
                    return;
                } else if (Dis->ReusableTimer != 0) {
                    //end
                    if (Dis->ConnectedObject != EntityRef.None) {
                        Dis->ConnectedObject = EntityRef.None;
                        physicsObject->Velocity = FPVector2.Zero;
                        physicsObject->IsFrozen = false;
                    }
                    Dis->ReusableTimer = 0;
                }
                break;
            case ThrowingObjectType.Pow:
                if (!physicsObject->IsFrozen) {
                    if (Dis->HitSomething /*|| (hazard->IPWSUntilGround && hazard->IPWSTime == 0 && physicsObject->IsTouchingGround)*/ || (Dis->Thrown &&
                    (physicsObject->IsTouchingGround || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->IsTouchingCeiling))) {
                        f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = physicsObject->DisableCollision = physicsObject->IsFrozen = true;
                        hazard->LifeTime = 120;
                        Dis->HitSomething = Dis->Thrown = false;
                        f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                    }
                } else {
                    var type = Dis->Varient == 1 ? ExplosionType.Shockwave : ExplosionType.GroundedShockwave;
                    if ((hazard->LifeTime <= 115 && hazard->LifeTime > 110) || type == ExplosionType.GroundedShockwave) {
                        Shape2D shape = Shape2D.CreateCircle(Dis->Varient == 1 ? 3 : 11);
                        var hits = f.Physics2D.OverlapShape(*transform, shape);
                        for (int i = 0; i < hits.Count; i++) {
                            var hit = hits[i];
                            if (hit.Entity == filter.Entity) {
                                continue;
                            }

                            f.Signals.OnBobombExplodeEntity(filter.Entity, hit.Entity, type);
                        }
                        if (Dis->Varient == 1 && (hazard->LifeTime == 10 && hazard->LifeTime == 5)) { //Red Pow Destroys Bricks
                            int sizeTiles = FPMath.FloorToInt(hazard->LifeTime == 10 ? 1 : 3);
                            IntVector2 origin = QuantumUtils.WorldToRelativeTile(stage, transform->Position + collider->Shape.Centroid);
                            for (int x = -sizeTiles; x <= sizeTiles; x++) {
                                for (int y = -sizeTiles; y <= sizeTiles; y++) {
                                    // Taxicab distance
                                    if (FPMath.Abs(x) + FPMath.Abs(y) > sizeTiles) {
                                        continue;
                                    }

                                    IntVector2 tilePos = origin + new IntVector2(x, y);
                                    StageTileInstance tileInstance = stage.GetTileRelative(f, tilePos);
                                    StageTile tile = f.FindAsset(tileInstance.Tile);
                                    if (tile is IInteractableTile it) {
                                        it.Interact(f, filter.Entity, InteractionDirection.Up, tilePos, tileInstance, out _);
                                    }
                                }
                            }
                        }
                    }
                    TryDrop();
                    if (!hazard->IsHazard) {
                        QuantumUtils.Decrement(ref hazard->LifeTime);
                        if (hazard->LifeTime <= 90) {
                            physicsObject->IsFrozen = false;
                            hazard->LifeTime = 0;
                            Dis->HitSomething = Dis->Thrown = false;
                            HazardSystem.DestroyHazard(f, filter.Entity);
                        }
                    }
                }

                break;
            case ThrowingObjectType.Barrel:
                if (Dis->Thrown && (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->Velocity.X == 0)) {
                    f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                    HazardSystem.DestroyHazard(f, filter.Entity);
                }
                break;
            case ThrowingObjectType.Freezie:
                if (Dis->Thrown && (Dis->HitSomething || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->Velocity.X == 0)) {
                    Dis->Thrown = Dis->HitSomething = false;
                    f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                    HazardSystem.DestroyHazard(f, filter.Entity);
                }
                break;
            case ThrowingObjectType.CoinBox: {
                if (!Dis->Thrown && !holderExists) {
                    if (Dis->ReusableTimer != 5 && holderExists)
                        f.Events.PlayComboSound(filter.Entity, 0);
                    Dis->ReusableTimer = 5;
                    break;
                }
                if (Dis->ReusableTimer > 0) {
                    FP Distance = Dis->Thrown ? FP._0_50 : f.Unsafe.TryGetPointer(holdable->Holder, out PhysicsObject* marioPhysicsObject) ? (FPMath.Abs(marioPhysicsObject->Velocity.X) / 10) : 0;
                    Dis->ReusableTimer -= Distance;
                } else {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                    EntityRef spawnedItem = EntityRef.None;

                    byte newCoins = (byte) (mario->Coins + 1);
                    bool item = newCoins == f.Global->Rules.CoinsForPowerup;
                    if (item) {
                        mario->Coins = 0;
                        spawnedItem = MarioPlayerSystem.SpawnItem(f, holdable->PreviousHolder, mario, default, false);
                        Dis->ReusableTimer = 50;
                    } else {
                        mario->Coins = newCoins;
                        Dis->ReusableTimer = 5;
                    }

                    f.Events.ThrowObjSimple(entity, transform->Position + (FPVector2.Up / 2));
                    f.Events.MarioPlayerCollectedCoin(holdable->PreviousHolder, newCoins, spawnedItem, f.Unsafe.GetPointer<Transform2D>(entity)->Position + FPVector2.Up, false, false);
                }
                break;
            }
            case ThrowingObjectType.BillBlock: {
                if (Dis->Thrown && Dis->ReusableTimer > 0) {
                    Dis->ReusableTimer -= 1;
                    //Throw Hovers for a bit
                    physicsObject->Velocity.Y = -FP._0_10;
                    if (Dis->ReusableTimer <= 0) {
                        f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                        physicsObject->Velocity.X = Dis->Facing ? 1 : -1;
                    }
                } else if (!holderExists) {
                    //Not hovering, fall
                    if (Dis->IsFlying) {
                        //if has wings, fly
                        if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall)
                            Dis->Facing = physicsObject->IsTouchingLeftWall;
                        physicsObject->Velocity.X = QuantumUtils.MoveTowards(physicsObject->Velocity.X, Dis->Facing ? FP._1_50 : -FP._1_50, 2 * f.DeltaTime);
                    }
                }

                if (!Dis->Thrown && !holderExists) {
                    Dis->ReusableTimer = 240;
                    break;
                }
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
            }
            case ThrowingObjectType.CannonBox:

                switch (Dis->Varient) {
                case 0: //Normal
                    if (!holderExists) {
                        if (QuantumUtils.Decrement(f, ref Dis->ReusableTimer)) {
                            CannonBoxStartShoot(1);
                        }
                    } else {
                        //Carried Cannonbox
                        var mario2 = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                        Dis->Facing = mario2->FacingRight;
                        Input* input = f.GetPlayerInput(mario2->PlayerRef);

                        if (input->PowerupAction.IsDown) {
                            //charge
                            QuantumUtils.Decrement(f, ref Dis->ReusableTimer);
                        } else if (Dis->ReusableTimer < CannonBoxChargeLimit) {
                            //shoot
                            CannonBoxStartShoot((byte) (Dis->ReusableTimer == 0 ? 2 : 1));
                        }
                    }
                    break;
                case 1:
                case 2: //Preparing to shoot
                    if (Dis->ReusableTimer > 0 && (Dis->ReusableTimer -= 1) <= 0) {
                        CannonBoxLaunchBullet();
                    }
                    break;
                case 3: //cooldown
                    if (QuantumUtils.Decrement(f, ref Dis->ReusableTimer)) {
                        Dis->Varient = 0;
                        Dis->ReusableTimer = CannonBoxChargeLimit;
                    }
                    break;
                }

                void CannonBoxStartShoot(byte variant) {
                    Dis->Varient = variant;
                    Dis->ReusableTimer = CannonBoxShootDelay;
                    f.Events.ThrowObjSimple(entity, transform->Position);
                }

                void CannonBoxLaunchBullet() {
                    FPVector2 spawnPos = transform->Position + new FPVector2(Dis->Facing ? FP._0_25 : -FP._0_25, FP._0_05);
                    EntityRef newEntity = f.Create(f.SimulationConfig.CannonBoxBulletPrototype);
                    var projectile = f.Unsafe.GetPointer<Projectile>(newEntity);
                    projectile->Initialize(f, newEntity, holdable->PreviousHolder, spawnPos, Dis->Facing, false);
                    if (Dis->Varient == 2)
                        projectile->Speed *= 2;
                    Dis->ReusableTimer = CannonBoxPlayerCooldown;
                    Dis->Varient = 3;
                }
                break;
            case ThrowingObjectType.Fridge:
                //TODO: (?)
                break;
            case ThrowingObjectType.Voidwall:
                if (!physicsObject->IsFrozen) {
                    if (Dis->HitSomething /*|| (hazard->IPWSUntilGround && hazard->IPWSTime == 0 && physicsObject->IsTouchingGround)*/ || (!Dis->IsFlying &&
                    (physicsObject->IsTouchingGround || physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall || physicsObject->IsTouchingCeiling))) {
                        f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                        physicsObject->DisableCollision = true;
                        hazard->LifeTime = 25;
                        physicsObject->IsFrozen = true;
                        f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                        holdable->Holder = EntityRef.None;
                    }
                } else {
                    TryDrop();
                    if (hazard->LifeTime == 1) {
                        //create voidwall
                        hazard->LifeTime = 0;
                        var NewObject = f.Create(f.SimulationConfig.VoidWallWall);
                        f.Signals.InitializeHazard(NewObject, EntityRef.None, transform->Position, SpawnReason.Forced, new QListPtr<byte>());
                        HazardSystem.DestroyHazard(f, filter.Entity);
                    }
                    holdable->Holder = EntityRef.None;
                    /*if (!hazard->IsHazard) {
                        QuantumUtils.Decrement(ref hazard->LifeTime);
                        if (hazard->LifeTime <= 90) {
                            physicsObject->IsFrozen = false;
                            hazard->LifeTime = 0;
                            Dis->HitSomething = Dis->Thrown = false;
                            HazardSystem.DestroyHazard(f, filter.Entity);
                        }
                    }*/
                }
                break;
            case ThrowingObjectType.ChainPost:
                if (f.Exists(Dis->ConnectedObject)) {
                    /*bool Yank = false;
                    if (f.Exists(holdable->Holder)) {
                        Input* input2 = f.GetPlayerInput(f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder)->PlayerRef);
                        if (input2->PowerupAction.WasPressed) {
                            Yank = true;
                            f.Events.ThrowObjSimple(filter.Entity, new FPVector2(0, -999));
                        }
                    }*/
                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, f.Unsafe.GetPointer<Transform2D>(Dis->ConnectedObject)->Position, out var PostPos, out var ObjectPos);
                    FP Distance = QuantumUtils.WrappedDistance(f, PostPos, ObjectPos);

                    FP Threshold = 3 * (Dis->Varient == 1 ? 2 : 1);

                    //Make Sure Their Despawn Timers Are The Same To prevent Oddities
                    //TODO: For Things That Aren't Throwables And Modify Their lifetime, This breaks (ex: bosses)
                    var chainedHazard = f.Unsafe.GetPointer<Hazard>(Dis->ConnectedObject);
                    if (hazard->LifeTime > chainedHazard->LifeTime) {
                        chainedHazard->LifeTime = hazard->LifeTime;
                    } else {
                        hazard->LifeTime = chainedHazard->LifeTime;
                    }

                    if (Distance > Threshold) {
                        var chainedPhysics = f.Unsafe.GetPointer<PhysicsObject>(Dis->ConnectedObject);
                        if (chainedPhysics->IsFrozen) {
                            //this object is immovable!
                            break;
                        }
                        FPVector2 damageDirection = (PostPos - ObjectPos).Normalized;

                        //pull torwards the post
                        chainedPhysics->IsTouchingGround = false;
                        FP DistanceLimit = 5 + (Distance * FP._0_50);
                        chainedPhysics->Velocity = new FPVector2(FPMath.Clamp(chainedPhysics->Velocity.X + (damageDirection.X * 2), -DistanceLimit, DistanceLimit), FPMath.Clamp(chainedPhysics->Velocity.Y + (damageDirection.Y * 2), -DistanceLimit, DistanceLimit));
                        //pull holder if it has one
                        if (f.Unsafe.TryGetPointer<Holdable>(Dis->ConnectedObject, out Holdable* chainedHoldable)) {
                            chainedHoldable->PreviousHolder = holdable->PreviousHolder;
                            if (chainedHoldable->Holder != EntityRef.None) {
                                f.Unsafe.GetPointer<PhysicsObject>(chainedHoldable->Holder)->Velocity = chainedPhysics->Velocity;
                            }
                            //Make object Thrown if Possible
                            if (f.Unsafe.TryGetPointer<ThrowingObject>(Dis->ConnectedObject, out ThrowingObject* chainedThrowable)) {
                                chainedThrowable->Thrown = !chainedThrowable->IsFlying;
                            }
                        }

                        if (Distance > Threshold * FP._1_75) {
                            //break
                            Dis->ConnectedObject = EntityRef.None;
                            Debug.Log(Dis->ConnectedObject + " Disconnected From " + filter.Entity);
                            f.Events.ThrowObjSimple(filter.Entity, transform->Position);
                        }
                    }
                }
                break;
            case ThrowingObjectType.KingBooStone:
                if (Dis->HitSomething) {
                    HazardSystem.DestroyHazard(f, filter.Entity);
                    return;
                }
                if (f.Exists(holdable->PreviousHolder) && !f.Has<Boss>(holdable->PreviousHolder) && !(f.Unsafe.TryGetPointer<MarioPlayer>(holdable->PreviousHolder, out var mar) && mar->IsBoss != EntityRef.None)) {
                    //We've Been Pickedup By A Player, ignore all other code
                    Dis->Varient = 3;
                    physicsObject->Gravity.Y = -10;
                    physicsObject->TerminalVelocity = -3;
                    if (f.Exists(holdable->Holder)) {
                        hazard->LifeTime = 120;
                    } else if (Dis->HitSomething) {
                        hazard->LifeTime = 1;
                    }
                } else if (Dis->Varient >= 2) {
                    //We've Hit Ground, Allow Us To Fall Into The Pit

                } else {
                    bool TouchingTiles = false;
                    Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
                    int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, transform->Position, filter.PhysicsCollider->Shape, tiles, stage);
                    for (int i = 0; i < overlappingTiles; i++) {
                        var tile = f.FindAsset(tiles[i].Tile.Tile);
                        if (tile && (tile.CollisionData.IsFullTile /*|| (tiles[i].Position.Y < transform->Position.Y && physicsObject->Velocity.Y < 0)*/)) { //TODO: make semis work
                            TouchingTiles = true;
                            break;
                        }
                    }
                    if (TouchingTiles) {
                        if (Dis->Varient == 1) {
                            //We Hit Ground, Allow Carryable
                            physicsObject->Gravity.Y = -10;
                            physicsObject->Velocity.X = physicsObject->Velocity.X > 0 ? 1 : -1;
                            physicsObject->Velocity.Y = 3;
                            physicsObject->TerminalVelocity = -1;
                            Dis->Thrown = false;
                            Dis->Varient = 2;
                            Dis->ReusableTimer = 1;
                            hazard->LifeTime = 200;
                            f.Events.PlayComboSound(filter.Entity, 0);
                        }
                    } else {
                        //We Are outside of ground
                        Dis->Varient = 1;
                        Dis->ReusableTimer = 3;
                    }
                }
                break;
            }

            void TryDrop() {
                if (holderExists) {
                    holdable->DropWithoutThrowing(f, holdable->Holder);
                }
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
        private void OnThrowingObjectMarioSolidPreContact(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            //test if this works better
            if (f.Unsafe.TryGetPointer<ThrowingObject>(contact.Entity, out var throwable)) {
                if (f.Unsafe.GetPointer<Interactable>(contact.Entity)->ColliderDisabled) {
                    keepContacts = false;
                    return;
                }
                //spring
                if (throwable->Type == ThrowingObjectType.Spring && f.Has<PhysicsObject>(contact.Entity)) {
                    keepContacts = HandleSpringboardInteraction(f, contact.Entity, entity, true);

                } else if (f.Unsafe.TryGetPointer<Holdable>(contact.Entity, out var holdable) && holdable->IsSolidCarryable) {
                    //if it's solid
                    if (f.Has<MarioPlayer>(entity) && !f.Exists(holdable->Holder)) {
                        //no holder, try to pickup so we don't lose velocity
                        keepContacts = OnMarInteraction(f, entity, contact.Entity);

                    } else if (f.Has<Boss>(entity)) {
                        keepContacts = OnBossInteraction(f, contact.Entity, entity);

                    } else if (f.Has<BigStar>(entity)) {
                        keepContacts = false;

                    } else if (f.Exists(holdable->Holder) && !holdable->HoldAboveHead) { //same as bellow
                        if (f.Has<Projectile>(entity)) { //the stone will make contact, but not eerything else
                            if (throwable->Type != ThrowingObjectType.Stone)
                                keepContacts = false;
                        } else if (f.Has<PhysicsObject>(entity) && !holdable->HoldAboveHead && f.Exists(holdable->Holder)) { //things will only make contact if it's a head held object
                            keepContacts = false;
                        } else {
                            keepContacts = true;
                        }
                    }
                }
            }
        }

        public static bool OnMarInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            if (f.Exists(holdable->Holder) && Dis->Type != ThrowingObjectType.Spring) {
                //Force The player To Drop This Item if it's GroundPounded While They Are Carrying It
                if (mario->IsGroundpoundActive) {
                    holdable->DropWithoutThrowing(f, thisEntity);
                    f.Events.PlayComboSound(thisEntity, 0);
                    switch (Dis->Type) {
                    case ThrowingObjectType.CoinBox:
                        Dis->ReusableTimer = 5;
                        break;
                    }
                }
                return false;
            }
            if (holdable->PreviousHolder == marioEntity && holdable->IgnoreOwnerFrames > 0) {
                return false;
            }
            if (f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled) {
                return false;
            }

            #region SetValues
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position; var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            bool hitRight = Dis->Thrown ? !Dis->Facing : damageDirection.X > 0;

            bool MarioGroundpounding = mario->IsGroundpoundActive && damageDirection.Y > Constants._0_66;
            #endregion

            //Special Interactions
            if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                if (damageDirection.Y <= Constants._0_66) {
                    // HOMERUN
                    f.Events.PlayComboSound(thisEntity, 0);
                    Dis->Thrown = true;
                    Dis->HitSomething = Dis->IsFlying = false;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity = new FPVector2(hitRight ? -8 : 8, 5);
                    switch (Dis->Type) {
                    case ThrowingObjectType.Freezie:
                        //break this
                        f.Events.ThrowObjSimple(thisEntity, ourPos);
                        HazardSystem.DestroyHazard(f, thisEntity);
                        return false;
                    case ThrowingObjectType.Pow:
                        //explode this
                        f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                        Dis->HitSomething = true;
                        return false;
                    }
                }
                return false;
            } else if (Dis->Type == ThrowingObjectType.Spring && damageDirection.Y > Constants._0_66) {
                mario->GroundpoundCooldownFrames = 20;
                mario->IsGroundpounding = mario->IsGroundpoundActive = false;
                HandleSpringboardInteraction(f, marioEntity, thisEntity, false);
            } else if (MarioGroundpounding) {
                //we groundpounded this object
                holdable->PreviousHolder = marioEntity;
                switch (Dis->Type) {
                case ThrowingObjectType.Freezie:
                    //break this
                    f.Events.ThrowObjSimple(thisEntity, ourPos);
                    HazardSystem.DestroyHazard(f, thisEntity);
                    return false;
                case ThrowingObjectType.ChainPost:
                    //break this
                    HazardSystem.DestroyHazard(f, thisEntity);
                    return false;
                case ThrowingObjectType.Pow:
                    //explode this
                    f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                    Dis->HitSomething = true;
                    return false;
                }
                if (Dis->IsFlying) {
                    Dis->IsFlying = false;
                    physicsObject->Velocity.Y = 5;
                    return false;
                }
            }

            //IsOwned, Counts Teamates
            bool IsOwned = f.Exists(holdable->PreviousHolder) && f.Unsafe.TryGetPointer<MarioPlayer>(holdable->PreviousHolder, out var holdermar) && mario->GetTeam(f) == holdermar->GetTeam(f) && Dis->IgnoreTeamates;
            
            //Try To Hit
            if ((Dis->Thrown || (!physicsObject->IsTouchingGround && Dis->Type == ThrowingObjectType.Stone)) && !IsOwned) {
                if (!mario->IsDamageable) {
                    return true;
                }
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
                    if (Dis->Type == ThrowingObjectType.Freezie) {
                        Dis->HitSomething = true;
                        f.Unsafe.GetPointer<IceBlock>(IceBlockSystem.Freeze(f, marioEntity))->AutoBreakFrames = 360;
                    } else {
                        if (mario->DoKnockback(f, marioEntity, hitRight, Dis->StarsToDrop, /*TeamateItem*/ Dis->StarsToDrop > 2 ? KnockbackStrength.Groundpound : KnockbackStrength.FireballBump, thisEntity)) {
                            f.Events.PlayKnockbackEffect(marioEntity, thisEntity, KnockbackStrength.FireballBump,
                                (f.Unsafe.GetPointer<Transform2D>(marioEntity)->Position + f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position) / 2);
                        }
                    }
                }
                return false;
            }
            Dis->HitSomething = false;
            //Try To Pickup
            if ((!Dis->Thrown || IsOwned)
              && (damageDirection.Y <= Constants._0_66 || Dis->HitSomething)) {
                //Only Allow Carry If No Team Or Same Team --- TOTEST
                if (!HazardSystem.IsCanInteractWithTeamHazard(f, marioEntity, thisEntity, Dis->IgnoreTeamates)) { //Can only pickup if it's on our team... or no team
                    return true;
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
                        marioPhysicsObject->Velocity.X = 0;
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
                    case ThrowingObjectType.CannonBox:

                        Dis->ReusableTimer = FP._0_50;
                        Dis->Varient = 3;
                        break;
                    }
                    return false;
                }
            }
            return true;
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

        #region Typical Enemy/projectile/self interactions
        public static void OnThrowingObjectGoombaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    IceBlockSystem.Freeze(f, otherEntity);
                } else {
                    goomba->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
                }
            }
        }
        public static void OnThrowingObjectKoopaInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var koopa = f.Unsafe.GetPointer<Koopa>(otherEntity);

            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    IceBlockSystem.Freeze(f, otherEntity);
                } else {
                    koopa->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
                }
            }
        }
        public static void OnThrowingObjectBobombInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    IceBlockSystem.Freeze(f, otherEntity);
                } else {
                    bobomb->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
                }
            }
        }
        public static void OnThrowingObjectBulletBillInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var bill = f.Unsafe.GetPointer<BulletBill>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    IceBlockSystem.Freeze(f, otherEntity);
                } else {
                    bill->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
                }
            }
        }
        public static void OnThrowingObjectPiranhaPlantInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var plant = f.Unsafe.GetPointer<PiranhaPlant>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    IceBlockSystem.Freeze(f, otherEntity);
                } else {
                    plant->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
                }
            }
        }
        public static void OnThrowingObjectBooInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var boo = f.Unsafe.GetPointer<Boo>(otherEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (Dis->Thrown || beingHeld) {
                // Destroy them
                boo->Kill(f, otherEntity, thisEntity, EnemyKillReason.Special);
            }
        }
        public static void OnThrowingObjectIceBlockInteraction(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var ice = f.Unsafe.GetPointer<IceBlock>(otherEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            bool beingHeld = f.Exists(holdable->Holder);

            if ((Dis->Thrown || beingHeld) && Dis->Type != ThrowingObjectType.Freezie) {
                // Destroy them
                IceBlockSystem.Destroy(f, otherEntity, IceBlockBreakReason.Other, holdable->PreviousHolder);
            }
        }
        public static void OnThrowingObjectIceBlockInteractionStationary(Frame f, EntityRef thisEntity, EntityRef otherEntity) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var ice = f.Unsafe.GetPointer<IceBlock>(otherEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            bool beingHeld = f.Exists(holdable->Holder);

            if ((Dis->Thrown || beingHeld) && Dis->Type != ThrowingObjectType.Freezie) {
                // Destroy them
                IceBlockSystem.Destroy(f, otherEntity, IceBlockBreakReason.Other, holdable->PreviousHolder);
            }
        }
        public static void OnThrowingObjectBossInteraction(Frame f, EntityRef thisEntity, EntityRef bossEntity) {
            OnBossInteraction(f, thisEntity, bossEntity);
        }
        public static bool OnBossInteraction(Frame f, EntityRef thisEntity, EntityRef bossEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->iframes > 0 || boss->Dead)
                return false;

            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            if (holdable->PreviousHolder == bossEntity || holdable->PreviousHolder == boss->ControllerPlayer) {
                return false; //hang on, this is OUR throwable!
            }

            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            if (Dis->Thrown && Dis->StarsToDrop != 0) {
                if (Dis->Type == ThrowingObjectType.Freezie) {
                    Dis->HitSomething = true;
                    f.Unsafe.GetPointer<IceBlock>(IceBlockSystem.Freeze(f, bossEntity))->AutoBreakFrames = 90;
                } else {
                    var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
                    Dis->HitSomething = true;
                    Dis->Thrown = false;
                    physicsObject->Velocity = new FPVector2(Dis->Facing ? -3 : 3, 4);
                    physicsObject->IsTouchingGround = false;
                    if (Dis->GroundBounce)
                        Dis->BounceTimes = 1;

                    f.Events.PlayKnockbackEffect(bossEntity, thisEntity, KnockbackStrength.FireballBump,
                        (f.Unsafe.GetPointer<Transform2D>(bossEntity)->Position + f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position) / 2);

                    var strength = Dis->StarsToDrop == 3 ? KnockbackStrength.Groundpound : 
                        Dis->StarsToDrop == 1 ? KnockbackStrength.FireballBump : 
                        KnockbackStrength.Normal;

                    //kingbooblocks gets special treatment C: , he takes extra damage if a player thrown his block
                    if (f.Has<KingBoo>(bossEntity) && !f.Has<Boss>(holdable->PreviousHolder)) {
                        strength = KnockbackStrength.Normal;
                        f.Events.KingBooKnockbacked(bossEntity);
                    }
                    //Damage Boss
                    boss->BossHarmed(f, bossEntity, Dis->Facing, strength, Dis->StarsToDrop > 1);
                    f.Events.PlayBossHitSound(bossEntity);
                }
            }
            return true;
        }

        public static bool OnThrowingObjectProjectileInteraction(Frame f, EntityRef projectileEntity, EntityRef thisEntity, PhysicsContact contact) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            /*if (f.Exists(holdable->Holder) && Dis->Type != ThrowingObjectType.Stone) {
                //Force The player To Drop This Item if it's hit While They Are Carrying It
                holdable->DropWithoutThrowing(f, thisEntity);
                f.Events.PlayComboSound(thisEntity, 0);
                f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
            }*/
            if (Dis->Type == ThrowingObjectType.Stone || Dis->Type == ThrowingObjectType.Barrel) {
                f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
                return true;
            }
            return false;
        }
        public static bool OnThrowingObjectThrowingObjectInteraction(Frame f, EntityRef otherEntity, EntityRef thisEntity, PhysicsContact contact) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            //var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var otherDis = f.Unsafe.GetPointer<ThrowingObject>(otherEntity);
            if (otherDis->Type == ThrowingObjectType.KingBooStone) {
                HazardSystem.DestroyHazard(f, otherEntity);
            }
            //if (Dis->Type == ThrowingObjectType.KingBooStone) {
            //    HazardSystem.DestroyHazard(f, thisEntity);
            //}
            return false;
        }
        #endregion

        #region Springboard interactions
        public static bool OnSpringboardSolidInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, PhysicsContact contact) {
            return HandleSpringboardInteraction(f, anyEntity, thisEntity, true);
        }
        public static void OnSpringboardAnythingInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity) {
            HandleSpringboardInteraction(f, anyEntity, thisEntity, false);
        }
        public static bool HandleSpringboardInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, bool FromSolid) {
            var Dis = f.Unsafe.GetPointer<ThrowingObject>(thisEntity);
            var phys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(anyEntity);

            //if (f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled) {
            //    return false;
            //}
            //fix for lemmyball interaction bug
            if (f.Has<LemmyBall>(anyEntity))
                LemmyBallSystem.TryLemmyBallPush(f, anyEntity, thisEntity, true);

            //Can't Interact
            if (Dis->ReusableTimer > 0 || otherPhys->Velocity.Y >= 0 || f.Has<Projectile>(anyEntity)) {
                return false;
            }

            var otherTransform = f.Unsafe.GetPointer<Transform2D>(anyEntity)->Position; var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), otherTransform, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (damageDirection.Y > FP._0_75) {
                Dis->ConnectedObject = anyEntity;
                phys->IsFrozen = true;
                otherPhys->Velocity.Y = -1;

                var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
                var coinitem = f.Unsafe.GetPointer<CoinItem>(thisEntity);
                var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);

                if (holdable->Holder != EntityRef.None && hazard->LifeTime < hazard->BaseLifeTime && (hazard->IsHazard || hazard->IsCoinItem)) {
                    hazard->LifeTime = hazard->BaseLifeTime;
                }
                f.Events.ThrowObjSimple(thisEntity, ourPos);
            }
            return true;
        }
        #endregion
        #endregion

        #region Signals
        public void OnPreContactCallback(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContact) {
            if (contact.Entity != EntityRef.None
                && f.Unsafe.TryGetPointer(contact.Entity, out ThrowingObject* throwable) && throwable->Type == ThrowingObjectType.ChainPost
                && (entity == throwable->ConnectedObject)) {

                keepContact = false;
            }
        }
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
            physicsObject->Velocity.X = Dis->Thrown ? ((Dis->ThrowForce + bonusSpeed) * (mario->FacingRight ? 1 : -1)) : (mario->FacingRight ? 3 : -3);
            physicsObject->Velocity.Y = Dis->Thrown ? 2 : -1;
            holdable->IgnoreOwnerFrames = 20; //15

            f.Unsafe.GetPointer<PhysicsCollider2D>(entity)->Enabled = true;
            //f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
            physicsObject->DisableCollision = false;

            // Disable Carryabilites
            switch (Dis->Type) {
            case ThrowingObjectType.Stone: {
                Dis->Thrown = !crouching;
                marioPhysicsObject->Velocity.X = FPMath.Clamp(marioPhysicsObject->Velocity.X, -1, 1);
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
                Dis->ReusableTimer = 90;
                break;
            case ThrowingObjectType.KingBooStone:
                physicsObject->DisableCollision = true;
                break;
            case ThrowingObjectType.CannonBox:
                Dis->Varient = 0;
                Dis->ReusableTimer = CannonBoxChargeLimit;
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
                if (fromBelow)
                    Dis->Thrown = Dis->HitSomething = true;
                break;
            case ThrowingObjectType.Freezie:
                // Break This
                f.Events.ThrowObjSimple(entity, transform->Position);
                HazardSystem.DestroyHazard(f, entity);
                break;
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out ThrowingObject* Dis)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsCollider2D* collider)) {
                return;
            }

            var specialValues = f.ResolveList(spawnData);

            switch (Dis->Type) {
            case ThrowingObjectType.Basic:
            case ThrowingObjectType.Stone:
            case ThrowingObjectType.Spring:
                break;
            case ThrowingObjectType.Pow:
                Dis->Varient = (byte) (specialValues[0] == 1 ? 1 : 0);
                if (specialValues[1] == 1) {
                    Dis->Thrown = true;
                    hazard->IPWSTime = 1;
                } else if (specialValues[1] == 2) {
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
            case ThrowingObjectType.Potion:
                break;
            case ThrowingObjectType.Voidwall:
                if (specialValues[0] == 1) {
                    Dis->Thrown = true;
                    Dis->IsFlying = false;
                    hazard->IPWSTime = 1;
                }
                //Dis->Varient = (byte)index;
                break;
            case ThrowingObjectType.ChainPost:
                if (spawnReason == SpawnReason.WasCreatedFromNested) {
                    Debug.Log("Halt, Too Much Nested Objects");
                    break;
                }

                Dis->Varient = (byte) (specialValues[0] == 1 ? 1 : 0);
                if (specialValues[1] != 255) {
                    //spawn from hazard List
                    HazardManagerSystem.CreateHazardFromReference(f, specialValues[1], out var newEntity, out var newSpawndata);
                    Dis->ConnectedObject = newEntity;
                    f.Signals.InitializeHazard(Dis->ConnectedObject, EntityRef.None, spawnpoint, spawnReason == SpawnReason.Forced ? SpawnReason.WasCreatedFromNested : (spawnReason == SpawnReason.Fridge ? SpawnReason.Forced : SpawnReason.Fridge), newSpawndata.Extra);
                } else {
                    //Default To Chain Chomp
                    Dis->ConnectedObject = f.Create(f.SimulationConfig.Chainchomp);
                    f.Signals.InitializeHazard(Dis->ConnectedObject, thisEntity, spawnpoint, SpawnReason.Forced, spawnData);
                }
                break;
            }

            if (Dis->IsFlying) {
                hazard->IPWSUntilGround = false;
            }
        }
        #endregion
    }
}
