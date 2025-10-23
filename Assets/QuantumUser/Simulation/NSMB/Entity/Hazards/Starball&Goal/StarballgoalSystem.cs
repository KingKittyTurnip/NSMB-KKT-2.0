using Photon.Deterministic;
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using UnityEditor;
using UnityEngine;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class StarballgoalSystem : SystemMainThreadFilterStage<StarballgoalSystem.Filter> {
        private static readonly FP HoverArea = FP.FromString("0.53");
        public struct Filter {
            public EntityRef Entity;
            public Starballgoal* Starballgoal;
            public Transform2D* Transform;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Starballgoal, Starball>(f, OnStarballGoalInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var starballgoal = filter.Starballgoal;
            
            if (starballgoal->CaughtStarBall != EntityRef.None) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(starballgoal->CaughtStarBall);
                var ballTransform = f.Unsafe.GetPointer<Transform2D>(starballgoal->CaughtStarBall); var DisTransform = f.Unsafe.GetPointer<Transform2D>(filter.Entity);
                QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, ballTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                physicsObject->Velocity.X += (theirPos - ourPos).Normalized.X > 0 ? -1 : 1;
                physicsObject->Velocity.X *= Constants._0_90;
                physicsObject->Velocity.Y = 0;
                ballTransform->Position.Y += ((DisTransform->Position.Y + HoverArea) - ballTransform->Position.Y) * FP._0_50;
                starballgoal->DespawnTimer++;
                if (starballgoal->DespawnTimer > 60) {
                    StarballSystem.BreakOpenStarball(f, starballgoal->CaughtStarBall, filter.Entity);
                    starballgoal->DespawnTimer = 122;
                    f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                    f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity)->Enabled = false;
                    starballgoal->CaughtStarBall = EntityRef.None;
                }
                return;
            }

            starballgoal->DespawnTimer++;
            if (starballgoal->DespawnTimer > 121) {
                if (starballgoal->DespawnTimer == 122) {
                    f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                    f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity)->Enabled = false;
                    f.Events.StarBallDestroyed(EntityRef.None, filter.Entity);
                }
                if (starballgoal->DespawnTimer > 161)
                    f.Destroy(filter.Entity);
            } else {
                var Objects = f.Filter<Starball>();
                while (Objects.NextUnsafe(out EntityRef OtherEntity, out Starball* starball)) {
                    if (starball->Rider != EntityRef.None) {
                        starballgoal->DespawnTimer = 0;
                    }
                }
            }
        }

        public static void TryCreateStarballGoal(Frame f, FPVector2 newPos, VersusStageData stage) {
            EntityRef newEntity = f.Create(f.SimulationConfig.StarballGoal);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(newEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(newEntity);

            #region Loop&RoundHalfTheStagePos
            //make "X" halfway in the stage
            FP width = stage.TileDimensions.X * FP._0_50;
            newPos.X += width / 2;
            if (newPos.X > stage.StageWorldMax.X) {
                newPos.X -= width;
            }
            //make "Y" halfway in the stage (used for volcano maps)
            width = stage.CameraMaxPosition.Y - stage.CameraMinPosition.Y;
            newPos.Y += width / 2;
            if (newPos.Y > stage.CameraMaxPosition.Y) {
                newPos.Y -= width;
            }
            //round
            newPos = new FPVector2(FPMath.RoundToInt(newPos.X * 2) / 2, FPMath.RoundToInt(newPos.Y * 2) / 2);
            #endregion

            Span<PhysicsObjectSystem.LocationTilePair> tiles = stackalloc PhysicsObjectSystem.LocationTilePair[64];
            int attempts = 0, Xattempts = 0;
            FPVector2 Checkbonus = FPVector2.Zero;
            bool upwardscheck = false;

            while (true) { //Place "Y" on the ground
                #region EndlessFailsafe
                attempts++;
                if (attempts > 255) {
                    //Check Took Too long
                    f.Destroy(newEntity);
                    Debug.Log("Failed To |Find A Air Tile| or |ground underneath valid position| For Starballgoal, x checks: " + Xattempts + " checkbonus: " + Checkbonus);
                    return;
                }
                #endregion
                #region AlreadyInsideTileCheck
                bool AlreadyInsideTile = false;
                int overlappingTiles = PhysicsObjectSystem.GetTilesOverlappingHitbox(f, newPos + Checkbonus, collider->Shape, tiles, stage);
                for (int i = 0; i < overlappingTiles; i++) {
                    StageTile stageTile = f.FindAsset(tiles[i].Tile.Tile);
                    if (stageTile != null) {
                        AlreadyInsideTile = true;
                    }
                }
                if (AlreadyInsideTile) {
                    Checkbonus.Y += upwardscheck ? FP._0_50 : -FP._0_50;
                    continue;
                }
                if (stage.CameraMinPosition.Y > newPos.Y + Checkbonus.Y) {
                    if (Checkbonus.Y < 0)
                        Checkbonus.Y = FP._0_50;
                    else
                        Checkbonus.Y += FP._0_50;
                    upwardscheck = true;
                    continue;
                } else if (stage.CameraMaxPosition.Y < newPos.Y + Checkbonus.Y) {
                    Checkbonus.Y = 0;
                    upwardscheck = false;
                    Xattempts++;
                    Checkbonus.X = GetXOffsetbonus(Xattempts);
                    continue;
                }
                #endregion
                #region CheckGroundBellow
                var contacted = PhysicsObjectSystem.Raycast(f, stage, newPos + Checkbonus + (FPVector2.Left / 4), FPVector2.Down, 10, out var point);
                var contactedr = PhysicsObjectSystem.Raycast(f, stage, newPos + Checkbonus + (FPVector2.Right / 4), FPVector2.Down, 10, out var R);
                if (contactedr) {
                    if (point.Position.Y < R.Position.Y) {
                        point = R;
                    }
                } else if (!contacted) {
                    //huh. above a pit, lets try some more
                    Xattempts++;
                    Checkbonus.X = GetXOffsetbonus(Xattempts);
                    continue;
                }
                #endregion
                //That Wasn't Too Hard Was it?
                newPos.X += Checkbonus.X;
                newPos.Y = point.Position.Y;
                break;
            }

            transform->Position = newPos;

            FP GetXOffsetbonus(int Xa) {
                //0=0 // 1=0.5 // 2=-0.5 // 3=1 // 4=-1
                return (((Xa % 2) * 2) -1) * FPMath.RoundToInt(Xa / 2) * FP._0_50;
            }
        }

        public static void OnStarballGoalInteraction(Frame f, EntityRef goalEntity, EntityRef starballEntity) {
            var starball = f.Unsafe.GetPointer<Starball>(starballEntity);
            var starballgoal = f.Unsafe.GetPointer<Starballgoal>(goalEntity);
            if (starball->Rider == EntityRef.None) {
                //Only Riders
                return;
            }

            var ballTransform = f.Unsafe.GetPointer<Transform2D>(starballEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(goalEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, ballTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_50) {
                starballgoal->CaughtStarBall = starballEntity;
                f.Unsafe.GetPointer<PhysicsCollider2D>(goalEntity)->Enabled = false;
                starballgoal->DespawnTimer = 0;
                f.Unsafe.GetPointer<Interactable>(goalEntity)->ColliderDisabled = true;
            }
        }
    }
}
