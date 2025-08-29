using Photon.Deterministic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class StarballSystem : SystemMainThreadFilterStage<StarballSystem.Filter>, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Starball* Starball;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Starball>(f, OnStarballMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var starball = filter.Starball;
            var physicsObject = filter.PhysicsObject;

            //TODO: Place This Code Elsewhere
            //(This Code Makes It Collide With The Terrain Correctly, Since it only cares about the Box bounds)
            var collider = filter.Collider;
            collider->Shape.Box.Extents = new FPVector2(Constants._0_40, Constants._0_40); //new FPVector2(FP._0_01, FP._0_50);
            collider->Shape.Circle.Radius = FP._0_50;


            if (starball->Rider != EntityRef.None) {
                physicsObject->Velocity.X = 3; //Move
                if (false)
                    physicsObject->Velocity.Y = 7;
            } else {
                physicsObject->Velocity.X *= FP._0_99;
            }
            if (physicsObject->IsTouchingGround && physicsObject->FloorAngle != 0) {
                if (!physicsObject->WasTouchingGround) {
                    physicsObject->Velocity.X = -Constants.WeirdSlopeConstant * physicsObject->FloorAngle * 4;
                    //SOUND EVENT
                }
                physicsObject->Velocity.X -= (Constants.WeirdSlopeConstant * physicsObject->FloorAngle) * FP._0_10;
            }
        }

        #region Interactions
        public static void OnStarballMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var starball = f.Unsafe.GetPointer<Starball>(thisEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            UnityEngine.Debug.Log("Touched mario");
            if (starball->Rider == marioEntity) {
                //Wait, This is OUR Mario!
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            QuantumUtils.UnwrapWorldLocations(f, marioTransform->Position, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            //Try Make Rider
            if (starball->Rider == EntityRef.None && FPVector2.Dot(damageDirection, FPVector2.Down) > FP._0_25 && mario->KnockbackGetupFrames <= 0 && mario->CurrentKnockback == KnockbackStrength.None) {
                UnityEngine.Debug.Log("Set As Rider");
                starball->Rider = marioEntity;
                return;
            }

            //Try Bonk Other Players
            if (attackFromAbove) {
                physicsObject->Velocity.Y = 7;
            } else {
                physicsObject->Velocity.X = (damageDirection.X < 0 ? -3 : 3);
            }
            if (mario->IsDamageable) {
                mario->DoKnockback(f, marioEntity, damageDirection.X >= 0, 1, attackFromAbove ? KnockbackStrength.Groundpound : KnockbackStrength.Normal, thisEntity, false);
            }

        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set Container
        }
        #endregion
    }
}
