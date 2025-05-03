using Photon.Deterministic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class ClockSystem : SystemMainThreadFilterStage<ClockSystem.Filter>, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Clock* clock;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Clock>(f, OnClockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var clock = filter.clock;

            if (clock->Collected) {
                UnityEngine.Debug.Log("Kill Time!");
                QuantumUtils.Decrement(ref clock->TimeTillKill);
                if (clock->TimeTillKill <= 0) {
                    f.Destroy(filter.Entity);
                }
            }

        }

        #region Interactions
        public static void OnClockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var clock = f.Unsafe.GetPointer<Clock>(thisEntity);
            if (clock->Collected)
                return;

            clock->Collected = true;
            clock->TimeTillKill = 30;
            // += clock->Time;
            f.Events.ClockCollect(thisEntity, f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position, clock->Time, clock->ResetTime, clock->TickTimeup);
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set Timeup
            clock->TickTimeup = false;

            //Set ResetTime
            clock->ResetTime = false;

            //SetTime
            clock->Time = 10;

            //SetColor
        }
        #endregion
    }
}
