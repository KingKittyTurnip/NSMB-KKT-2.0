using Photon.Deterministic;
using Quantum.Collections;
using System.Collections.Generic;

namespace Quantum {
    public unsafe class CataquackSystem : SystemMainThreadEntityFilter<Cataquack, CataquackSystem.Filter>, ISignalOnEntityBumped, ISignalOnBobombExplodeEntity,
        ISignalOnIceBlockBroken, ISignalOnEnemyKilledByStageReset, ISignalOnEntityCrushed, ISignalOnEnemyRespawned, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public Cataquack* Cataquack;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Cataquack, MarioPlayer>(f, OnDuckMarioInteraction);
            f.Context.Interactions.Register<Cataquack, Projectile>(f, OnDuckProjectileInteraction);
            f.Context.Interactions.Register<Cataquack, IceBlock>(f, OnDuckIceBlockInteraction);

            f.Context.Interactions.Register<PhysicsObject, Cataquack>(f, OnDuckAnythingInteraction);
            f.Context.Interactions.Register<PhysicsObject, Cataquack>(f, OnDuckSolidInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var duckman = filter.Cataquack;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;

            // Death animation
            if (enemy->IsDead) {
                return;
            }

            // Inactive check 
            if (!enemy->IsAlive
                || filter.Freezable->IsFrozen(f)) {
                return;
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
            }

            QuantumUtils.Decrement(f, ref duckman->FlingTimer);

            if (physicsObject->IsTouchingGround) {
                //Turn Around At Ledge
                if (duckman->Varient > CataquackVarient.BasicBlue) {
                    FPVector2 checkPosition = transform->Position + filter.Collider->Shape.Centroid /* + (FPVector2.Right * FP._0_05 * (enemy->FacingRight ? 1 : -1))*/;
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

                // Move
                physicsObject->Velocity.X = duckman->FlingTimer == 0 ? duckman->Speed * (enemy->FacingRight ? 1 : -1) : 0;
            }
        }

        #region Interactions
        public static void OnDuckMarioInteraction(Frame f, EntityRef duckEntity, EntityRef marioEntity) {
            var duckman = f.Unsafe.GetPointer<Cataquack>(duckEntity);
            var duckTransform = f.Unsafe.GetPointer<Transform2D>(duckEntity);
            var duckEnemy = f.Unsafe.GetPointer<Enemy>(duckEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            if (duckEnemy->IsDead) {
                return;
            }

            QuantumUtils.UnwrapWorldLocations(f, duckTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            bool groundpounded = mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (mario->InstakillsEnemies(marioPhysicsObject, true)) {
                if (duckman->Varient == CataquackVarient.SturdyGreen) {
                    f.Events.IsNowResistantHit(f.Number, duckEntity);
                } else {
                    duckman->Kill(f, duckEntity, marioEntity, groundpounded ? EnemyKillReason.Groundpounded : EnemyKillReason.Special);
                    mario->DoEntityBounce |= mario->IsDrilling;
                }
                return;
            }
            if (groundpounded) {
                if (duckman->Varient == CataquackVarient.SturdyGreen) {
                    f.Events.IsNowResistantHit(f.Number, duckEntity);
                } else {
                    duckman->Kill(f, duckEntity, marioEntity, groundpounded ? EnemyKillReason.Groundpounded : EnemyKillReason.Special);
                    mario->DoEntityBounce |= mario->IsDrilling;
                    return;
                }
            }

            if (mario->IsCrouchedInShell) {
                marioPhysicsObject->Velocity.X = 0;
                duckEnemy->ChangeFacingRight(f, duckEntity, ourPos.X > theirPos.X);

            } else {
                if (duckman->FlingTimer <= 0) {
                    duckEnemy->ChangeFacingRight(f, duckEntity, damageDirection.X > 0);
                    f.Events.CataquackFling(f, duckEntity);
                }
                mario->JumpHeld = true;
                mario->JumpState = JumpState.SingleJump;
                mario->IsGroundpounding = false;
                marioPhysicsObject->IsTouchingGround = false;
                marioPhysicsObject->Velocity.Y = duckman->LaunchSpeed;
                marioPhysicsObject->Velocity.X = duckEnemy->FacingRight ? -3 : 3;
                duckman->FlingTimer = Constants._0_35;
            }
        }

        public static bool OnDuckIceBlockInteraction(Frame f, EntityRef duckEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var duckman = f.Unsafe.GetPointer<Cataquack>(duckEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            if (duckman->Varient == CataquackVarient.SturdyGreen) {
                f.Events.IsNowResistantHit(f.Number, duckEntity);
                IceBlockSystem.Destroy(f, iceBlockEntity, IceBlockBreakReason.HitWall, duckEntity);
                return false;
            }

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < Constants.PhysicsGroundMaxAngleCos) {

                duckman->Kill(f, duckEntity, iceBlockEntity, EnemyKillReason.Special);
            }
            return false;
        }

        public static void OnDuckProjectileInteraction(Frame f, EntityRef duckEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);
            var duckman = f.Unsafe.GetPointer<Cataquack>(duckEntity);

            if (duckman->Varient == CataquackVarient.SturdyGreen) {
                f.Events.IsNowResistantHit(f.Number, duckEntity);
            } else {
                switch (projectileAsset.Effect) {
                case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
                case ProjectileEffectType.Fire: {
                    f.Unsafe.GetPointer<Cataquack>(duckEntity)->Kill(f, duckEntity, projectileEntity, EnemyKillReason.Special);
                    break;
                }
                case ProjectileEffectType.Freeze: {
                    IceBlockSystem.Freeze(f, duckEntity);
                    break;
                }
                }
            }

            f.Signals.OnProjectileHitEntity(projectileEntity, duckEntity);
        }


        public static bool OnDuckSolidInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, PhysicsContact contact) {
            return HandleDuckInteraction(f, anyEntity, thisEntity, true);
        }
        public static void OnDuckAnythingInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity) {
            HandleDuckInteraction(f, anyEntity, thisEntity, false);
        }
        public static bool HandleDuckInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, bool FromSolid) {
            var duckman = f.Unsafe.GetPointer<Cataquack>(thisEntity);
            var duckEnemy = f.Unsafe.GetPointer<Enemy>(thisEntity);
            var otherPhys = f.Unsafe.GetPointer<PhysicsObject>(anyEntity);

            //fix for lemmyball interaction bug
            //if (f.Has<LemmyBall>(anyEntity))
            //    LemmyBallSystem.TryLemmyBallPush(f, anyEntity, thisEntity, true);

            if (duckEnemy->IsDead || otherPhys->WindImmune || otherPhys->IsFrozen) {
                return false;
            }

            var otherTransform = f.Unsafe.GetPointer<Transform2D>(anyEntity)->Position; var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), otherTransform, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (duckman->FlingTimer <= 0) {
                duckEnemy->ChangeFacingRight(f, thisEntity, damageDirection.X > 0);
                f.Events.CataquackFling(f, thisEntity);
            }
            otherPhys->IsTouchingGround = false;
            otherPhys->Velocity.Y = duckman->LaunchSpeed;
            otherPhys->Velocity.X = duckEnemy->FacingRight ? -4 : 4;
            duckman->FlingTimer = Constants._0_35;
            return true;
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Cataquack* duckman)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return;
            }

            duckman->Kill(f, entity, bumpOwner, EnemyKillReason.Special);
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity, ExplosionType type) {
            if (f.Unsafe.TryGetPointer(entity, out Cataquack* duckman)) {
                duckman->Kill(f, entity, bobomb, EnemyKillReason.Special);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason, EntityRef attacker) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Cataquack* duckman)) {
                duckman->Kill(f, iceBlock->Entity, brokenIceBlock, EnemyKillReason.Special);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Cataquack* duckman)) {
                duckman->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Cataquack* duckman)) {
                duckman->Kill(f, entity, EntityRef.None, EnemyKillReason.InWall);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Cataquack* duckman)) {
                duckman->Respawn(f, entity);
            }
        }
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Cataquack* duckman)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)) {
                return;
            }
            var specialValues = f.ResolveList(spawnData);
            UnityEngine.Debug.Log(specialValues[0]);
            //Set Varient
            duckman->Varient = (CataquackVarient) specialValues[0];

            enemy->IsActive = true;
            enemy->FacingRight = f.RNG->Next((FP) 0, 1) > FP._0_50;
        }
        #endregion
    }
}