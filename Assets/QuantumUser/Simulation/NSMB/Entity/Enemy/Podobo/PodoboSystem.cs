using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class PodoboSystem : SystemMainThreadEntityFilter<Podobo, PodoboSystem.Filter>, ISignalOnEnemyRespawned, ISignalInitializeHazard, ISignalOnTryLiquidSplash {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Podobo* Podobo;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Podobo, MarioPlayer>(f, OnPodoboMarioPlayerInteraction);
            f.Context.Interactions.Register<Podobo, Projectile>(f, OnPodoboProjectileInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;

            if (!enemy->IsAlive) {
                return;
            }

            var podobo = filter.Podobo;
            var physicsObject = filter.PhysicsObject;
            var entity = filter.Entity;
            var transform = filter.Transform;

            if (physicsObject->IsUnderwater && physicsObject->Velocity.Y <= 0) {

                QuantumUtils.UnwrapWorldLocations(f, transform->Position, podobo->IsHopB ? new FPVector2(podobo->HopBLocation, 0) : enemy->Spawnpoint, out FPVector2 ourPos, out FPVector2 theirPos);

                physicsObject->Velocity.X = FPMath.Clamp((theirPos.X - ourPos.X) * 10, -6, 6);
                physicsObject->Velocity.Y = 0;

                bool Close = FPMath.Abs(theirPos.X - ourPos.X) < FP._0_05;

                switch (podobo->Varient) {
                case PodoboType.Lava: {
                    PodoboJump(2);
                    break;
                }
                case PodoboType.Poison: {
                    PodoboJump(0);
                    break;
                }
                case PodoboType.Cold: {
                    PodoboJump(4);
                    break;
                }
                }

                void PodoboJump(FP time) {
                    if (QuantumUtils.Decrement(f, ref podobo->WaitTime) && Close) {
                        transform->Position.X = podobo->IsHopB ? podobo->HopBLocation : enemy->Spawnpoint.X;
                        podobo->WaitTime = time;
                        podobo->IsHopB = !podobo->IsHopB;
                        physicsObject->Velocity.X = 0;
                        physicsObject->Velocity.Y = podobo->JumpStrength;
                        f.Events.PodoboLeap(entity);
                    }
                }
            }
        }

        public void OnPodoboMarioPlayerInteraction(Frame f, EntityRef podoboEntity, EntityRef marioEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var podobo = f.Unsafe.GetPointer<Podobo>(podoboEntity);

            if (mario->InstakillsEnemies(marioPhysicsObject, false)) {
                podobo->Kill(f, podoboEntity, marioEntity, EnemyKillReason.Special);
            } else {
                var booEnemy = f.Unsafe.GetPointer<Enemy>(podoboEntity);
                if (!mario->IsCrouchedInShell && booEnemy->IntangibilityFrames == 0) {
                    if (podobo->Varient != PodoboType.Cold) {
                        mario->Powerdown(f, marioEntity, false, podoboEntity);
                    } else {
                        IceBlockSystem.Freeze(f, marioEntity, true);
                    }
                }
            }
        }

        public void OnPodoboProjectileInteraction(Frame f, EntityRef podoboEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);
            var podobo = f.Unsafe.GetPointer<Podobo>(podoboEntity);

            switch (podobo->Varient) {
            case PodoboType.Lava: {
                PodoboWeakness(ProjectileEffectType.Freeze);
                break;
            }
            case PodoboType.Poison: {
                //no weakness
                break;
            }
            case PodoboType.Cold: {
                PodoboWeakness(ProjectileEffectType.Fire);
                break;
            }
            }

            void PodoboWeakness(ProjectileEffectType type) {
                if (projectileAsset.Effect == type) {
                    podobo->Kill(f, podoboEntity, projectileEntity, EnemyKillReason.Special);
                }
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Podobo* podobo)) {
                podobo->Respawn(f, entity);
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            if (!f.Unsafe.TryGetPointer(entity, out Podobo* podobo)) {
                return;
            }

            *doSplash = true;

            if (!exit) {
                var liquid = f.Unsafe.GetPointer<Liquid>(liquidEntity);
                var liquidTransform = f.Unsafe.GetPointer<Transform2D>(liquidEntity);

                var transform = f.Unsafe.GetPointer<Transform2D>(entity);
                transform->Position.Y = liquidTransform->Position.Y + (liquid->HeightTiles/2);
            }
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Podobo* podobo)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* phys)) {
                return;
            }

            enemy->IsActive = true;
            hazard->DoNotDespawnInPit = false;
            phys->Velocity.Y = podobo->JumpStrength;
        }
    }
}