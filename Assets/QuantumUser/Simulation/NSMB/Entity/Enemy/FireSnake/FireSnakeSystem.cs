using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    public unsafe class FireSnakeSystem : SystemMainThreadEntityFilter<FireSnake, FireSnakeSystem.Filter>, ISignalOnEnemyReturnedHome,
        ISignalOnComponentAdded<FireSnake>, ISignalOnComponentRemoved<FireSnake>, ISignalOnEnemyRespawned, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public FireSnake* FireSnake;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public Hazard* Hazard;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<FireSnake, MarioPlayer>(f, OnFireSnakeMarioInteraction);
            f.Context.Interactions.Register<FireSnakeSegment, MarioPlayer>(f, OnFireSnakeMarioInteraction);
            f.Context.Interactions.Register<FireSnake, Projectile>(f, OnFireSnakeProjectileInteraction);
            f.Context.Interactions.Register<FireSnakeSegment, Projectile>(f, OnFireSnakeProjectileInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var fireSnake = filter.FireSnake;
            var hazard = filter.Hazard;
            if (!f.Exists(fireSnake->Segments[0])) {
                SpawnSegments(f, filter.Entity, fireSnake);
            }
            
            for (int i = 0; i < fireSnake->FireSnakeSegments; i++) {
                UpdateSegment(f, fireSnake->Segments[i], fireSnake->FireSnakeSegments);
            }

            var enemy = filter.Enemy;
            if (!enemy->IsAlive) {
                return;
            }

            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsTouchingGround) {
                physicsObject->Velocity = FPVector2.Zero;
                if (++fireSnake->JumpTimer == 20) {
                    // Time to jump
                    var transform = f.Unsafe.GetPointer<Transform2D>(filter.Entity);
                    var closestMario = QuantumUtils.FindClosestAliveMario(f, transform->Position, out FPVector2 closestMarioPosition, stage);
                    bool right;
                    if (closestMario != EntityRef.None) {
                        right = QuantumUtils.WrappedDirectionSign(stage, transform->Position, closestMarioPosition) == -1;
                    } else {
                        right = enemy->FacingRight;
                    }

                    enemy->FacingRight = right;
                    physicsObject->Velocity.X = fireSnake->JumpHorizontalSpeed * (right ? 1 : -1);
                    physicsObject->Velocity.Y = f.RNG->Next(0, 2) == 0 ? fireSnake->JumpHeightHigh : fireSnake->JumpHeightLow;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->WasTouchingGround = false;
                    fireSnake->JumpTimer = 0;
                }
            } else {
                if (physicsObject->IsTouchingLeftWall) {
                    enemy->FacingRight = true;
                    physicsObject->Velocity.X = fireSnake->JumpHorizontalSpeed;
                } else if (physicsObject->IsTouchingRightWall) {
                    enemy->FacingRight = false;
                    physicsObject->Velocity.X = -fireSnake->JumpHorizontalSpeed;
                }
                fireSnake->JumpTimer = 0;
            }
        }

        private void UpdateSegment(Frame f, EntityRef segment, int fireSnakeSegments) {
            var fireSnakeSegment = f.Unsafe.GetPointer<FireSnakeSegment>(segment);
            var transform = f.Unsafe.GetPointer<Transform2D>(segment);
            var parentTransform = f.Unsafe.GetPointer<Transform2D>(fireSnakeSegment->Parent);

            var buffer = fireSnakeSegment->PositionBuffer;
            int index = f.Number - fireSnakeSegment->SpawnTick;
            if (index >= fireSnakeSegments) {
                transform->Position = buffer[index % fireSnakeSegments];
            } else if (index == 0) {        
                transform->Position = parentTransform->Position;
            }
            buffer[index % fireSnakeSegments] = parentTransform->Position;
        }

        private void SpawnSegments(Frame f, EntityRef fireSnakeEntity, FireSnake* fireSnake) {
            for (int i = 0; i < fireSnake->Segments.Length; i++) {
                if (f.Exists(fireSnake->Segments[i]))
                f.Destroy(fireSnake->Segments[i]);
            }

            EntityRef parent = fireSnakeEntity;
            for (int i = 0; i < fireSnake->FireSnakeSegments; i++) {
                EntityRef newEntity = f.Create(fireSnake->SegmentPrototype);
                var newSegment = f.Unsafe.GetPointer<FireSnakeSegment>(newEntity);
                newSegment->FireSnake = fireSnakeEntity;
                newSegment->Parent = parent;
                newSegment->Index = (byte) i;
                newSegment->Reset(f, newEntity);
                fireSnake->Segments[i] = newEntity;
                parent = newEntity;
            }
        }

        private void OnFireSnakeMarioInteraction(Frame f, EntityRef fireSnakeEntity, EntityRef marioPlayerEntity) {
            f.Unsafe.GetPointer<MarioPlayer>(marioPlayerEntity)->Powerdown(f, marioPlayerEntity, false, fireSnakeEntity);
        }

        private void OnFireSnakeProjectileInteraction(Frame f, EntityRef fireSnakeEntity, EntityRef projectileEntity) {
            // Talk to my dad on what to do.
            if (f.Unsafe.TryGetPointer(fireSnakeEntity, out FireSnakeSegment* segment)) {
                fireSnakeEntity = segment->FireSnake;
            }

            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);
            bool hit = false;
            if (projectileAsset.Effect == ProjectileEffectType.Freeze) {
                f.Unsafe.GetPointer<FireSnake>(fireSnakeEntity)->Kill(f, fireSnakeEntity);
                hit = true;
            }
            if (hit || projectileAsset.DestroyOnHit) {
                f.Signals.OnProjectileHitEntity(projectileEntity, fireSnakeEntity);
            }
        }

        public void OnEnemyReturnedHome(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out FireSnake* fireSnake)) {
                if (f.Exists(fireSnake->Segments[0])) {
                    for (int i = 0; i < fireSnake->FireSnakeSegments; i++) {
                        EntityRef segment = fireSnake->Segments[i];
                        f.Unsafe.GetPointer<FireSnakeSegment>(segment)->Reset(f, fireSnake->Segments[i]);
                    }
                }
                fireSnake->JumpTimer = 0;
            }
        }

        public unsafe void OnAdded(Frame f, EntityRef entity, FireSnake* component) {
            SpawnSegments(f, entity, component);
        }

        public unsafe void OnRemoved(Frame f, EntityRef entity, FireSnake* component) {
            for (int i = 0; i < component->FireSnakeSegments; i++) {
                f.Destroy(component->Segments[i]);
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out FireSnake* fireSnake)) {
                fireSnake->Respawn(f, entity);
            }
        }
        //KKT mod
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, byte ExtraA, byte ExtraB, byte ExtraC, byte ExtraD) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out FireSnake* firesnack)
                || !f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)) {
                return;
            }

            enemy->IsActive = true;
            firesnack->FireSnakeSegments = ExtraA;
            SpawnSegments(f, thisEntity, firesnack);
        }
    }
}