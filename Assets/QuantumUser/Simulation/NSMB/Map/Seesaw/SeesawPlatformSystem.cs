using Photon.Deterministic;

namespace Quantum {
    public unsafe class SeesawSystem : SystemMainThreadEntityFilter<Seesaw, SeesawSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Seesaw* Seesaw;
            public PhysicsCollider2D* Collider;
            //public MovingPlatform* MovingPlatform;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, Seesaw>(f, OnSeesawAnythingInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var seesaw = filter.Seesaw;
            //var movingPlatform = filter.MovingPlatform;
            var collider = filter.Collider;

            //Calculate velocity
            var amount = seesaw->WeightOnLift == 0 ? (transform->Rotation > 0 ? FP._0_03 : -FP._0_03) : seesaw->WeightOnLift;
            var cap = FPMath.Max(FPMath.Abs(seesaw->Velocity) - FP._0_03, seesaw->WeightOnLift == 0 ? seesaw->TerminalVelocity/2 : seesaw->TerminalVelocity);
            var accel = new FPVector2(amount, 0).Normalized.X * seesaw->Acceleration;
            seesaw->Velocity = FPMath.Clamp(seesaw->Velocity - accel, -cap, cap);

            //set rot
            transform->Rotation = FPMath.Clamp(transform->Rotation + (seesaw->Velocity * f.DeltaTime), -seesaw->MaxTipping, seesaw->MaxTipping);

            //bounce back
            if (FPMath.Abs(transform->Rotation) == seesaw->MaxTipping) {
                seesaw->Velocity = seesaw->Velocity * -FP._0_50;
            }

            //movingPlatform->Velocity.Y = FPMath.Abs(seesaw->Velocity) / FP.Pi;

            seesaw->WeightOnLift = 0;
            seesaw->Delay = 5;
        }

        public static bool OnSeesawAnythingInteraction(Frame f, EntityRef otherEntity, EntityRef scaleEntity, PhysicsContact contact) {
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(scaleEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var seesaw = f.Unsafe.GetPointer<Seesaw>(scaleEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);

            //add more weight if farther away
            //add a weight variable for specific objects to contribute moooore to the swaying?
            seesaw->WeightOnLift += theirPos.X - ourPos.X;
            return false;
        }
    }
}