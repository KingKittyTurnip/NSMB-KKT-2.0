using Photon.Deterministic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace Quantum {
    
    public unsafe class StarballgoalSystem : SystemMainThreadFilterStage<StarballgoalSystem.Filter> {
        private static readonly FP HoverArea = FP.FromString("0.53");
        public struct Filter {
            public EntityRef Entity;
            public Starballgoal* Starballgoal;
            public Transform2D* Transform;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Starballgoal, Starball>(f, OnStarballGoalInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var starballgoal = filter.Starballgoal;
            
            if (starballgoal->CaughtStarBall != EntityRef.None) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(starballgoal->CaughtStarBall);
                var ballTransform = f.Unsafe.GetPointer<Transform2D>(starballgoal->CaughtStarBall); var DisTransform = f.Unsafe.GetPointer<Transform2D>(filter.Entity);
                QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, ballTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                physicsObject->Velocity.X += (theirPos - ourPos).Normalized.X > 0 ? -1 : 1;
                physicsObject->Velocity.X *= Constants._0_90;
                physicsObject->Velocity.Y = 0;
                ballTransform->Position.Y += ((DisTransform->Position.Y + HoverArea) - ballTransform->Position.Y) * FP._0_50;
                starballgoal->DespawnTimer++;
                if (starballgoal->DespawnTimer > 60) {
                    StarballSystem.BreakOpenStarball(f, starballgoal->CaughtStarBall, filter.Entity);
                    starballgoal->DespawnTimer = 122;
                    f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                    f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity)->Enabled = false;
                    starballgoal->CaughtStarBall = EntityRef.None;
                }
                return;
            }

            starballgoal->DespawnTimer++;
            if (starballgoal->DespawnTimer > 121) {
                if (starballgoal->DespawnTimer == 122) {
                    f.Unsafe.GetPointer<Interactable>(filter.Entity)->ColliderDisabled = true;
                    f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity)->Enabled = false;
                    f.Events.StarBallDestroyed(EntityRef.None, filter.Entity);
                }
                if (starballgoal->DespawnTimer > 161)
                    f.Destroy(filter.Entity);
            } else {
                var Objects = f.Filter<Starball>();
                while (Objects.NextUnsafe(out EntityRef OtherEntity, out Starball* starball)) {
                    if (starball->Rider != EntityRef.None) {
                        starballgoal->DespawnTimer = 0;
                    }
                }
            }
        }

        public static void OnStarballGoalInteraction(Frame f, EntityRef goalEntity, EntityRef starballEntity) {
            var starball = f.Unsafe.GetPointer<Starball>(starballEntity);
            var starballgoal = f.Unsafe.GetPointer<Starballgoal>(goalEntity);
            if (starball->Rider == EntityRef.None) {
                //Only Riders
                return;
            }

            var ballTransform = f.Unsafe.GetPointer<Transform2D>(starballEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(goalEntity);
            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position, ballTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;

            if (FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_50) {
                starballgoal->CaughtStarBall = starballEntity;
                f.Unsafe.GetPointer<PhysicsCollider2D>(goalEntity)->Enabled = false;
                starballgoal->DespawnTimer = 0;
                f.Unsafe.GetPointer<Interactable>(goalEntity)->ColliderDisabled = true;
            }
        }
    }
}
