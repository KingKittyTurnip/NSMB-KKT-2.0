using Photon.Deterministic;

namespace Quantum {
    
    public unsafe class CloudBillPlatformSystem : SystemMainThreadFilterStage<CloudBillPlatformSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public CloudBillPlatform* cloud;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, CloudBillPlatform>(f, OnCloudBillPlatformMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
        }

        #region Interactions
        public static void OnCloudBillPlatformMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity) {
            var clock = f.Unsafe.GetPointer<Clock>(thisEntity);
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

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
    }
}
