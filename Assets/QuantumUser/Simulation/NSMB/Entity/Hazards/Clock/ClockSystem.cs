using Photon.Deterministic;

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
            } else if (clock->TickTimeup) {
                f.Global->Timer = 10 + FP._0_10;
            } else if (clock->ResetTime) {
                f.Global->Timer = clock->Time = f.Global->Rules.TimerMinutes * f.UpdateRate;
            } else {
                f.Global->Timer += clock->Time;
                f.Global->Timer = FPMath.Clamp(f.Global->Timer, FP._0_50, f.Global->Rules.TimerMinutes * f.UpdateRate);
            }

            f.Events.ClockCollect(thisEntity, f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position, clock->Time, clock->ResetTime, clock->TickTimeup, f.Global->Timer == 0);

            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            if (hazard->IsHazard && hazard->RestrictSpawnPosition) {
                f.Global->UsedHazardSpawns.Clear(hazard->index);
                f.Global->UsedHazardSpawnCount--;
            }
            f.Destroy(thisEntity);
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Clock* clock)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            //Set TickTimeup
            clock->TickTimeup = hazardata.SpecialValues[0].BaseValue == 2;

            //Set ResetTime
            clock->ResetTime = hazardata.SpecialValues[0].BaseValue == 1;

            //SetTime
            clock->Time = hazardata.SpecialValues[0].BaseValue == 0 ? 10 : -10;
        }
        #endregion
    }
}
