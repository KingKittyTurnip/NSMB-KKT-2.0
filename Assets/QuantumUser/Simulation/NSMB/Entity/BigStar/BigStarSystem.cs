using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;
using UnityEngine;

namespace Quantum {
    public unsafe class BigStarSystem : SystemMainThread, ISignalOnMarioPlayerDropObjective {

        public override bool StartEnabled => false;

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<BigStar, MarioPlayer>(f, OnBigStarMarioInteraction);
            f.Context.Interactions.Register<BigStar, Boss>(f, OnBigStarBossInteraction);
            f.Context.Interactions.Register<BigStar, Monty>(f, OnBigStarMontyInteraction);
        }

        public override void Update(Frame f) {
            VersusStageData stage = null;

            if (!f.Exists(f.Global->MainBigStar) && QuantumUtils.Decrement(ref f.Global->BigStarSpawnTimer)) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                HandleSpawningNewStar(f, stage);
            }

            var allStars = f.Filter<BigStar>();
            while (allStars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                HandleStar(f, ref stage, entity, bigStar);
            }
        }

        private void HandleSpawningNewStar(Frame f, VersusStageData stage) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            EntityRef newEntity = TrySpawnObjective(f, ((StarChasersGamemode) gamemode).BigStarPrototype, stage);

            if (newEntity != EntityRef.None) {
                var newStar = f.Unsafe.GetPointer<BigStar>(newEntity);
                var newStarPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newEntity);
                newStar->IsStationary = true;
                newStarPhysicsObject->DisableCollision = true;
            }
        }

        private void HandleStar(Frame f, ref VersusStageData stage, EntityRef entity, BigStar* bigStar) {
            if (entity == f.Global->MainBigStar) { //bigStar->IsStationary ?? might break something
                return;
            }

            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            if (QuantumUtils.Decrement(ref bigStar->Lifetime)) {
                // Timer despawn
                f.Events.CollectableDespawned(entity, transform->Position, false);
                var hazard = f.Unsafe.GetPointer<Hazard>(entity);
                if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                    f.Global->UsedHazardSpawns.Clear(hazard->index);
                    f.Global->UsedHazardSpawnCount--;
                }
                f.Destroy(entity);
                return;
            }

            if (bigStar->IsStationary) {
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (transform->Position.Y < stage.StageWorldMin.Y && bigStar->UncollectableFrames == 0 && physicsObject->Velocity.Y <= 0) {
                // Below world
                if (physicsObject->DisableCollision) {
                    // Bounce
                    physicsObject->Velocity.Y = Constants._8_50 + 3;
                    physicsObject->IsTouchingGround = false;
                } else {
                    // Despawn
                    f.Events.CollectableDespawned(entity, transform->Position, false);
                    f.Destroy(entity);
                    return;
                }
            }


            if (physicsObject->IsTouchingGround) {
                physicsObject->Velocity.X = bigStar->Speed * (bigStar->FacingRight ? 1 : -1);
                physicsObject->Velocity.Y = bigStar->BounceForce;
                physicsObject->IsTouchingGround = false;
            }

            if (!stage.IsWrappingLevel) {
                var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
                ref Shape2D shape = ref physicsCollider->Shape;
                if (transform->Position.X - shape.Centroid.X - shape.Box.Extents.X <= stage.StageWorldMin.X) {
                    // Hit left wall
                    bigStar->FacingRight = true;
                } else if (transform->Position.X + shape.Centroid.X + shape.Box.Extents.X >= stage.StageWorldMax.X) {
                    // Hit right wall
                    bigStar->FacingRight = false;
                }
            }

            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                bigStar->FacingRight = physicsObject->IsTouchingLeftWall;
            }

            if (physicsObject->DisableCollision
                && QuantumUtils.Decrement(ref bigStar->UncollectableFrames)
                && transform->Position.Y < FPMath.Max(stage.StageWorldMin.Y + 7, stage.StageWorldMax.Y)) {

                var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
                if (!PhysicsObjectSystem.BoxInGround(f, transform->Position, physicsCollider->Shape, stage: stage)) {
                    physicsObject->DisableCollision = false;
                    //physicsObject->SlowInLiquids = true; //NO.
                }
            }
            if (FPMath.Abs(physicsObject->Velocity.X) < bigStar->Speed) {
                physicsObject->Velocity.X = bigStar->Speed * (bigStar->FacingRight ? 1 : -1);
            }
        }

        public void OnBigStarMarioInteraction(Frame f, EntityRef starEntity, EntityRef marioEntity) {
            if (!f.Exists(starEntity) || f.DestroyPending(starEntity)) {
                return;
            }

            var star = f.Unsafe.GetPointer<BigStar>(starEntity);
            if (star->UncollectableFrames > 0) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->IsDead) {
                return;
            }

            mario->GamemodeData.StarChasers->Stars++;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            if (starEntity == f.Global->MainBigStar) { //bigStar->IsStationary ?? might break something
                stage.ResetStage(f, false);
                f.Global->BigStarSpawnTimer = (ushort) FPMath.RoundToInt(((FP) (624 - (f.Global->RealPlayers * 12)) * ((FP)f.Global->Rules.StarFrequency/(FP)100)));
            }

            f.Signals.OnMarioPlayerCollectedStar(marioEntity);
            GameLogicSystem.CheckForGameEnd(f);

            f.Events.MarioPlayerCollectedStar(marioEntity, f.Unsafe.GetPointer<Transform2D>(starEntity)->Position, starEntity);
            f.Events.CollectableDespawned(starEntity, f.Unsafe.GetPointer<Transform2D>(starEntity)->Position, true);

            var hazard = f.Unsafe.GetPointer<Hazard>(starEntity);
            if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                f.Global->UsedHazardSpawns.Clear(hazard->index);
                f.Global->UsedHazardSpawnCount--;
            }
            f.Destroy(starEntity);
        }

        public void OnBigStarBossInteraction(Frame f, EntityRef starEntity, EntityRef bossEntity) {
            var boss = f.Unsafe.GetPointer<Boss>(bossEntity);
            if (boss->ControllerPlayer == EntityRef.None)
                return;
            /*if (f.Unsafe.TryGetPointer<Tank>(bossEntity, out var tank)) {
                //do nothing, we are tank.
                var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);

                QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                f.Unsafe.GetPointer<BigStar>
                    
            } else*/
                OnBigStarMarioInteraction(f, starEntity, boss->ControllerPlayer);
        }
        public void OnBigStarMontyInteraction(Frame f, EntityRef starEntity, EntityRef montyEntity) {
            var monty = f.Unsafe.GetPointer<Monty>(montyEntity);
            var boss = f.Unsafe.GetPointer<Boss>(monty->OwnerEntity);
            if (boss->ControllerPlayer == EntityRef.None)
                return;
            OnBigStarMarioInteraction(f, starEntity, boss->ControllerPlayer);
        }

        /*public void OnReturnToRoom(Frame f) {
            f.Global->MainBigStar = EntityRef.None;
            f.Global->BigStarSpawnTimer = 0;
            f.Global->UsedStarSpawns.ClearAll();
        }*/

        public void OnMarioPlayerDropObjective(Frame f, EntityRef entity, int amount, EntityRef attacker) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                if (mario->CurrentPowerupState == PowerupState.Doneflower) {//double damage
                    amount = amount * 2;
                    if (amount > 4)
                        amount = 4;
                }
                SpawnStarsFromPlayer(f, entity, mario, amount);
            }
        }

        public static EntityRef TrySpawnObjective(Frame f, AssetRef<EntityPrototype> prototype, VersusStageData stage) {
            var spawnpoints = stage.BigStarSpawnpoints;
            int numSpawnpoints = spawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;

            for (int i = 0; i < numSpawnpoints; i++) {
                // Find a spot...
                int bitsSet = usedSpawnpoints.GetSetCount();
                if (bitsSet >= numSpawnpoints) {
                    usedSpawnpoints.ClearAll();
                    bitsSet = 0;
                }

                int selection = f.RNG->Next(0, numSpawnpoints - bitsSet);
                int count = selection;
                int index = 0;
                for (int j = 0; j < numSpawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                usedSpawnpoints.Set(index);

                // Spawn a star.
                Shape2D shape = f.Context.CircleRadiusTwo;
                FPVector2 position = spawnpoints[index];

                HitCollection hits = f.Physics2D.OverlapShape(position, 0, shape, f.Context.PlayerOnlyMask);
                if (hits.Count > 0) {
                    // Hit something.
                    f.Events.BigCollectableAttemptedSpawn(index, position, Success: false);
                    continue;
                }

                FP radius = shape.BroadRadius + 1;
                if (position.X - radius <= stage.StageWorldMin.X || position.X + radius >= stage.StageWorldMax.X) {
                    // Close to the level seam... check for extra overlaps.
                    FPVector2 seamPosition = position;
                    seamPosition.X += stage.TileDimensions.X * FP._0_50 * ((position.X < stage.StageWorldMidpoint.X) ? 1 : -1);

                    HitCollection seamHits = f.Physics2D.OverlapShape(seamPosition, 0, shape, f.Context.PlayerOnlyMask);
                    if (seamHits.Count > 0) {
                        // Hit something.
                        f.Events.BigCollectableAttemptedSpawn(index, position, Success: false);
                        continue;
                    }
                }

                // Good to go.
                EntityRef newEntity = f.Create(prototype);
                f.Global->MainBigStar = newEntity;
                f.Unsafe.GetPointer<Transform2D>(newEntity)->Position = position;
                f.Events.BigCollectableAttemptedSpawn(index, position, Success: true);
                return newEntity;
            }

            // Failed.
            f.Global->BigStarSpawnTimer = 30;
            return EntityRef.None;
        }

        private static void SpawnStarsFromPlayer(Frame f, EntityRef marioEntity, MarioPlayer* mario, int amount) {
            //var starChasersData = mario->GamemodeData.StarChasers;

            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Unsafe.GetPointer<Transform2D>(marioEntity);

            bool fastStars = amount > 2 && mario->GamemodeData.StarChasers->Stars > 2;
            int starDirection = mario->FacingRight ? 1 : 2;

            //if (f.Global->Rules.ModifierLivesEnabled && mario->Lives == 0) {
            if (!mario->IsValid(f)) {
                fastStars = true;
                mario->NoLivesStarDirection = (byte) ((mario->NoLivesStarDirection + 1) % 4);
                starDirection = mario->NoLivesStarDirection;

                starDirection = starDirection switch {
                    2 => 1,
                    1 => 2,
                    _ => starDirection
                };
            }

            int droppedStars = 0;
            var gamemode = (StarChasersGamemode) f.FindAsset(f.Global->Rules.Gamemode);
            while (amount > 0) {
                if (mario->GamemodeData.StarChasers->Stars <= 0) {
                    break;
                }

                int actualStarDirection = starDirection % 4;
                if (!fastStars) {
                    actualStarDirection = starDirection switch {
                        0 => 2,
                        3 => 1,
                        _ => starDirection
                    };
                }

                EntityRef newStarEntity = f.Create(gamemode.BigStarPrototype);
                var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
                newStarTransform->Position = transform->Position;
                newStar->InitializeMovingStar(f, stage, newStarEntity, actualStarDirection);

                mario->GamemodeData.StarChasers->Stars--;
                amount--;
                droppedStars++;
                starDirection++;
            }

            if (droppedStars > 0) {
                f.Events.MarioPlayerDroppedStar(marioEntity);
                GameLogicSystem.CheckForGameEnd(f);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out BigStar* bigstar)) {
                return;
            }

            var specialValues = f.ResolveList(spawnData);

            //Set Sturdy
            bigstar->IsStationary = specialValues[0] == 1;
            f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->DisableCollision = bigstar->IsStationary;
            if (!bigstar->IsStationary) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                bigstar->InitializeMovingStar(f, stage, thisEntity, (f.RNG->Next() >= FP._0_50 ? 1 : 2));
                hazard->RestrictSpawnPosition = false;
                //f.Global->UsedHazardSpawns.Clear(hazard->index);
                //f.Global->UsedHazardSpawnCount--;
            }
        }
    }
}
