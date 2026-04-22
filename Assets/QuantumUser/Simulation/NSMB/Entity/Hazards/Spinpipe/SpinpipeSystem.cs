using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;

namespace Quantum {
    
    public unsafe class SpinpipeSystem : SystemMainThreadFilterStage<SpinpipeSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Spinpipe* spinpipe;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;

            public CoinItem* CoinItem;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, Spinpipe>(f, OnSpinpipeMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var spinpipe = filter.spinpipe;
            var entity = filter.Entity;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;
            var coinitem = filter.CoinItem;
            var collider = filter.Collider;

            //Hacky Fix...
            if (coinitem->SpawnAnimationFrames == 1) {
                physicsObject->DisableCollision = false;
            }

            //Handle Start And End Tipping
            if (hazard->LifeTime <= 60) {
                if (spinpipe->Active)
                    f.Events.SpinpipeLand(filter.Entity, true);
                spinpipe->Active = false;
                if (!spinpipe->Broken)
                    physicsObject->Velocity.Y = 6;
                physicsObject->IsFrozen = hazard->LifeTime <= 40;
            } else if (!spinpipe->Active && (physicsObject->IsTouchingGround || spinpipe->groundDelay > 0)) {
                if (spinpipe->groundDelay <= 0)
                    f.Events.SpinpipeLand(filter.Entity, false);
                physicsObject->DisableCollision = true;
                spinpipe->groundDelay++;
                physicsObject->Velocity.Y = -1;
                physicsObject->Gravity.Y = 0;
                if (spinpipe->groundDelay > 30) {
                    physicsObject->IsFrozen = true;
                    if (spinpipe->groundDelay > 80) {
                        physicsObject->Velocity.Y = 0;
                        spinpipe->Active = true;
                    }
                }
            }
            collider->Shape.Centroid = (physicsObject->IsFrozen && !(spinpipe->Active || spinpipe->groundDelay > 0)) || physicsObject->Velocity.Y > 0 ? new FPVector2(0, 999) : spinpipe->Broken ? new FPVector2(0, -FP._0_50) : FPVector2.Zero;

            //Spinpipe Can Push
            // Multiple Pipes optimization
            var ExistingPipes = f.Filter<Spinpipe>();
            bool MainPipeChecked = false, IsMainPipe = false, SpinpipeUnactive = false;
            Spinpipe* Captain = spinpipe;
            byte TempStrength = 0;
            while (ExistingPipes.NextUnsafe(out EntityRef OtherEntity, out Spinpipe* Aspinpipe)) {

                if (!MainPipeChecked) { // We Have Not Found A Spinpipe Yet
                    if (Aspinpipe->Broken) {
                        continue;
                    }
                    MainPipeChecked = true;
                    if (spinpipe != Aspinpipe) {
                        return;
                    }

                    // I Am The Captain Now
                    IsMainPipe = true;
                    Captain = spinpipe;
                }
                if (IsMainPipe) { // If Captain, Then Link All Other "Similar" Fans To It
                    if (Aspinpipe->Active) {
                        TempStrength++;
                        if (QuantumUtils.Decrement(ref Captain->TipTime) && !Aspinpipe->Broken) {
                            Captain->Right = !Captain->Right;
                            Captain->TipTime = 10 * 59;
                        }
                    } else { // Sync Other Spinpipe
                        SpinpipeUnactive = true;
                        Aspinpipe->TipTime = Captain->TipTime;
                        Aspinpipe->Right = Captain->Right;
                    }
                }
                continue;
            }

            //if (!IsMainPipe)
            //    return;
            if (TempStrength == 0 && SpinpipeUnactive) {
                //stop all tipping
                FP intestity = FPMath.Max((f.Global->SpinpipeSlope * 3) - hazard->LifeTime, 0);
                f.Global->SpinpipeSlope = f.Global->SpinpipeSlope > 0 ? -intestity : intestity;
                if (FPMath.Abs(f.Global->SpinpipeSlope) < 1 || hazard->LifeTime <= 1) //2nd check is a failsafe
                    f.Global->SpinpipeSlope = 0;
            } else {
                //tip the stage
                f.Global->SpinpipeMAX = FPMath.Max(32 - (TempStrength * 4), 10);
                if (TempStrength > 4) {
                    TempStrength = 4;
                }
                f.Global->SpinpipeSlope = FPMath.Clamp(f.Global->SpinpipeSlope + ((FP._0_25 + (TempStrength * FP._0_05)) * (spinpipe->Right ? 1 : -1)), -f.Global->SpinpipeMAX, f.Global->SpinpipeMAX);
            }
            //f.DeltaTime = FP._0_02;
        }

        #region Interactions
        public static bool OnSpinpipeMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            #region SetValues
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var spinpipe = f.Unsafe.GetPointer<Spinpipe>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            //var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            //var mariophys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            #endregion

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom && !spinpipe->Broken) { //TODO: Add Metal
                if (spinpipe->Sturdy) {
                    f.Events.IsNowResistantHit(f.Number, thisEntity);
                } else {
                    if (hazard->LifeTime > 1200)
                        hazard->LifeTime = 1200;
                    spinpipe->Broken = true;
                    DisCollider->Shape.Box.Extents = new FPVector2(DisCollider->Shape.Box.Extents.X, FP._0_50);
                    DisCollider->Shape.Centroid.Y = -FP._0_50;
                    f.Events.SpinpipeDestroy(f, thisEntity, damageDirection.X > 0);
                    return false;
                }
            }
            return false;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Spinpipe* spinpipe)) {
                return;
            }
            var specialValues = f.ResolveList(spawnData);

            spinpipe->Sturdy = specialValues[0] == 2;
            //Set Constant Direction
            spinpipe->Broken = specialValues[0] == 1;
            if (spinpipe->Broken) {
                var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
                DisCollider->Shape.Box.Extents = new FPVector2(DisCollider->Shape.Box.Extents.X, FP._0_50);
            }

            //Starting Direction
            spinpipe->Right = specialValues[1] == 0 ? (f.RNG->Next() >= FP._0_50) : specialValues[1] == 2;

            //Set FanTime
            spinpipe->TipTime = 10 * 59; // set to Basically 10 seconds
            f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.Y = 5;
        }
        #endregion
    }
}
