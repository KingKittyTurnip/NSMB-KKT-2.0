using Photon.Deterministic;
using Quantum.Collections;
using static BreakableBrickTile;
using static IInteractableTile;

namespace Quantum {
    
    public unsafe class StarballSystem : SystemMainThreadFilterStage<StarballSystem.Filter>, ISignalInitializeHazard {
        private static readonly FP SlopeBonus = FP.FromString("0.15");
        public struct Filter {
            public EntityRef Entity;
            public Starball* Starball;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Starball>(f, OnStarballMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var starball = filter.Starball;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            bool Deccel = true;
            // Despawn off bottom of stage
            if (filter.Transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y || physicsObject->IsUnderwater) {
                physicsObject->IsFrozen = true;
                if (starball->Rider != EntityRef.None) {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(starball->Rider);
                    mario->RidingStarball = false;
                    mario->DoEntityBounce = true;
                }

                f.Destroy(filter.Entity);
                return;
            }
            QuantumUtils.Decrement(ref starball->JumpBufferFrames);
            QuantumUtils.Decrement(ref starball->CoyoteTimeFrames);

            //Rider Logic
            if (starball->Rider != EntityRef.None) {
                var marphys = f.Unsafe.GetPointer<PhysicsObject>(starball->Rider);
                var mario = f.Unsafe.GetPointer<MarioPlayer>(starball->Rider);
                if (mario->CurrentKnockback == KnockbackStrength.None && mario->RidingStarball && mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                    //(Make Controlled By The Mouse)
                    Input inputs = *f.GetPlayerInput(mario->PlayerRef);
                    if (inputs.Jump.WasPressed) { // Jump buffer
                        starball->JumpBufferFrames = 12;
                    }
                    if (physicsObject->IsTouchingGround) { // Coyote Time
                        starball->CoyoteTimeFrames = 5;
                    }
                    //Left/Right
                    if ((inputs.Left.IsDown || inputs.Right.IsDown) && !mario->IsCrouching) {
                        physicsObject->Velocity.X += (inputs.Left.IsDown ? -1 : 1) * (FPMath.Abs(physicsObject->Velocity.X) > 3 ? FPMath.Abs(physicsObject->Velocity.X) > 6 ? FP._0_03 : FP._0_10 : Constants._0_1875) * (mario->FacingRight == physicsObject->Velocity.X > 0 ? 1 : 2); //Move
                        Deccel = false;
                    }

                    //Transfer Collision Logic
                    if (marphys->IsTouchingCeiling) {
                        physicsObject->IsTouchingCeiling = true;
                    } else {
                        if (marphys->IsTouchingLeftWall)
                            physicsObject->IsTouchingLeftWall = true;
                        if (marphys->IsTouchingRightWall)
                            physicsObject->IsTouchingRightWall = true;
                    }

                    //Jump
                    if (starball->JumpBufferFrames > 0 && starball->CoyoteTimeFrames > 0 && physicsObject->WasTouchingGround) {
                        physicsObject->Velocity.Y = 10 + (physicsObject->Velocity.X * FP._0_10);
                        physicsObject->IsTouchingGround = false;
                        starball->CoyoteTimeFrames = 0;
                        starball->JumpBufferFrames = 0;
                    }
                    if (inputs.Jump.IsDown && physicsObject->Velocity.Y >= -1) {
                        physicsObject->Gravity.Y = -20;
                    } else {
                        physicsObject->Gravity.Y = -31;
                    }

                    //Misc Actions
                    f.Unsafe.GetPointer<Transform2D>(starball->Rider)->Position = filter.Transform->Position + new FPVector2(0, FP._0_50 + (starball->CoyoteTimeFrames == 0 ? FP._0_20 : 0));
                    marphys->Velocity = physicsObject->Velocity;
                    mario->IsSkidding = false;
                } else {
                    starball->Rider = EntityRef.None;
                    mario->RidingStarball = false;
                }

                //Create A Goal if One Doesn't Exist
                var Objects = f.Filter<Starballgoal>();
                bool i = true;
                while (Objects.NextUnsafe(out EntityRef OtherEntity, out Starballgoal* starballgoal)) {
                    i = false;
                    break;
                }
                if (i) {
                    EntityRef newStarEntity = f.Create(starball->CurrentGoal);
                    //var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                    var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
                    newStarTransform->Position = filter.Transform->Position + new FPVector2(1, -FP._0_50);
                }
            } else {
                physicsObject->Gravity.Y = -31;
            }

            //Physics
            if (physicsObject->IsTouchingGround) {
                if (physicsObject->FloorAngle != 0) {
                    if (!physicsObject->WasTouchingGround) {
                        physicsObject->Velocity.X = FPMath.Clamp(physicsObject->FloorAngle, -8, 8);
                    }
                    physicsObject->Velocity.X -= (Constants.WeirdSlopeConstant * physicsObject->FloorAngle) * SlopeBonus;
                    Deccel = false;
                }
                if (!physicsObject->WasTouchingGround) {
                    f.Events.StarBallLand(f, filter.Entity, physicsObject->FloorAngle != 0);
                }
            } else {
                Deccel = false;
            }
            TouchedBricks(f, filter.Entity, stage);
            physicsObject->BreakMegaObjects = FPMath.Abs(physicsObject->Velocity.X) > 6;
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                physicsObject->Velocity.X = FPMath.Abs(physicsObject->Velocity.X) * (physicsObject->IsTouchingLeftWall ? 1 : -1) * FP._0_75;
            }
            if (physicsObject->IsTouchingCeiling) {
                physicsObject->Velocity.Y = 0;
            }
            if (Deccel)
                physicsObject->Velocity.X *= Constants._0_95;
            physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X, -10, 10);
        }

        private bool TouchedBricks(Frame f, EntityRef Starball, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(Starball);
            var transform = f.Unsafe.GetPointer<Transform2D>(Starball);

            bool BrickBroken = false;
            //Tile Check
            QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
            foreach (var contact in contacts) {
                if (f.Exists(contact.Entity)) {
                    if (f.Has<BreakableObject>(contact.Entity)) {
                        BrickBroken = true;
                    }
                    continue;
                }

                FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                bool right = dot < 0;

                // Floor tiles.
                var tileInstance = stage.GetTileRelative(f, contact.Tile);
                StageTile tile = f.FindAsset(tileInstance.Tile);
                if (tile is IInteractableTile it && contact.Tile.Y > transform->Position.Y - FP._0_50) {
                    it.Interact(f, Starball, InteractionDirection.Up,
                       new IntVector2(contact.Tile.X, contact.Tile.Y), tileInstance, out bool tempPlayBumpSound);

                    //If The Thing in Front Is Breakable By Bombs Or Shells, Push Through (do note he can break mega breakabled he will just be bumped)
                    if (!((tile is BreakableBrickTile uh && !uh.BreakingRules.HasFlag(BreakableBy.Shells) && !uh.BreakingRules.HasFlag(BreakableBy.Bombs))
                        || (tile is CoinTile uhh && !uhh.BreakingRules.HasFlag(BreakableBy.Shells) && !uhh.BreakingRules.HasFlag(BreakableBy.Bombs))
                        || (tile is PowerupTileBase uhhh && !uhhh.BreakingRules.HasFlag(BreakableBy.Shells) && !uhhh.BreakingRules.HasFlag(BreakableBy.Bombs))))
                        BrickBroken = true;
                }
            }

            return BrickBroken;
        }

        public static void BreakOpenStarball(Frame f, EntityRef starballEntity, EntityRef starballgoalEntity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(starballEntity);
            var starball = f.Unsafe.GetPointer<Starball>(starballEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(starballEntity);
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            int starDirection = 0;
            physicsObject->IsFrozen = true;
            if (starball->Rider != EntityRef.None) {
                var mario = f.Unsafe.GetPointer<MarioPlayer>(starball->Rider);
                mario->RidingStarball = false;
                starDirection = !f.Unsafe.GetPointer<MarioPlayer>(starball->Rider)->FacingRight ? 1 : 2;
            } else {
                starDirection = FPMath.CeilToInt(f.RNG->Next() * 2);
            }

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
            EntityRef newStarEntity = f.Create(starball->Contains);
            var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
            var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
            newStarTransform->Position = transform->Position;
            newStar->InitializeMovingStar(f, stage, newStarEntity, starDirection);

            f.Events.StarBallDestroyed(starballEntity, starballgoalEntity);

            /*if (droppedStars > 0) {
                f.Events.MarioPlayerDroppedStar(entity);
                GameLogicSystem.CheckForGameEnd(f);
            }*/

            f.Destroy(starballEntity);
        }

        #region Interactions
        public static void OnStarballMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var starball = f.Unsafe.GetPointer<Starball>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (starball->Rider == marioEntity) {
                //Wait, This is OUR Mario!
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            //Try Make Rider
            if (starball->Rider == EntityRef.None && FPVector2.Dot(damageDirection, FPVector2.Down) > FP._0_25 && mario->KnockbackGetupFrames <= 0 && mario->CurrentKnockback == KnockbackStrength.None) {
                starball->Rider = marioEntity;
                mario->RidingStarball = true;
                return;
            }

            //Try Bonk Other Players
            if (attackFromAbove) {
                physicsObject->Velocity.Y = 7;
            } else {
                physicsObject->Velocity.X = (damageDirection.X < 0 ? -3 : 3);
            }
            if (mario->IsDamageable) {
                mario->DoKnockback(f, marioEntity, damageDirection.X >= 0, 1, attackFromAbove ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, thisEntity, false);
            }

        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set Container
        }
        #endregion
    }
}
