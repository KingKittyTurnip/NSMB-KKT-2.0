namespace Quantum {
    public unsafe class EnemySystem : SystemMainThreadEntityFilter<Enemy, EnemySystem.Filter>, ISignalOnTryLiquidSplash, ISignalOnBeforeInteraction,
        ISignalOnEnemyDespawned, ISignalOnEnemyRespawned, ISignalOnMarioPlayerMegaMushroomFootstep {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public Hazard* Hazard;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var hazard = filter.Hazard;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            if (!hazard->IsActive) {
                return;
            }

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                enemy->IsDead = true;

                HazardSystem.DestroyHazard(f, filter.Entity);
                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
            }
        }

        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, true);
        }

        public static void EnemyBumpTurnaroundOnlyFirst(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, false);
        }

        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB, bool turnBoth) {
            var enemyA = f.Unsafe.GetPointer<Enemy>(entityA);
            var enemyB = f.Unsafe.GetPointer<Enemy>(entityB);
            var transformA = f.Unsafe.GetPointer<Transform2D>(entityA);
            var transformB = f.Unsafe.GetPointer<Transform2D>(entityB);

            QuantumUtils.UnwrapWorldLocations(f, transformA->Position, transformB->Position, out var ourPos, out var theirPos);
            bool right = ourPos.X > theirPos.X;
            if (ourPos.X == theirPos.X) {
                right = ourPos.Y < theirPos.Y;
            }
            enemyA->ChangeFacingRight(f, entityA, right);
            if (turnBoth) {
                enemyB->ChangeFacingRight(f, entityB, !right);
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquid, QBoolean exit, bool* doSplash) {
            if (f.Unsafe.TryGetPointer(entity, out Hazard* hazard)) {
                *doSplash &= hazard->IsActive;
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy) &&
                f.Unsafe.TryGetPointer(entity, out Hazard* hazard)) {
                *allowInteraction &= (!enemy->IsDead && hazard->IsActive);
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = false;
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = true;
            }
        }

        public void OnMarioPlayerMegaMushroomFootstep(Frame f) {
            var it = f.Unsafe.FilterStruct<Filter>();
            Filter filter = default;
            while (it.Next(&filter)) {
                var physicsObject = filter.PhysicsObject;
                if (!(!filter.Enemy->IsDead && filter.Hazard->IsActive)
                    || physicsObject->IsFrozen
                    || physicsObject->DisableCollision
                    || !physicsObject->IsTouchingGround) {
                    continue;
                }
                
                physicsObject->Velocity.Y = Constants._3_50;
                physicsObject->IsTouchingGround = false;
            }
        }
    }
}