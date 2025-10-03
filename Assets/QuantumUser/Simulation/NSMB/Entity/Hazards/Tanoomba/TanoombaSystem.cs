using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Quantum {
    
    public unsafe class TanoombaSystem : SystemMainThreadFilterStage<TanoombaSystem.Filter>, ISignalOnEntityBumped, //ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard, ISignalOnEnemyRespawned, ISignalOnStageReset {

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
        }
        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var tanoomba = filter.Tanoomba;
            var enemy = filter.Enemy;
            var physicsObject = filter.PhysicsObject;
            var transform = filter.Transform;
            var collider = filter.PhysicsCollider;
            bool ComplexFrame = f.Number == FPMath.RoundToInt(f.Number / 10) * 10; // We use this to cut down on the MANY search calculations

            //Transform if We Somehow Fell into The Pit, Tho if We Are Dead Destroy Ourselves
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                if (enemy->IsDead) {
                    HazardSystem.DestroyHazard(f, filter.Entity);
                    return;
                } else if (tanoomba->State != TanoombaState.Searching) {
                    tanoomba->TanoombaResetTransform(f, filter.Entity, false);
                    tanoomba->State = TanoombaState.Searching;
                    f.Events.PlayPuffParticle(transform->Position);
                }
            }
            if (enemy->IsDead) {
                return;
            }

            #region Base Tanoomba
            switch (tanoomba->State) {
            //Tanoomba Wanders
            case TanoombaState.Idling:
                if (tanoomba->Laughing) {
                    tanoomba->GetupFrames++;
                    if (tanoomba->GetupFrames > 240) {
                        tanoomba->Laughing = false;
                    }
                    break;
                }

                //Check For Player
                if (!FarFromPlayers(f, ref filter, transform->Position, 3) && tanoomba->GetupFrames <= 0) {
                    tanoomba->GetupFrames++;
                    physicsObject->Velocity.X = 0;
                    physicsObject->Velocity.Y = 3;
                    physicsObject->IsTouchingGround = false;
                    f.Events.TanoombaFlee(filter.Entity);
                } else if (tanoomba->GetupFrames > 0) {
                    if (tanoomba->GetupFrames++ > 45) {
                        tanoomba->State = TanoombaState.Searching;
                        f.Events.PlayPuffParticle(transform->Position);
                    }
                } else {
                    physicsObject->Velocity.X = (physicsObject->IsTouchingGround ? 1 : 1 + FP._0_75) * (enemy->FacingRight ? 1 : -1);
                }
                break;
            //Tanoomba Flees And Searches The World Something To Turn into, Away From players Ofc
            case TanoombaState.Searching: {
                if (ComplexFrame) {
                    var newForm = GetForm(f, ref filter, stage);
                    if (newForm != TanoombaFormState.Max) {
                        tanoomba->Form = newForm;
                        enemy->FacingRight = f.RNG->Next() > FP._0_50;
                        tanoomba->State = TanoombaState.Transformed;
                        f.Events.PlayPuffParticle(transform->Position);
                    } else {
                        //Do Nothing Ig... Try Again Next Frame
                    }
                }
                break;
            }
            //Tanoomba Runs Up To Attack The Player
            case TanoombaState.Attacking: {
                if (physicsObject->IsTouchingGround || tanoomba->GetupFrames > 0) {
                    tanoomba->GetupFrames++;
                }
                if (physicsObject->IsTouchingGround || tanoomba->GetupFrames > 30) {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(tanoomba->TargetedPlayer);
                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(tanoomba->TargetedPlayer);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                    enemy->FacingRight = damageDirection.X > 0;

                    if (mario->IsDead) {
                        tanoomba->TargetedPlayer = EntityRef.None;
                        f.Events.TanoombaLMAO(filter.Entity);
                        tanoomba->Laughing = true;
                        tanoomba->GetupFrames = 0;
                        physicsObject->Velocity.X = 0;
                        tanoomba->State = TanoombaState.Idling;
                    } else if (tanoomba->GetupFrames > 90) {
                        physicsObject->Velocity.X *= Constants._0_95;
                        if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_10 && tanoomba->GetupFrames > 150) {
                            tanoomba->TargetedPlayer = EntityRef.None;
                            tanoomba->State = TanoombaState.Idling;
                            physicsObject->Velocity.X = 0;
                            tanoomba->GetupFrames = 0;
                        }
                    } else {
                        physicsObject->Velocity.X = FPMath.Clamp(physicsObject->Velocity.X + (FP._0_10 * (enemy->FacingRight ? 1 : -1)), -3, 3);
                    }
                }
                break;
            }
            //Tanoomba Gets Knockbacked if hit From A loose Projectile, This is To prevent it From easily Getting One-Shoted
            case TanoombaState.KnockedBack: {
                if (physicsObject->IsTouchingGround)
                    physicsObject->Velocity.X += physicsObject->Velocity.X > 0 ? -FP._0_10 : FP._0_10;
                if (FPMath.Abs(physicsObject->Velocity.X) < FP._0_20) {
                    physicsObject->Velocity.X = 0;
                    tanoomba->GetupFrames--;
                    if (tanoomba->GetupFrames <= 0) {
                        tanoomba->State = TanoombaState.Searching;
                        f.Events.PlayPuffParticle(transform->Position);
                    }
                }
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
                #region Enemy Transforms
                case TanoombaFormState.Goomba: {
                    physicsObject->Velocity.X = Constants._0_875 * (enemy->FacingRight ? 1 : -1);
                    break;
                }
                case TanoombaFormState.KoopaShell: {
                    physicsObject->Velocity.X = Constants._5_50 * (enemy->FacingRight ? 1 : -1);
                    //See if Koopass Are In The Stage, Become Koopa
                    break;
                }
                #endregion
                #region Hazard Transforms
                //Check If Hazards Contains This Object
                case TanoombaFormState.HeavyStone: {
                    break;
                }
                case TanoombaFormState.LemmyBall: {
                    //Check If Hazards Contains This Object
                    break;
                }
                #endregion
                }
                #endregion
            } else {
                //Check For Level Geometry
                if (physicsObject->IsTouchingGround && (tanoomba->State == TanoombaState.Idling || tanoomba->State == TanoombaState.Attacking)) {
                    if ((physicsObject->IsTouchingLeftWall && !enemy->FacingRight) || (physicsObject->IsTouchingRightWall && enemy->FacingRight)) {
                        FPVector2 checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, FP._1_33);
                        if (PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out var thewallbro)) {
                            enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
                        } else {
                            checkPositione = transform->Position + new FPVector2(enemy->FacingRight ? FP._0_50 : -FP._0_50, 1);
                            physicsObject->Velocity.Y = PhysicsObjectSystem.Raycast(f, stage, checkPositione, FPVector2.Down, FP._0_33, out thewallbro) ? tanoomba->JumpVelocity : 4;
                            physicsObject->IsTouchingGround = false;
                        }
                    }

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
                            enemy->ChangeFacingRight(f, filter.Entity, !enemy->FacingRight);
                        }
                    }
                }
            }
        }

        #region Tanoomba Tools
        public TanoombaFormState GetForm(Frame f, ref Filter filter, VersusStageData stage) {
            //This Script Is Extremely Icky, Not Much Else To Do Bout' It
            var tanoomba = filter.Tanoomba;
            var transform = filter.Transform;
            FPVector2 nullPos = new FPVector2(0, -255);
            List<TanoombaFormState> AvailibleForms = new List<TanoombaFormState>();
            for (int i = 0; i < (int) TanoombaFormState.Max - 1; i++) {
                AvailibleForms.Add((TanoombaFormState) i);
            }

            bool Decided = false;
            TanoombaFormState TryForm = TanoombaFormState.Max;
            FP GeneralDistance = 8;

            int emergencycounter = 0;
            while (!Decided) {
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
                        tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRefs[FPMath.RoundToInt(f.RNG->Next() * (EntityRefs.Count - 1))], true);
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
                    bool BigStarsExist = false;
                    foreach (var h in f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas) {
                        if (h.Name == "Bigstar") {
                            BigStarsExist = true;
                            break;
                        }
                    }
                    if (!BigStarsExist)
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

                        if (FarFromPlayers(f, ref filter, stage.BigStarSpawnpoints[index], GeneralDistance)) {
                            Decided = true;
                            tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, true, stage.BigStarSpawnpoints[index]);
                            break;
                        }
                    }
                    break;
                }
                #endregion
                #region Enemy Transforms
                case TanoombaFormState.Goomba: {
                    //See if Goombas Are In The Stage, Become Goomba
                    bool thisExists = false;
                    foreach (var h in f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas) {
                        if (h.Name == "Goomba") {
                            thisExists = true;
                            break;
                        }
                    }
                    if (!thisExists) {
                        var stuff = f.Filter<Goomba>();
                        while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                            thisExists = true;
                            break;
                        }
                    }
                    if (thisExists) {
                        var position = AttemptRandomPosition(f, ref filter, true);
                        if (position != nullPos) {
                            Decided = true;
                            tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                        }
                    }
                    break;
                }
                case TanoombaFormState.KoopaShell: {
                    //See if Koopass Are In The Stage, Become Koopa
                    bool thisExists = false;
                    foreach (var h in f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas) {
                        if (h.Name == "Koopa" || h.Name == "Red Koopa" || h.Name == "Blue Koopa" || h.Name == "Rolla Koopa") {
                            thisExists = true;
                            break;
                        }
                    }
                    if (!thisExists) {
                        var stuff = f.Filter<Koopa>();
                        while (stuff.NextUnsafe(out EntityRef OtherEntity, out var e)) {
                            thisExists = true;
                            break;
                        }
                    }
                    if (thisExists) {
                        var position = AttemptRandomPosition(f, ref filter, true);
                        if (position != nullPos) {
                            Decided = true;
                            tanoomba->TanoombaStartTransform(f, filter.Entity, EntityRef.None, false, position);
                        }
                    }
                    break;
                }
                #endregion
                #region Hazard Transforms
                //Check If Hazards Contains This Object
                case TanoombaFormState.HeavyStone: {
                    break;
                }
                case TanoombaFormState.LemmyBall: {
                    //Check If Hazards Contains This Object
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

        public bool FarFromPlayers(Frame f, ref Filter filter, FPVector2 Pos, FP Distance) {
            FP distance = 999;
            var players = f.Filter<MarioPlayer>();
            while (players.NextUnsafe(out EntityRef OtherEntity, out MarioPlayer* mar)) {
                if (mar->IsDead)
                    continue;
                //Find Closest Player
                QuantumUtils.UnwrapWorldLocations(f, Pos, f.Unsafe.GetPointer<Transform2D>(OtherEntity)->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FP e = FPVector2.Distance(ourPos, theirPos);
                if (e < distance) {
                    distance = e;
                }
            }
            return distance > Distance;
        }

        public FPVector2 AttemptRandomPosition(Frame f, ref Filter filter, bool PlaceOnGround) {

            return new FPVector2(0, -255);
        }
        #endregion

        #region Interactions
        public static void OnTanoombaMarioInteraction(Frame f, EntityRef thisEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->State == TanoombaState.Searching || (tanoomba->State == TanoombaState.Attacking && tanoomba->GetupFrames < 12) ) {
                return;
            }
            //var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var mariophys = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            //FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            if (mario->InstakillsEnemies(mariophys, false)) {
                tanoomba->Kill(f, thisEntity, marioEntity, KillReason.Special);
            } else if (tanoomba->State == TanoombaState.Transformed) {
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.Normal, thisEntity, false);
                tanoomba->TanoombaResetTransform(f, thisEntity, true);
                tanoomba->TargetedPlayer = marioEntity;
                tanoomba->GetupFrames = 0;
            } else if (tanoomba->State == TanoombaState.Attacking && (tanoomba->TargetedPlayer == marioEntity || !attackedFromAbove)) {
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.CollisionBump, thisEntity, tanoomba->TargetedPlayer == marioEntity && tanoomba->GetupFrames <= 90);
                physicsObject->Velocity.X *= -1;
                tanoomba->GetupFrames = 90;
                f.Events.TanoombaAttack(thisEntity);
            } else if (attackedFromAbove) {
                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                mario->DoEntityBounce = !groundpounded;
                tanoomba->Kill(f, thisEntity, marioEntity, KillReason.Normal);
            } else {
                tanoomba->HurtTanoomba(f, thisEntity, marioEntity, damageDirection.X > 0);
                mario->DoKnockback(f, marioEntity, damageDirection.X <= 0, 1, KnockbackStrength.CollisionBump, thisEntity, false);
            }
            return;
        }
        public static void OnTanoombaProjectileInteraction(Frame f, EntityRef thisEntity, EntityRef projectileEntity) {
            var projectile = f.Unsafe.GetPointer<Projectile>(projectileEntity);
            var projectileAsset = f.FindAsset(projectile->Asset);
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->State != TanoombaState.Idling && tanoomba->State != TanoombaState.Attacking) {
                return;
            }

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                f.Unsafe.GetPointer<Tanoomba>(thisEntity)->HurtTanoomba(f, thisEntity, projectileEntity, !projectile->FacingRight);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, thisEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, thisEntity);
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

            Dis->HurtTanoomba(f, entity, blockBump, onRight);
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
                    coin->IsCollected = false;
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
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

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
