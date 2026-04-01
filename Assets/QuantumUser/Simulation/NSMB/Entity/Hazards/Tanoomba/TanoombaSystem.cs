using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using static LoopingMusicData;

namespace Quantum {
    
    public unsafe class TanoombaSystem : SystemMainThreadFilterStage<TanoombaSystem.Filter>, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard, ISignalOnEnemyRespawned, ISignalOnStageReset {

        //magic numbers
        FP playerCloseRange = 4;

        FP attackTimer = Constants._2_50;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Tanoomba* Tanoomba;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Tanoomba, MarioPlayer>(f, OnTanoombaMarioInteraction);
            f.Context.Interactions.Register<Tanoomba, Projectile>(f, OnTanoombaProjectileInteraction);
            f.Context.Interactions.Register<Tanoomba, Enemy>(f, OnTanoombaEnemyInteraction);
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var tanoomba = filter.Tanoomba;
            var enemy = filter.Enemy;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            var entity = filter.Entity;
            bool ComplexFrame = f.Number == FPMath.RoundToInt(f.Number / 10) * 10; // We use this to cut down on the MANY search calculations

            //Transform if We Somehow Fell into The Pit, Tho if We Are Dead Destroy Ourselves
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                if (enemy->IsDead) {
                    HazardSystem.DestroyHazard(f, filter.Entity);
                    return;
                } else if (tanoomba->State != TanoombaState.Searching) {
                    tanoomba->TanoombaResetTransform(f, filter.Entity, false);
                    tanoomba->SwitchState(f, entity, TanoombaState.Searching);
                }
            }
            if (enemy->IsDead) {
                return;
            }

            #region Base Tanoomba
            switch (tanoomba->State) {
            case TanoombaState.Idling:
                MoveAroundState(FP._0_10, physicsObject->IsTouchingGround ? 1 : 1 + FP._0_75);

                //Check For Player
                if (!FarFromPlayers(f, ref filter, transform->Position, playerCloseRange, true)) {
                    tanoomba->SwitchState(f, entity, TanoombaState.Shocked);
                }
                break;
            case TanoombaState.Searching: {
                if (ComplexFrame) {
                    var newForm = GetForm(f, ref filter, stage);
                    if (newForm != TanoombaFormState.Max) {
                        tanoomba->SwitchState(f, entity, TanoombaState.Transformed);
                        tanoomba->Form = newForm;
                        enemy->FacingRight = FormIsStationary(newForm) ? false : f.RNG->Next() > FP._0_50;
                    }
                }
                break;
            }
            case TanoombaState.Attacking: {
                if (physicsObject->IsTouchingGround) {
                    tanoomba->Invulnrable = tanoomba->ReusableTimer > attackTimer - FP._0_25; //small delay before becoming vulnrable
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(tanoomba->TargetedPlayer);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(tanoomba->TargetedPlayer);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                    enemy->FacingRight = damageDirection.X > 0;
                    MoveAroundState(FP._0_10, 3);
                }
                if ((physicsObject->IsTouchingGround || tanoomba->ReusableTimer < attackTimer) && QuantumUtils.Decrement(f, ref tanoomba->ReusableTimer)) {
                    tanoomba->SwitchState(f, entity, TanoombaState.Idling);
                }
                TryLaugh();
                break;
            }
            case TanoombaState.KnockedBack: {
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_20) {
                    physicsObject->Velocity.X = 0;
                    if (QuantumUtils.Decrement(f, ref tanoomba->ReusableTimer)) {
                        tanoomba->SwitchState(f, entity, TanoombaState.Searching);
                    }
                } else if (physicsObject->IsTouchingGround) {
                    physicsObject->Velocity.X += physicsObject->Velocity.X > 0 ? -FP._0_10 : FP._0_10;
                }
                if (tanoomba->ReusableTimer < FP._0_20) {
                    if (!tanoomba->Invulnrable) {
                        tanoomba->Invulnrable = true;
                        f.Events.TanoombaPoof(entity);
                    }
                } else if (physicsObject->Velocity.Y < 0) {
                    tanoomba->Invulnrable = false;
                }
                break;
            }
            case TanoombaState.Happy: {
                physicsObject->Velocity.X *= Constants._0_95;

                TryLaugh();

                if (QuantumUtils.Decrement(f, ref tanoomba->ReusableTimer) && FPMath.Abs(physicsObject->Velocity.X) < FP._0_10)
                    tanoomba->SwitchState(f, entity, TanoombaState.Idling);
                break;
            }
            case TanoombaState.Laughing:
            case TanoombaState.Shocked: {
                physicsObject->Velocity.X *= Constants._0_95;

                if (QuantumUtils.Decrement(f, ref tanoomba->ReusableTimer)) {
                    tanoomba->SwitchState(f, entity, tanoomba->State == TanoombaState.Shocked ? TanoombaState.Searching : TanoombaState.Idling);
                } else if (!tanoomba->Invulnrable && tanoomba->ReusableTimer < FP._0_20) {
                    tanoomba->Invulnrable = true;
                    f.Events.TanoombaPoof(entity);
                }
                break;
            }
            case TanoombaState.Transformed: {
                //TODO: vvv move here vvv
                break;
            }
            }
            #endregion
            if (tanoomba->State == TanoombaState.Transformed) {
                #region Transformed Tanoomba
                if (ComplexFrame) {
                    if (!tanoomba->PlayerPassedBy) {
                        if (!FarFromPlayers(f, ref filter, transform->Position, 6)) {
                            tanoomba->PlayerPassedBy = true;
                        }
                    } else {
                        if (FarFromPlayers(f, ref filter, transform->Position, 8)) {
                            tanoomba->PlayerPassedBy = false;
                            tanoomba->TanoombaResetTransform(f, filter.Entity, false);
                        }
                    }
                }

                switch (tanoomba->Form) {
                #region Level Tranforms
                case TanoombaFormState.Block: {
                    break;
                }
                case TanoombaFormState.Star: {
                    //check if bigstar is nearby us
                    int spawnpoints = stage.BigStarSpawnpoints.Length;
                    for (int i = 0; i < spawnpoints; i++) {
                        int count = f.RNG->Next(0, spawnpoints);
                        int index = 0;
                        for (int j = 0; j < spawnpoints; j++) {
                            if (count-- == 0) {
                                index = j;
                                break;
                            }
                        }
                    }
                    break;
                }
                case TanoombaFormState.Powerup: {
                    //powerup movement behavior
                    break;
                }
                #endregion
                #region Enemy Transforms
                case TanoombaFormState.Goomba: {
                    physicsObject->Velocity.X = Constants._0_875 * (enemy->FacingRight ? 1 : -1);
                    if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                        enemy->FacingRight = physicsObject->IsTouchingLeftWall;
                    }
                    break;
                }
                case TanoombaFormState.KoopaShell: {
                    physicsObject->Velocity.X = Constants._5_50 * (enemy->FacingRight ? 1 : -1);
                    if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                        enemy->FacingRight = physicsObject->IsTouchingLeftWall;
                        f.Events.PlayBumpSound(filter.Entity);
                    }
                    break;
                }
                #endregion
                #region Hazard Transforms
                case TanoombaFormState.LemmyBall: {
                    break;
                }
                #endregion
                }
                #endregion
            }

            void MoveAroundState(FP VelocityBonus, FP SpeedCap) {
                SpeedCap = FPMath.Max(FPMath.Abs(physicsObject->Velocity.X) - FP._0_10, SpeedCap);
                physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (VelocityBonus * (enemy->FacingRight ? 1 : -1)), -SpeedCap, SpeedCap);
                
                if (physicsObject->IsTouchingGround) {
                    //check for wall to jump over
                    if ((physicsObject->IsTouchingLeftWall && !enemy->FacingRight) || (physicsObject->IsTouchingRightWall && enemy->FacingRight)) {
                        FPVector2 checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, FP._1_33);
                        if (PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out var thewallbro)) {
                            enemy->ChangeFacingRight(f, entity, physicsObject->IsTouchingLeftWall);
                        } else {
                            checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, 1);
                            physicsObject->Velocity.Y = PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out thewallbro) ? tanoomba->JumpVelocity : 4;
                            physicsObject->IsTouchingGround = false;
                        }
                    }

                    //check for ledge to jump over, or turn around
                    FPVector2 checkPosition = transform->Position + (FPVector2.Right * FP._0_05 * (enemy->FacingRight ? 1 : -1));
                    if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_33, out var hit)) {
                        // Failed to hit a raycast, but check to make sure we don't have a contact point instead.

                        bool turnaround = true;
                        QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                        foreach (var contact in contacts) {
                            if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                                // Not on the ground
                                continue;
                            }

                            // Is a ground contact
                            QuantumUtils.UnwrapWorldLocations(stage, transform->Position, contact.Position, out FPVector2 ourPos, out FPVector2 contactPos);
                            if ((enemy->FacingRight && ourPos.X < contactPos.X)
                                || (!enemy->FacingRight && ourPos.X > contactPos.X)) {
                                turnaround = false;
                                break;
                            }
                        }

                        if (turnaround) {
                            //Jump The Gap? or turn back
                            FPVector2 checkPositione = transform->Position + new FPVector2((enemy->FacingRight ? FP._1_75 : -FP._1_75), 2);
                            if (PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, 3, out hit)) {
                                if (hit.Position.Y <= transform->Position.Y) {
                                    physicsObject->Velocity.Y = tanoomba->JumpVelocity;
                                    physicsObject->IsTouchingGround = false;
                                    return;
                                }
                            }
                            if (PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, 3, out hit)) {
                                return;
                            }
                            enemy->ChangeFacingRight(f, entity, !enemy->FacingRight);
                        }
                    }
                }
            }

            void TryLaugh() {
                var mario = f.Unsafe.GetPointer<MarioPlayer>(tanoomba->TargetedPlayer);
                if (mario->IsDead) {
                    tanoomba->SwitchState(f, entity, TanoombaState.Laughing);
                }
            }
        }

        #region Tanoomba Tools
        public TanoombaFormState GetForm(Frame f, ref Filter filter, VersusStageData stage) {
            //This Script Is Extremely Icky, Not Much Else To Do Bout' It
            var tanoomba = filter.Tanoomba;
            var transform = filter.Transform;
            List<TanoombaFormState> AvailibleForms = new List<TanoombaFormState>();
            for (int i = 0; i < (int) TanoombaFormState.Max - 1; i++) {
                AvailibleForms.Add((TanoombaFormState) i);
            }

            bool Decided = false;
            TanoombaFormState TryForm = TanoombaFormState.Max;
            FP GeneralDistance = 8;

            int emergencycounter = 0;
            var position = AttemptRandomPosition(f, ref filter, true);
            if (position == new FPVector2(0, -255)) {
                Debug.Log("Tanoomba Couldn't locate A Spot, Trying Again Later");
                return TanoombaFormState.Max;
            }
            while (!Decided) {
                tanoomba->FormVariant = 0;
                TryForm = AvailibleForms[FPMath.RoundToInt(f.RNG->Next() * (AvailibleForms.Count - 1))];
                List<EntityRef> EntityRefs = new();

                switch (TryForm) {
                #region Level Tranforms
                case TanoombaFormState.Coin: {
                    //Pick A Random Coin
                    var stuff = f.Filter<Coin>();
                    while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                        if (!e->IsCollected &&
                            e->CoinType.HasFlag(CoinType.BakedInStage) && !e->CoinType.HasFlag(CoinType.Dotted)
                            && FarFromPlayers(f, ref filter, f.Unsafe.GetPointer<Transform2D>(OtherEntity)->Position, GeneralDistance))
                            EntityRefs.Add(OtherEntity);
                    }
                    if (EntityRefs.Count != 0) {
                        Decided = true;
                        EntityRef pick = EntityRefs[FPMath.RoundToInt(f.RNG->Next() * (EntityRefs.Count - 1))];
                        tanoomba->TanoombaStartTransform(f, filter.Entity, pick, true);
                        f.Unsafe.GetPointer<Coin>(pick)->IsCollected = true;
                    }
                    break;
                }
                case TanoombaFormState.Block: {
                    //Find A ? Block Tile, This One Will Be Finicky To Do
                    break;
                }
                case TanoombaFormState.Star: {
                    //Be Sure To Create The Starspawn Icon, but without the sounds or animation to not draw attention to it right away
                    //Pick A Random Star Spot
                    if (!IsInHazardList(f, "Bigstar"))
                        break;
                    int spawnpoints = stage.BigStarSpawnpoints.Length;
                    for (int i = 0; i < spawnpoints; i++) {
                        int count = f.RNG->Next(0, spawnpoints);
                        int index = 0;
                        for (int j = 0; j < spawnpoints; j++) {
                            if (count-- == 0) {
                                index = j;
                                break;
                            }
                        }
                        //Remove This Spawn Location, If possible ofc
                        //if (!f.Global->UsedStarSpawns.IsSet(index)) {
                        //    f.Global->UsedStarSpawns.Set(index);
                        //    f.Global->UsedStarSpawnCount++;
                        //}

                        if (FarFromPlayers(f, ref filter, stage.BigStarSpawnpoints[index], GeneralDistance)) {
                            Decided = true;
                            tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, true, stage.BigStarSpawnpoints[index]);
                            break;
                        }
                    }
                    break;
                }
                case TanoombaFormState.Powerup: {
                    //Get A Random Avalible Powerup
                    bool thisExists = true;
                    //eh, add it checks for the existing powerups
                    tanoomba->FormVariant = (byte) f.RNG->Next(0, 16);

                    if (thisExists) {
                        Decided = true;
                        tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                    }
                    break;
                }
                #endregion
                #region Enemy Transforms
                case TanoombaFormState.Goomba: {
                    //See if Goombas Are In The Stage, Become Goomba
                    bool thisExists = false;
                    var stuff = f.Filter<Goomba>();
                    while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                        thisExists = true;
                        break;
                    }
                    thisExists |= !thisExists && IsInHazardList(f, "Goomba");
                    if (thisExists) {
                        Decided = true;
                        tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                    }
                    break;
                }
                case TanoombaFormState.KoopaShell: {
                    //See if Koopas Are In The Stage, Become Koopa
                    bool thisExists = false;
                    List<byte> Variants = new();
                    var stuff = f.Filter<Koopa>();
                    while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                        thisExists = true;
                        Variants.Add((byte) (e->DontWalkOfLedges ? 1 : e->SpawnPowerupWhenStomped != null ? 2 : e->IsSpiny ? 3 : 0));
                    }
                    thisExists |= !thisExists && (IsInHazardList(f, "Koopa")); // allow other koopas?
                    if (thisExists) {
                        Decided = true;
                        tanoomba->FormVariant = Variants == null ? (byte) 0 : Variants[f.RNG->Next(0, Variants.Count)];
                        tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                    }
                    break;
                }
                #endregion
                #region Hazard Transforms
                //Check If Hazards Contains This Object
                case TanoombaFormState.HeavyStone: {
                    bool thisExists = false;
                    var stuff = f.Filter<ThrowingObject>();
                    while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                        if (e->Type == ThrowingObjectType.Stone) {
                            thisExists = true;
                            break;
                        }
                    }
                    thisExists |= !thisExists && IsInHazardList(f, "Heavystone");
                    if (thisExists) {
                        Decided = true;
                        tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                    }
                    break;
                }
                case TanoombaFormState.LemmyBall: {
                    break;
                }
                #endregion
                }
                
                if (!Decided) {
                    Debug.Log("Tried Form: " + TryForm);
                    AvailibleForms.Remove(TryForm);
                    if (AvailibleForms.Count <= 0) {
                        Decided = true;
                        TryForm = TanoombaFormState.Max;
                    }
                }
                if (emergencycounter++ > 100) {
                    Debug.LogError("Ran Emergency Counter, List Count: " + AvailibleForms.Count + " Decided?: " + Decided);
                    foreach (var j in AvailibleForms) {
                        Debug.LogWarning(j);
                    }
                    Debug.Break();
                    return TanoombaFormState.Max;
                }
            }
            Debug.Log("Try This Form: " + TryForm);
            return TryForm;
        }

        public bool IsInHazardList(Frame f, string name) { //add the ability to "out" the special values
            foreach (var h in f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas) {
                if (h.Name == name) {
                    return true;
                }
            }
            return false;
        }

        public bool FarFromPlayers(Frame f, ref Filter filter, FPVector2 Pos, FP Distance, bool FaceTorwardPlayer = false) {
            FP distance = 999;
            var players = f.Filter<MarioPlayer>();
            bool trydirec = false;
            while (players.NextUnsafe(out EntityRef OtherEntity, out MarioPlayer* mar)) {
                if (mar->IsDead)
                    continue;
                //Find Closest Player
                QuantumUtils.UnwrapWorldLocations(f, Pos, f.Unsafe.GetPointer<Transform2D>(OtherEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FP e = FPVector2.Distance(ourPos, theirPos);
                if (e < distance) {
                    distance = e;
                    if (FaceTorwardPlayer) {
                        trydirec = ourPos.X - theirPos.X < 0;
                    }
                }
            }
            if (FaceTorwardPlayer && !(distance > Distance)) {
                filter.Enemy->FacingRight = trydirec;
            }
            return distance > Distance;
        }

        public FPVector2 AttemptRandomPosition(Frame f, ref Filter filter, bool PlaceOnGround) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = filter.Transform;

            //try 15 times
            for (int i = 0; i < 15; i++) {
                transform->Position = new FPVector2(
                    FPMath.RoundToInt(stage.StageWorldMin.X + ((stage.StageWorldMax.X - stage.StageWorldMin.X) * f.RNG->Next())), 
                    FPMath.RoundToInt(stage.StageWorldMin.Y + ((stage.StageWorldMax.Y - stage.StageWorldMin.Y) * f.RNG->Next())));

                if (PhysicsObjectSystem.BoxInGround(f, transform->Position, filter.PhysicsCollider->Shape, true, stage, filter.Entity, true) && !PhysicsObjectSystem.TryEject(f, filter.Entity, stage)) {
                    //Check if We Are In The Ground, Then Try To Eject, if Can't, Offset Position, if STILL can't, continue
                    transform->Position += new FPVector2((f.RNG->Next() * 2) - 1, (f.RNG->Next() * 2) - 1);
                    if (!PhysicsObjectSystem.TryEject(f, filter.Entity, stage)) {
                        continue;
                    }
                }

                //Snap to ground bellow
                var contacted = PhysicsObjectSystem.Raycast(f, stage, transform->Position + (FPVector2.Left / 4), FPVector2.Down, 10, out var point);
                var contactedr = PhysicsObjectSystem.Raycast(f, stage, transform->Position + (FPVector2.Right / 4), FPVector2.Down, 10, out var R);
                if (contactedr) {
                    if (point.Position.Y < R.Position.Y) {
                        point = R;
                    }
                } else if (!contacted) {
                    //huh. above a pit. can't use this position
                    continue;
                }
                transform->Position.Y = point.Position.Y + FP._0_50;

                if (FarFromPlayers(f, ref filter, transform->Position, 8)) {
                    return transform->Position;
                }
            }

            return new FPVector2(0, -255);
        }

        public bool FormIsStationary(TanoombaFormState form) {
            switch (form) {
            case TanoombaFormState.Goomba:
            case TanoombaFormState.KoopaShell: {
                return false;
            }
            }
            return true;
        }
        #endregion

        #region Interactions
        public static void OnTanoombaMarioInteraction(Frame f, EntityRef thisEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            if (tanoomba->Invulnrable) {
                return;
            }
            var mariophys = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            if (mario->InstakillsEnemies(mariophys, false)) {
                tanoomba->Kill(f, thisEntity, marioEntity, EnemyKillReason.Special);
            } else if (tanoomba->State == TanoombaState.Transformed) {
                if (FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_75) {
                    bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                    mario->DoEntityBounce = !groundpounded;
                    tanoomba->SwitchState(f, thisEntity, TanoombaState.KnockedBack, damageDirection.X > 0);
                } else {
                    mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.Normal, thisEntity, false);
                    tanoomba->TanoombaResetTransform(f, thisEntity, true);
                    tanoomba->TargetedPlayer = marioEntity;
                }
            } else if (tanoomba->State == TanoombaState.Attacking && (tanoomba->TargetedPlayer == marioEntity || !attackedFromAbove)) {
                if (mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.CollisionBump, thisEntity, tanoomba->TargetedPlayer == marioEntity && (mario->DamageInvincibilityFrames > 60 || mario->KnockbackGetupFrames > 0 || mario->IsInKnockback))) {
                    physicsObject->Velocity.X *= -1;
                    tanoomba->SwitchState(f, thisEntity, TanoombaState.Happy);
                }
            } else if (attackedFromAbove) {
                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                mario->DoEntityBounce = !groundpounded;
                tanoomba->Kill(f, thisEntity, marioEntity, EnemyKillReason.Normal);
            } else {
                tanoomba->SwitchState(f, thisEntity, TanoombaState.KnockedBack, damageDirection.X > 0);
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.CollisionBump, thisEntity, false);
            }
            return;
        }
        public static void OnTanoombaProjectileInteraction(Frame f, EntityRef thisEntity, EntityRef projectileEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);

            if (!tanoomba->Invulnrable) {
                switch (projectileAsset.Effect) {
                case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
                case ProjectileEffectType.Fire: {
                    tanoomba->SwitchState(f, thisEntity, TanoombaState.KnockedBack, !projectile->FacingRight);
                    break;
                }
                case ProjectileEffectType.Freeze: {
                    IceBlockSystem.Freeze(f, thisEntity);
                    break;
                }
                }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, thisEntity);
        }
        public static void OnTanoombaEnemyInteraction(Frame f, EntityRef thisEntity, EntityRef projectileEntity) {
            EnemySystem.EnemyBumpTurnaround(f, thisEntity, projectileEntity);

            //tanoomba interaction ig
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out Tanoomba* Dis)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || f.Exists(holdable->Holder)
                || holdable->IgnoreOwnerFrames > 0) {

                return;
            }
            
            FPVector2 bumperPosition;
            if (f.Unsafe.TryGetPointer(blockBump, out Transform2D* bumperTransform)) {
                bumperPosition = bumperTransform->Position;
            } else {
                bumperPosition = tileWorldPosition;
            }
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(entity);
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, bumperPosition, out FPVector2 ourPos, out FPVector2 theirPos);
            bool onRight = ourPos.X > theirPos.X;

            Dis->SwitchState(f, entity, TanoombaState.KnockedBack, !onRight);
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Tanoomba* tanoomba)) {
                tanoomba->Respawn(f, entity);
            }
        }
        public void OnStageReset(Frame f, QBoolean full) {
            var tanoombas = f.Filter<Tanoomba>();
            while (tanoombas.NextUnsafe(out EntityRef entity, out Tanoomba* tanoomba)) {
                switch (tanoomba->Form) {
                case TanoombaFormState.Coin: {
                    f.Unsafe.TryGetPointer<Coin>(tanoomba->TransformedObject, out var coin);
                    coin->IsCollected = true;
                    break;
                }
                case TanoombaFormState.Block: {
                    //Remove The Tile Where Tanoomba Is
                    break;
                }
                }
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Tanoomba* tanoomba)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            enemy->IsActive = true;

            //uhh i would put specific hazard spawn data here
            tanoomba->Form = TanoombaFormState.Max;

            /*switch (Dis->Type) {
            case ThrowingObjectType.Basic:
            case ThrowingObjectType.Stone:
            case ThrowingObjectType.Spring:
            case ThrowingObjectType.RedPow:
            case ThrowingObjectType.BluePow: //Bluepow And Red Pow Are Considerd Varients Of Eachother
            case ThrowingObjectType.Barrel:
            case ThrowingObjectType.Freezie:
            case ThrowingObjectType.CoinBox:
            case ThrowingObjectType.PropellerBox:
            case ThrowingObjectType.BillBlock:
            case ThrowingObjectType.CannonBox:
            case ThrowingObjectType.Fridge:
                break;
            }*/
        }
        #endregion
    }
}
