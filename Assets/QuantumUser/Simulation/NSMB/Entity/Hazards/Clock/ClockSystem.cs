using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    
    public unsafe class ClockSystem : SystemMainThreadFilterStage<ClockSystem.Filter>, ISignalInitializeHazard {
/*
 ---------------------------------------

Fix The Font Issues With The Clock particles - (Modify Spritesheet)

Make Timer UI Pulse And Blink When Time Changed - (Add An Animation And Event For It)

Use The Correct Sound For Collection(?)

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
            } else {
                switch (clock->type) {
                case ClockType.Add:
                    f.Global->Timer = FPMath.Min(f.Global->Timer + clock->Time, f.Global->Rules.TimerMinutes * f.UpdateRate);
                    break;
                case ClockType.Subtract:
                    f.Global->Timer = FPMath.Max(f.Global->Timer - clock->Time, FP._0_50);
                    break;
                case ClockType.Timeup:
                    f.Global->Timer = clock->Time;
                    break;
                case ClockType.Reset:
                    f.Global->Timer = clock->Time = f.Global->Rules.TimerMinutes * f.UpdateRate;
                    break;
                }
            }

            f.Events.ClockCollect(thisEntity, f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position, clock->Time, clock->type, f.Global->Timer == 0);

            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                f.Global->UsedHazardSpawns.Clear(hazard->index);
                f.Global->UsedHazardSpawnCount--;
            }
            f.Destroy(thisEntity);
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, byte ExtraA, byte ExtraB, byte ExtraC, byte ExtraD) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            //Set Timer
            clock->Time = (ExtraA+1) * 10;

            clock->type = (ClockType) ExtraB;
        }
        #endregion
    }
}