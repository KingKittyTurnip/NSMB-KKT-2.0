namespace Quantum {

    public unsafe class HazardSystem : SystemMainThreadFilterStage<HazardSystem.Filter>, ISignalOnEnemyDespawned {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;

            public Hazard* Hazard;

            public Holdable* Holdable;
            public Freezable* Freezable;

            public Enemy* Enemy;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var hazard = filter.Hazard;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            // Countdown To Despawn
            //if (hazard) {
            
            //}

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            f.Unsafe.TryGetPointer(entity, out Hazard* hazard);
            if (hazard->IsHazard)
                f.Destroy(entity);
        }
    }
}