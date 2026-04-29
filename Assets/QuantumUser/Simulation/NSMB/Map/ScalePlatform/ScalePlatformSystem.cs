using Photon.Deterministic;

namespace Quantum {
    public unsafe class ScalePlatformSystem : SystemMainThreadEntityFilter<ScalePlatform, ScalePlatformSystem.Filter> {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public ScalePlatform* ScalePlatform;
            public PhysicsCollider2D* Collider;
            public MovingPlatform* MovingPlatform;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, ScalePlatform>(f, OnScalePlatformAnythingInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var scaleplatform = filter.ScalePlatform;
            var movingPlatform = filter.MovingPlatform;
            var collider = filter.Collider;

            if (scaleplatform->WeightOnLift == 0 && scaleplatform->Timer == 0) {
                //do nothing
                if (QuantumUtils.Decrement(ref scaleplatform->Delay)) {
                    scaleplatform->Velocity = 0;
                    movingPlatform->Velocity.Y = 0;
                    UnityEngine.Debug.Log("nothing");
                }
            } else {
                UnityEngine.Debug.Log("scooties" + scaleplatform->WeightOnLift);
                FP Bonus = 0;
                if (scaleplatform->Timer == 0) {
                    var amount = new FPVector2(scaleplatform->WeightOnLift, 0).Normalized.X * scaleplatform->Acceleration;
                    scaleplatform->Velocity = FPMath.Max(scaleplatform->Velocity - amount, scaleplatform->TerminalVelocity);
                    scaleplatform->Offset += scaleplatform->Velocity * f.DeltaTime;

                    if (FPMath.Abs(scaleplatform->Offset) >= scaleplatform->Height*2) {
                        scaleplatform->Timer = 1;
                        scaleplatform->Offset = (scaleplatform->Offset > 0 ? 2 : -2) * scaleplatform->Height;
                    }
                } else if (scaleplatform->Timer++ > scaleplatform->PlatformbreakTime) {
                    Bonus = (scaleplatform->Timer - scaleplatform->PlatformbreakTime) * f.DeltaTime * 5;
                }

                //set location of platforms
                collider->Shape.Compound.GetShapes(f, out var shape, out int count);
                if (count == 2) {
                    shape[0].Centroid = new FPVector2(-scaleplatform->Length, -scaleplatform->Height - scaleplatform->Offset - Bonus);
                    shape[1].Centroid = new FPVector2(scaleplatform->Length, -scaleplatform->Height + scaleplatform->Offset - Bonus);
                } else {
                    UnityEngine.Debug.Log("Dude it MUST be 2 platforms not " + count);
                }

                movingPlatform->Velocity.Y = FPMath.Abs(scaleplatform->Velocity);

                scaleplatform->WeightOnLift = 0;
                scaleplatform->Delay = 5;
            }

            //Temp: remove later
            if (scaleplatform->Timer > 280) {
                ResetPlat(f, filter.Entity);
            }
        }

        //TODO: respawn on stage reset

        public void ResetPlat(Frame f, EntityRef scaleEntity) {
            var scaleplatform = f.Unsafe.GetPointer<ScalePlatform>(scaleEntity);
            var movingPlatform = f.Unsafe.GetPointer<MovingPlatform>(scaleEntity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(scaleEntity);

            scaleplatform->Offset = scaleplatform->Startoffset;
            collider->Shape.Compound.GetShapes(f, out var shape, out int count);
            if (count == 2) {
                shape[0].Centroid = new FPVector2(-scaleplatform->Length, -scaleplatform->Height - scaleplatform->Offset);
                shape[1].Centroid = new FPVector2(scaleplatform->Length, -scaleplatform->Height + scaleplatform->Offset);
            } else {
                UnityEngine.Debug.Log("Dude it MUST be 2 platforms not " + count);
            }
            scaleplatform->WeightOnLift = 0;

            scaleplatform->Timer = 0;
            scaleplatform->Delay = 0;

            scaleplatform->Velocity = 0;
            movingPlatform->Velocity.Y = 0;
        }

        public static bool OnScalePlatformAnythingInteraction(Frame f, EntityRef otherEntity, EntityRef scaleEntity, PhysicsContact contact) {
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(scaleEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);

            var scaleplatform = f.Unsafe.GetPointer<ScalePlatform>(scaleEntity);
            if (theirPos.X - ourPos.X > 0) {
                scaleplatform->WeightOnLift++;
            } else {
                scaleplatform->WeightOnLift--;
            }
            return false;
        }
    }
}