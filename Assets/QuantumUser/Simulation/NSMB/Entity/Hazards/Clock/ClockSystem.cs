using Photon.Deterministic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class ClockSystem : SystemMainThreadFilterStage<ClockSystem.Filter>, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Clock* clock;
            public Holdable* holdable;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Clock>(f, OnClockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var clock = filter.clock;

            if (clock->Collected) {
                QuantumUtils.Decrement(ref clock->TimeTillKill);
                if (clock->TimeTillKill <= 0) {
                    f.Destroy(filter.Entity);
                }
            }

        }

        #region Interactions
        public static void OnClockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            var clock = f.Unsafe.GetPointer<Clock>(thisEntity);

            clock->Collected = true;
            clock->TimeTillKill = 30;
            // += clock->Time;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            //Set Timeup

            //Set ResetTime

            //SetTime

            //SetColor
        }
        #endregion
    }
}
