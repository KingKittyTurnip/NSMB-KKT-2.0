using Photon.Deterministic;
using Quantum.Collections;
using static BreakableBrickTile;
using static IInteractableTile;
using UnityEngine;
using System.Security.Principal;

namespace Quantum {
    public unsafe class ThwompSystem : SystemMainThreadEntityFilter<Thwomp, ThwompSystem.Filter>, ISignalOnEntityBumped,
        ISignalOnEnemyKilledByStageReset, ISignalOnEnemyRespawned, ISignalInitializeHazard {

        public struct Filter {
			public EntityRef Entity;
			public Transform2D* Transform;
            public Enemy* Enemy;
			public Thwomp* Thwomp;
			public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
		}

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Thwomp, Goomba>(f, OnThwompGoombaInteraction);
            f.Context.Interactions.Register<Thwomp, Koopa>(f, OnThwompKoopaInteraction);
            f.Context.Interactions.Register<Thwomp, Bobomb>(f, OnThwompBobombInteraction);
            f.Context.Interactions.Register<Thwomp, PiranhaPlant>(f, OnThwompPlantInteraction);

            f.Context.Interactions.Register<Thwomp, Thwomp>(f, OnThwompThwompInteraction);
            f.Context.Interactions.Register<Thwomp, MarioPlayer>(f, OnThwompMarioInteraction);
            f.Context.Interactions.Register<Thwomp, Projectile>(f, OnThwompProjectileInteraction);
            f.Context.Interactions.Register<Thwomp, IceBlock>(f, OnThwompIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var thwomp = filter.Thwomp;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            // Inactive check 
            if (!enemy->IsAlive) {
                return;
            }

            Debug.Log(thwomp->State);

            switch (thwomp->State) {
            case ThwompState.Idle: {
                physicsObject->Velocity.X *= FP._0_75;

                var Players = f.Filter<MarioPlayer>();
                while (Players.NextUnsafe(out EntityRef OtherEntity, out MarioPlayer* mario)) {
                    //Ignore Dead Players
                    if (mario->IsDead)
                        continue;

                    var marioTransform = f.Unsafe.GetPointer<Transform2D>(OtherEntity);
                    var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(OtherEntity);
                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos);
                    damageDirection.X = FPMath.Abs(damageDirection.X);

                    FP bonus = thwomp->Big ? FP._0_50 : 0;

                    thwomp->PlayerNear = damageDirection.Y < FP._0_50 && damageDirection.X < 3 + bonus;
                    //Check If Player Is Within Bounds
                    if (!(damageDirection.Y < FP._0_50 && damageDirection.X < FP._1_20 + bonus)) {
                        continue;
                    }

                    //Start Fall
                    thwomp->State = ThwompState.Fall;
                    enemy->FacingRight = damageDirection.X > 0;
                    physicsObject->Gravity.Y = -18;
                    physicsObject->Velocity.Y = FP._1_50;
                    break;
                }
                break;
            }
            case ThwompState.Fall: {
                if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                    TouchedBricks(f, filter.Entity, stage);
                    thwomp->State = ThwompState.Landed;
                    thwomp->Timer = 0;
                    f.Events.ThwompLanded(f, filter.Entity, thwomp->Big);
                }
                break;
            }
            case ThwompState.Landed: {
                thwomp->Timer++;
                physicsObject->Velocity.X = 0;
                if (thwomp->Timer > 60) {
                    thwomp->State = ThwompState.Recover;
                    physicsObject->Gravity.Y = 0;
                    thwomp->Timer = 0;
                } else if (!physicsObject->IsTouchingGround) {
                    thwomp->State = ThwompState.Fall;
                }
                break;
            }
            case ThwompState.Recover: {
                physicsObject->Velocity.X *= FP._0_75;
                physicsObject->Velocity.Y = FP._1_50;
                physicsObject->IsTouchingGround = false;
                if (transform->Position.Y > enemy->Spawnpoint.Y || physicsObject->IsTouchingCeiling) {
                    physicsObject->Velocity.Y = 0;
                    thwomp->Timer++;
                    if (thwomp->Timer > 20) {
                        thwomp->State = ThwompState.Idle;
                    }
                }
                break;
            }
            }
        }

        private bool TouchedBricks(Frame f, EntityRef Choomba, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(Choomba);

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
                if (tile is IInteractableTile it) {
                    it.Interact(f, Choomba, InteractionDirection.Up,
                       new IntVector2(contact.Tile.X, contact.Tile.Y), tileInstance, out bool tempPlayBumpSound);

                    //If The Thing in Front Is Breakable By Bombs Or Shells, Push Through (do note he can break mega breakabled he will just be bumped)
                    if (!((tile is BreakableBrickTile uh) 
                        || (tile is CoinTile uhh && !uhh.BreakingRules.HasFlag(BreakableBy.Shells) && !uhh.BreakingRules.HasFlag(BreakableBy.Bombs))
                        || (tile is PowerupTileBase uhhh && !uhhh.BreakingRules.HasFlag(BreakableBy.Shells) && !uhhh.BreakingRules.HasFlag(BreakableBy.Bombs))))
                        BrickBroken = true;
                }
            }

            return BrickBroken;
        }

        #region Interactions
        public static void OnThwompGoombaInteraction(Frame f, EntityRef thwompEntity, EntityRef goombaEntity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            if (thwomp->State == ThwompState.Fall) {
                f.Unsafe.GetPointer<Goomba>(goombaEntity)->Kill(f, goombaEntity, thwompEntity, EnemyKillReason.Special);
            }
        }
        public static void OnThwompKoopaInteraction(Frame f, EntityRef thwompEntity, EntityRef koopaEntity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            if (thwomp->State == ThwompState.Fall) {
                f.Unsafe.GetPointer<Koopa>(koopaEntity)->Kill(f, koopaEntity, thwompEntity, EnemyKillReason.Special);
            }
        }
        public static void OnThwompBobombInteraction(Frame f, EntityRef thwompEntity, EntityRef bobombEntity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            if (thwomp->State == ThwompState.Fall) {
                f.Unsafe.GetPointer<Bobomb>(bobombEntity)->Kill(f, bobombEntity, thwompEntity, EnemyKillReason.Special);
            }
        }
        public static void OnThwompPlantInteraction(Frame f, EntityRef thwompEntity, EntityRef plantEntity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            if (thwomp->State == ThwompState.Fall) {
                f.Unsafe.GetPointer<PiranhaPlant>(plantEntity)->Kill(f, plantEntity, thwompEntity, EnemyKillReason.Special);
            }
        }
        public static void OnThwompThwompInteraction(Frame f, EntityRef thwompEntityA, EntityRef thwompEntityB) {
            var thwompATransform = f.Unsafe.GetPointer<Transform2D>(thwompEntityA);
            var thwompBTransform = f.Unsafe.GetPointer<Transform2D>(thwompEntityB);

            QuantumUtils.UnwrapWorldLocations(f, thwompATransform->Position + FPVector2.Up * FP._0_10, thwompBTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FP damageDirection = (theirPos - ourPos).Normalized.X;

            f.Unsafe.GetPointer<PhysicsObject>(thwompEntityA)->Velocity.X = damageDirection > 0 ? -3 : 3;
            f.Unsafe.GetPointer<PhysicsObject>(thwompEntityB)->Velocity.X = damageDirection > 0 ? 3 : -3;
        }

        public static void OnThwompMarioInteraction(Frame f, EntityRef thwompEntity, EntityRef marioEntity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            var thwompTransform = f.Unsafe.GetPointer<Transform2D>(thwompEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            QuantumUtils.UnwrapWorldLocations(f, thwompTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_75;

            if (mario->InstakillsEnemies(marioPhysicsObject, true)) {
                thwomp->Kill(f, thwompEntity, marioEntity, EnemyKillReason.Special);
                mario->DoEntityBounce |= mario->IsDrilling;
                return;
            }
            if (mario->IsCrouchedInShell) {
                mario->FacingRight = damageDirection.X < 0;
                marioPhysicsObject->Velocity.X = 0;
            } else if (mario->IsDamageable) {
                mario->Powerdown(f, marioEntity, false, thwompEntity);
            }
        }

        public static bool OnThwompIceBlockInteraction(Frame f, EntityRef thwompEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            IceBlockSystem.Destroy(f, iceBlockEntity, IceBlockBreakReason.Groundpounded, thwompEntity);
            return false;
        }

        public static void OnThwompProjectileInteraction(Frame f, EntityRef disEntity, EntityRef projectileEntity) {
            GiveprojectileEffect(f, disEntity, projectileEntity, f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset));
        }
        private static void GiveprojectileEffect(Frame f, EntityRef disEntity, EntityRef projEntity, ProjectileAsset asset) {
            var enemy = f.Unsafe.GetPointer<Enemy>(disEntity);
            var PhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(disEntity);
            if (PhysicsObject->Velocity.Y > 0)
                return;
            var thwomp = f.Unsafe.GetPointer<Thwomp>(disEntity);
            var Transform = f.Unsafe.GetPointer<Transform2D>(disEntity);
            var TransformProj = f.Unsafe.GetPointer<Transform2D>(projEntity);

            QuantumUtils.UnwrapWorldLocations(f, Transform->Position + FPVector2.Up * FP._0_10, TransformProj->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            //TODO: Magic numbers galore, make door hit thwomp like this
            if (false) {
                //thwomp gets launched
                thwomp->State = ThwompState.Fall;
                PhysicsObject->Velocity.X = damageDirection.X > 0 ? -3 : 3;
                PhysicsObject->Velocity.Y = 10;
                PhysicsObject->Gravity.Y = -18;
                PhysicsObject->IsTouchingGround = false;
            }

            f.Signals.OnProjectileHitEntity(projEntity, disEntity);
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Thwomp* thwomp)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive) {
                return;
            }

            var PhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            PhysicsObject->Velocity.Y = 6;
            PhysicsObject->IsTouchingGround = false;
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            //Break Things In The Way
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (f.Unsafe.TryGetPointer(entity, out Thwomp* thwomp)) {
                TouchedBricks(f, entity, stage);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Thwomp* thwomp)) {
                thwomp->Respawn(f, entity);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Thwomp* thwomp)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsCollider2D* collider)) {
                return;
            }
            var specialValues = f.ResolveList(spawnData);

            enemy->IsActive = true;

            if (specialValues[0] == 1) {
                //Big thwomp
                thwomp->Big = true;
                collider->Shape.Centroid.Y += collider->Shape.Box.Extents.Y;
                collider->Shape.Box.Extents *= 2;
            }
        }
        #endregion
    }
}