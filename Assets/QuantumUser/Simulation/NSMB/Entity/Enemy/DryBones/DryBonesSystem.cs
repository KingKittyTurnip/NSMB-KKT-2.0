using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class DryBonesSystem : SystemMainThreadEntityFilter<DryBones, DryBonesSystem.Filter>, ISignalOnEntityBumped, ISignalOnBobombExplodeEntity,
        ISignalOnIceBlockBroken, ISignalOnEnemyKilledByStageReset, ISignalOnEntityCrushed, ISignalOnEnemyRespawned, ISignalOnEnemyTurnaround, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public DryBones* Dry;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* Freezable;
            public Interactable* Interactable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<DryBones, Goomba>(f, OnDryGoombaInteraction);
            f.Context.Interactions.Register<DryBones, DryBones>(f, OnDryDryInteraction);
            f.Context.Interactions.Register<DryBones, PiranhaPlant>(f, EnemySystem.EnemyBumpTurnaroundOnlyFirst);
            f.Context.Interactions.Register<DryBones, MarioPlayer>(f, OnDryMarioInteraction);
            f.Context.Interactions.Register<DryBones, Projectile>(f, OnDryProjectileInteraction);
            f.Context.Interactions.Register<DryBones, IceBlock>(f, OnDryIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var dry = filter.Dry;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var interactable = filter.Interactable;

            // Inactive check 
            if (!enemy->IsAlive
                || filter.Freezable->IsFrozen(f)) {
                if (!enemy->IsActive && dry->DryHead != EntityRef.None) {
                    //Destroy Existing head We Are Dead
                    f.Events.PlayPuffParticle(f.Unsafe.GetPointer<Transform2D>(dry->DryHead)->Position + (FPVector2.Up * FP._0_25));
                    f.Destroy(dry->DryHead);
                    dry->DryHead = EntityRef.None;
                }
                return;
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
            }

            switch (dry->State) {
            case DryState.Idle: {
                // Move
                if (!QuantumUtils.Decrement(f, ref dry->ReusableTimer)) {
                    physicsObject->Velocity.X = 0;

                } else if (physicsObject->IsTouchingLeftWall
                           || physicsObject->IsTouchingRightWall
                           || physicsObject->IsTouchingGround) {

                    physicsObject->Velocity.X = dry->Speed * (enemy->FacingRight ? 1 : -1);
                }

                // Ledge Check
                if (physicsObject->IsTouchingGround) {
                    FPVector2 checkPosition = transform->Position + filter.Collider->Shape.Centroid;
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
                            enemy->ChangeFacingRight(f, entity, !enemy->FacingRight);
                        }
                    }
                }
                break;
            }
            case DryState.Broken: {
                if (QuantumUtils.Decrement(f, ref dry->ReusableTimer)) {
                    SwitchState(DryState.Retrieve, FP._0_50);
                    f.Events.DryRetrive(f, entity);
                } else {
                    var headphys = f.Unsafe.GetPointer<PhysicsObject>(dry->DryHead);
                    var head = f.Unsafe.GetPointer<DryHead>(dry->DryHead);
                    CalcHeadPhysics(headphys, head);
                }
                TryStopMomentum();
                break;
            }
            case DryState.Retrieve: {
                var headphys = f.Unsafe.GetPointer<PhysicsObject>(dry->DryHead);
                var head = f.Unsafe.GetPointer<DryHead>(dry->DryHead);

                if (QuantumUtils.Decrement(f, ref dry->ReusableTimer)) {
                    //retrive head
                    var headTransform = f.Unsafe.GetPointer<Transform2D>(dry->DryHead);

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + new FPVector2(enemy->FacingRight ? dry->HeadSpawnOffset.X : -dry->HeadSpawnOffset.X, 0), headTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;

                    headphys->Gravity.Y = 0;
                    headphys->DisableCollision = true;
                    headphys->IsTouchingGround = false;
                    headphys->Velocity = -damageDirection * head->Speed;

                    //head retrived
                    if ((theirPos - ourPos).Magnitude < FP._0_10) {
                        // turn to face closest player
                        /*Boss.GetClosestPlayer(f, transform->Position, EntityRef.None, out var NewMario, out var Distance);
                        if (f.Unsafe.TryGetPointer(NewMario, out Transform2D* marioTransform)) {
                            QuantumUtils.WrappedDistance(f, transform->Position, marioTransform->Position, out FP xDiff);
                            enemy->ChangeFacingRight(f, entity, xDiff < 0);
                        }*/

                        SwitchState(DryState.Idle, FP._1_50);
                        enemy->IntangibilityFrames = 90;

                        f.Destroy(dry->DryHead);
                        dry->DryHead = EntityRef.None;

                        interactable->ColliderDisabled = false;
                        f.Events.DryGetup(entity);
                    }
                } else {
                    CalcHeadPhysics(headphys, head);
                }
                TryStopMomentum();
                break;
            }
            }

            void SwitchState(DryState newState, FP time) {
                dry->State = newState;
                dry->ReusableTimer = time;
            }

            void TryStopMomentum() {
                if (physicsObject->IsTouchingGround) {
                    physicsObject->Velocity.X = 0;
                }
            }

            void CalcHeadPhysics(PhysicsObject* headphys, DryHead* head) {
                if (headphys->IsTouchingGround) {
                    if (headphys->Velocity.Y < -7) {
                        head->BounceTimes = 3;
                    }
                    if (head->BounceTimes > 0) {
                        headphys->Velocity.Y = head->BounceTimes * FP._1_50;
                        head->BounceTimes--;
                        headphys->IsTouchingGround = false;
                    }
                    headphys->Velocity.X *= Constants._0_90;
                }
            }
        }

        #region Interactions
        public static void OnDryDryInteraction(Frame f, EntityRef EntityA, EntityRef EntityB) {
            EnemySystem.EnemyBumpTurnaround(f, EntityA, EntityB);
        }
        public static void OnDryGoombaInteraction(Frame f, EntityRef EntityA, EntityRef EntityB) {
            EnemySystem.EnemyBumpTurnaround(f, EntityA, EntityB);
        }

        public static void OnDryMarioInteraction(Frame f, EntityRef dryEntity, EntityRef marioEntity) {
            var dry = f.Unsafe.GetPointer<DryBones>(dryEntity);
            var dryTransform = f.Unsafe.GetPointer<Transform2D>(dryEntity);
            var dryEnemy = f.Unsafe.GetPointer<Enemy>(dryEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, dryTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (mario->InstakillsEnemies(marioPhysicsObject, true)) {
                dry->Kill(f, dryEntity, marioEntity, groundpounded ? EnemyKillReason.Groundpounded : EnemyKillReason.Special);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }

            if (attackedFromAbove) {
                if (mario->CurrentPowerupState == PowerupState.MiniMushroom) {
                    if (mario->IsGroundpounding) {
                        mario->IsGroundpounding = false;
                        dry->DryBreak(f, dryEntity, false);
                    }
                    mario->DoEntityBounce = true;
                } else {
                    dry->DryBreak(f, dryEntity, mario->IsGroundpounding);
                    mario->DoEntityBounce = !mario->IsGroundpounding;
                }

                mario->IsDrilling = false;

            } else if (mario->IsCrouchedInShell) {
                marioPhysicsObject->Velocity.X = 0;
                dryEnemy->ChangeFacingRight(f, dryEntity, ourPos.X > theirPos.X);

            } else if (mario->IsDamageable && dryEnemy->IntangibilityFrames == 0) {
                mario->Powerdown(f, marioEntity, false, dryEntity);
                dryEnemy->ChangeFacingRight(f, dryEntity, damageDirection.X > 0);
            }
        }

        public static bool OnDryIceBlockInteraction(Frame f, EntityRef dryEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var dry = f.Unsafe.GetPointer<DryBones>(dryEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < Constants.PhysicsGroundMaxAngleCos) {

                dry->Kill(f, dryEntity, iceBlockEntity, EnemyKillReason.Special);
            }
            return false;
        }

        public static void OnDryProjectileInteraction(Frame f, EntityRef dryEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers: {
                f.Unsafe.GetPointer<DryBones>(dryEntity)->Kill(f, dryEntity, projectileEntity, EnemyKillReason.Special);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, dryEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, dryEntity);
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out DryBones* dry)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return;
            }

            dry->DryBreak(f, entity, false);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity, ExplosionType type) {
            if (f.Unsafe.TryGetPointer(entity, out DryBones* dry)) {
                dry->Kill(f, entity, bobomb, EnemyKillReason.Special);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out DryBones* dry)) {
                dry->Kill(f, iceBlock->Entity, brokenIceBlock, EnemyKillReason.Special);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out DryBones* dry)) {
                dry->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out DryBones* dry)) {
                dry->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }
        public void OnEnemyTurnaround(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out DryBones* dry) && dry->State == DryState.Idle) {
                dry->ReusableTimer = Constants._0_15;
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out DryBones* dry)) {
                dry->Respawn(f, entity);
            }
        }
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out DryBones* dry)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)) {
                return;
            }

            enemy->IsActive = true;
            enemy->FacingRight = f.RNG->Next((FP)0, 1) > FP._0_50;
        }
        #endregion
    }
}