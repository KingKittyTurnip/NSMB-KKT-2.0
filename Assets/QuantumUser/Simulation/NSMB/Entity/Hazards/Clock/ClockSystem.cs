using Photon.Deterministic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class ClockSystem : SystemMainThreadFilterStage<ClockSystem.Filter>, ISignalInitializeHazard {
/*
 ---------------------------------------

Fix The Font Issues With The Clock particles - (Modify Spritesheet)

Make Timer UI Pulse And Blink When Time Changed - (Add An Animation And Event For It)

 ---------------------------------------
*/
        public struct Filter {
            public EntityRef Entity;
            public Clock* clock;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Clock>(f, OnClockMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
        }

        #region Interactions
        public static void OnClockMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var clock = f.Unsafe.GetPointer<Clock>(thisEntity);

            // Change GlobalTime
            if (f.Global->Timer == 0) {
                //Clocks Can't be Collected During Overtime
            } else if (clock->TickTimeup) {
                f.Global->Timer = 10;
            } else if (clock->ResetTime) {
                f.Global->Timer = clock->Time = f.Global->Rules.TimerSeconds * f.UpdateRate;
            } else {
                f.Global->Timer += clock->Time;
                f.Global->Timer = FPMath.Clamp(f.Global->Timer, 1, f.Global->Rules.TimerSeconds * f.UpdateRate);
            }

            f.Events.ClockCollect(thisEntity, f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position, clock->Time, clock->ResetTime, clock->TickTimeup, f.Global->Timer == 0);
            f.Destroy(thisEntity);
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set TickTimeup
            clock->TickTimeup = false;

            //Set ResetTime
            clock->ResetTime = false;

            //SetTime
            clock->Time = 10;
        }
        #endregion
    }
}
