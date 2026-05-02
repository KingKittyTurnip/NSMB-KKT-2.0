using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe class ScalePlatformSystem : SystemMainThreadEntityFilter<ScalePlatform, ScalePlatformSystem.Filter>, ISignalOnStageReset {

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

            if (scaleplatform->Timer > 600) {
                //actually do nothing we are out of bounds, probably, i'm too lazy to add in a check
            } else if (scaleplatform->WeightOnLift == 0 && scaleplatform->Timer == 0) {
                //do nothing
                if (scaleplatform->Delay > 0 && QuantumUtils.Decrement(ref scaleplatform->Delay)) {
                    scaleplatform->Velocity = 0;
                    movingPlatform->Velocity.Y = 0;
                    collider->Shape.Compound.GetShapes(f, out var shape, out int count);
                    shape[0].Centroid = new FPVector2(-scaleplatform->Length, -scaleplatform->Height - scaleplatform->Offset);
                    shape[1].Centroid = new FPVector2(scaleplatform->Length, -scaleplatform->Height + scaleplatform->Offset);
                }
            } else if (scaleplatform->WeightIsEven) {
                scaleplatform->WeightIsEven = false;
            } else {
                //tip the scales
                FP Bonus = 0;
                if (scaleplatform->Timer == 0) {
                    //calculate platform velocity
                    var amount = new FPVector2(scaleplatform->WeightOnLift, 0).Normalized.X * scaleplatform->Acceleration;
                    scaleplatform->Velocity = FPMath.Max(scaleplatform->Velocity - amount, scaleplatform->TerminalVelocity);
                    scaleplatform->Offset += scaleplatform->Velocity * f.DeltaTime;

                    if (FPMath.Abs(scaleplatform->Offset) >= scaleplatform->Height) {
                        scaleplatform->Timer = 1;
                        scaleplatform->Offset = (scaleplatform->Offset > 0 ? 1 : -1) * scaleplatform->Height;
                        f.Events.ScaleplatformStepped(filter.Entity, false, true);
                    }
                } else if (scaleplatform->Timer++ > scaleplatform->PlatformbreakTime) {
                    //scale is broken and currently falling
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

                scaleplatform->WeightIsEven = false;
                scaleplatform->WeightOnLift = 0;
                scaleplatform->Delay = 5;
            }
        }

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

        #region Interactions
        public static bool OnScalePlatformAnythingInteraction(Frame f, EntityRef otherEntity, EntityRef scaleEntity, PhysicsContact contact) {
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(scaleEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var scaleplatform = f.Unsafe.GetPointer<ScalePlatform>(scaleEntity);
            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);

            //i mean we could make this check better but scalelifts never were that close together in the first place, a simple check works enough
            bool RightPlatform = theirPos.X - ourPos.X > 0;

            if (RightPlatform) {
                scaleplatform->WeightOnLift++;
            } else {
                scaleplatform->WeightOnLift--;
            }

            if (scaleplatform->Velocity == 0 && !scaleplatform->WeightIsEven) {
                scaleplatform->WeightIsEven = scaleplatform->WeightOnLift == 0;
                f.Events.ScaleplatformStepped(scaleEntity, RightPlatform, false);
            }
            return false;
        }
        #endregion

        #region Signals
        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<ScalePlatform>();
            while (filter.NextUnsafe(out EntityRef entity, out ScalePlatform* scaleplatform)) {
                if (full || scaleplatform->Timer > 180) {
                    //reset the platform at the start of the match, or if it's been broken for 3 seconds
                    ResetPlat(f, entity);
                }
            }
        }
        #endregion
    }
}