using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class TornadoSystem : SystemMainThreadEntityFilter<Tornado, TornadoSystem.Filter>, ISignalInitializeHazard {

        FP tornadoUpliftSpeed = FP._1_25;
        FP tornadoLaunchSpeed = 8;
        FP tornadoObjectAcceleration = FP._0_20;
        FP tornadoCone = 2;
        FP tornadoTop = Constants._2_50;
        FP ObjectMaxSpeed = 10;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Tornado* Tornado;
            public PhysicsCollider2D* Collider;
            public PhysicsObject* PhysicsObject;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, Tornado>(f, OnSpinnerMarioPlayerInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var tornado = filter.Tornado;
            var disTransform = filter.Transform;
            var disCollider = filter.Collider;
            var disPhysicsObject = filter.PhysicsObject;



            disPhysicsObject->Velocity.X = tornado->Speed;

            QHashSet<EntityRef> stuffInside = f.ResolveHashSet(tornado->EntitiesInside);
            foreach (var insideEntity in stuffInside) {
                if (f.Exists(insideEntity)) {
                    QuantumUtils.UnwrapWorldLocations(f, disTransform->Position, f.Unsafe.GetPointer<Transform2D>(insideEntity)->Position, out FPVector2 tornadoPos, out FPVector2 theirPos);
                    var theirPhysics = f.Unsafe.GetPointer<PhysicsObject>(insideEntity);
                    var theirCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(insideEntity);
                    var theirTransform = f.Unsafe.GetPointer<Transform2D>(insideEntity);

                    FP YMultiplier = (theirPos.Y - tornadoPos.Y)/tornadoCone;
                    bool ExitingNextFrame = theirPos.Y - theirCollider->Shape.Box.Extents.Y + theirCollider->Shape.Centroid.Y + (tornadoUpliftSpeed * f.DeltaTime) > tornadoPos.Y + tornadoTop;

                    theirPhysics->IsTouchingGround = false;

                    if (f.Unsafe.TryGetPointer<MarioPlayer>(insideEntity, out var mario)) {
                        mario->IsSpinnerFlying = !mario->IsInShell;
                        mario->IsPropellerFlying = mario->IsCrouching = mario->IsDrilling = mario->IsGroundpounding = false;
                        mario->JumpState = JumpState.SingleJump;
                        if (mario->IsInKnockback) {
                            mario->ResetKnockback(f, insideEntity);
                        }
                    }

                    if (ExitingNextFrame) {
                        theirPhysics->Velocity.Y = tornadoLaunchSpeed;
                        //f.Events.

                        if (mario != null) {
                            //make this controlled by input
                            Input inputs = mario->GetPlayerInput(f, insideEntity);
                            if (inputs.Right.IsDown ^ inputs.Left.IsDown) {
                                theirPhysics->Velocity.X = inputs.Right.IsDown ? 3 : -3;
                                continue;
                            }
                        }
                        theirPhysics->Velocity.X = theirPhysics->Velocity.X > 0 ? 3 : -3;
                    } else {
                        theirPhysics->Velocity.Y = tornadoUpliftSpeed;
                        FP absX = FPMath.Abs(theirPos.X - tornadoPos.X);
                        FP Overshot = YMultiplier > absX ? (theirPhysics->Velocity.X > 0 ? tornadoObjectAcceleration : -tornadoObjectAcceleration) : (FPMath.Clamp(tornadoPos.X - theirPos.X, -tornadoObjectAcceleration, tornadoObjectAcceleration) * 6);
                        theirPhysics->Velocity.X = FPMath.Clamp(theirPhysics->Velocity.X + Overshot, -ObjectMaxSpeed, ObjectMaxSpeed);
                    }
                }
            }

            stuffInside.Clear();
        }

        public static void OnSpinnerMarioPlayerInteraction(Frame f, EntityRef otherEntity, EntityRef tornadoEntity) {
            if (!f.Unsafe.TryGetPointer<PhysicsObject>(otherEntity, out var phys) || phys->WindImmune || phys->IsFrozen || 
                f.Has<Projectile>(otherEntity)) {
                return;
            }

            var hazard = f.Unsafe.GetPointer<Hazard>(tornadoEntity);
            if (hazard->IsHazard && hazard->LifeTime < 45) {
                return;
            }

            var tornado = f.Unsafe.GetPointer<Tornado>(tornadoEntity);
            QHashSet<EntityRef> mariosSet = f.ResolveHashSet(tornado->EntitiesInside);

            mariosSet.Add(otherEntity);
            return;
        }

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Tornado* tornado)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            //decide random speed
            int rng = f.RNG->Next(-2, 3);
            if (rng <= 0) {
                rng--;
            }
            tornado->Speed = ((FP) rng) * FP._0_50;

            //place on ground 
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (PhysicsObjectSystem.Raycast(f, stage, transform->Position, FPVector2.Down, 2, out var hit)) {
                transform->Position.Y = hit.Position.Y;
            } else {
                transform->Position.Y -= 2;
            }
        }
        #endregion
    }
}