using Photon.Deterministic;
using Quantum.Collections;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class FanSystem : SystemMainThreadFilterStage<FanSystem.Filter>, ISignalInitializeHazard {
/*
 ---------------------------------------

Fan Periodic Wind Changing

Fan Blade Spinning anim

wobbling anim

Wind Particles (attach to the fan, make it the size of the stage and increase emmission to copensate for larger sizes)

Make The Fan Malfunctioned If Groundpounded // Doesn't Change Direction and wobbles a bit more

Make Fan Become Broken If Hit By mega/metal/heavystone // blows upwards

Team interactions Make it Not Effect Teamate Objects


 ---------------------------------------
*/
        public struct Filter {
            public EntityRef Entity;
            public Fan* fan;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Fan>(f, OnClockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var fan = filter.fan;
            var entity = filter.Entity;

            var Objects = f.Filter<PhysicsObject>();
            while (Objects.NextUnsafe(out EntityRef OtherEntity, out PhysicsObject* physobj)) {
                if (physobj->DisableCollision || physobj->IsFrozen || physobj->WindImmune)
                    continue;
                f.Unsafe.TryGetPointer(OtherEntity, out Transform2D* trans);
                f.Unsafe.TryGetPointer(OtherEntity, out PhysicsCollider2D* col);
                PhysicsObjectSystem.Filter physicsSystemFilter = new PhysicsObjectSystem.Filter {
                        Entity = OtherEntity,
                        Transform = trans,
                        PhysicsObject = physobj,
                        Collider = col,
                    };
                //PhysicsObjectSystem.MoveHorizontally((FrameThreadSafe) f, new FPVector2(fan->Strength, 0), ref physicsSystemFilter, stage, null, out _);
                if (!physobj->IsTouchingGround)
                    PhysicsObjectSystem.MoveVertically((FrameThreadSafe) f, new FPVector2(0, fan->Strength), ref physicsSystemFilter, stage, null, out _);
            }
        }

        #region Interactions
        public static void OnClockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var clock = f.Unsafe.GetPointer<Clock>(thisEntity);

            //TODO: gp makes malfunctioned, mega/Metal Knocks it over making it blow upwards
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Fan* fan)) {
                return;
            }

            //Set Constant Direction
            fan->IsMalfunctioned = false; // Enable if hazard rules allow (Use Smoke Particles To Indicate)

            //Starting Diraction
            fan->FacingRight = false; // Rng this unless specified by hazard rules
        }
        #endregion
    }
}
