using JetBrains.Annotations;
using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using static Quantum.CurrentHazards.HazardDataList;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class SpinpipeSystem : SystemMainThreadFilterStage<SpinpipeSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Spinpipe* spinpipe;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            //f.Context.Interactions.Register<MarioPlayer, Fan>(f, OnFanMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var spinpipe = filter.spinpipe;
            var entity = filter.Entity;
            var hazard = filter.hazard;
            var physicsObject = filter.PhysicsObject;

            //Handle Start And End Tipping
            if (hazard->LifeTime <= 60) {
                spinpipe->Active = false;
                physicsObject->Velocity.Y = 8;
                physicsObject->IsFrozen = hazard->LifeTime <= 40;
            } else if (!spinpipe->Active && (physicsObject->IsTouchingGround || spinpipe->groundDelay > 0)) {
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

            //Spinpipe Can Push
            // Multiple Pipes optimization
            var ExistingPipes = f.Filter<Spinpipe>();
            bool MainPipeChecked = false, IsMainPipe = false, SpinpipeUnactive = false;
            Spinpipe* Captain = spinpipe;
            byte TempStrength = 0;
            while (ExistingPipes.NextUnsafe(out EntityRef OtherEntity, out Spinpipe* Aspinpipe)) {

                if (!MainPipeChecked) { // We Have Not Found A Spinpipe Yet
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
                        if (QuantumUtils.Decrement(ref Captain->TipTime)) {
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

            if (!IsMainPipe)
                return;
            if (TempStrength == 0 && SpinpipeUnactive) {
                //stop all tipping
                FP intestity = FPMath.Max((f.Global->SpinpipeSlope * 3) - hazard->LifeTime, 0);
                f.Global->SpinpipeSlope = f.Global->SpinpipeSlope > 0 ? -intestity : intestity;
                if (FPMath.Abs(f.Global->SpinpipeSlope) < 1 || hazard->LifeTime <= 1) //2nd check is a failsafe
                    f.Global->SpinpipeSlope = 0;
            } else {
                //tip the stage
                FP spinpipeturncap = FPMath.Max(32 - (TempStrength * 4), 5);
                if (TempStrength > 4) {
                    TempStrength = 4;
                }
                f.Global->SpinpipeSlope = FPMath.Clamp(f.Global->SpinpipeSlope + ((FP._0_25 + (TempStrength * FP._0_10)) * (spinpipe->Right ? 1 : -1)), -spinpipeturncap, spinpipeturncap);
            }

        }

        #region Interactions
        public static bool OnFanMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            #region SetValues
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var fan = f.Unsafe.GetPointer<Fan>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity); 
            var DisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var DisCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
            var mariophys = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            QuantumUtils.UnwrapWorldLocations(f, DisTransform->Position + ((DisCollider->Shape.Centroid.Y - DisCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            FP upDot = FPVector2.Dot(damageDirection, FPVector2.Up);
            #endregion

            if (mario->CurrentPowerupState == PowerupState.MegaMushroom && !fan->Sturdy) { //TODO: Add Metal
                if (hazard->LifeTime > 1200)
                    hazard->LifeTime = 1200;
                physicsObject->IsFrozen = physicsObject->DisableCollision = f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                fan->FellOver = fan->Broken = true;
                fan->FanTime = fan->TurnEffectorDowntime = 90;
                fan->FacingRight = false;
                DisCollider->Enabled = false;
                DisCollider->Shape.Box.Extents = FPVector2.Zero;
                DisCollider->Shape.Centroid.Y = -999;
                f.Events.OnFanHit(thisEntity, true);
                return false;
            } else if (upDot >= Constants.PhysicsGroundMaxAngleCos && (mario->IsGroundpounding || mario->GroundpoundStandFrames > 0) && !fan->Sturdy) {
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = damageDirection.X > 0 ? -2 : 2;
                physicsObject->Velocity.Y = 3;
                fan->Broken = true;
                fan->FanTime = 90;
                mario->IsGroundpounding = mariophys->IsTouchingGround = false;
                mariophys->Velocity.Y = 6;
                f.Events.OnFanHit(thisEntity, false);
                return true;
            } else if (upDot <= -Constants.PhysicsGroundMaxAngleCos) {
                physicsObject->IsTouchingGround = false;
                physicsObject->Velocity.X = damageDirection.X > 0 ? -2 : 2;
                physicsObject->Velocity.Y = 3;
            }
            return false;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Spinpipe* spinpipe)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            //Set Constant Direction
            spinpipe->FellOver = false;

            //Starting Direction
            spinpipe->Right = (f.RNG->Next() >= FP._0_50);

            //Set FanTime
            spinpipe->TipTime = 10 * 59; // set to Basically 10 seconds
            f.Unsafe.GetPointer<PhysicsObject>(thisEntity)->Velocity.Y = 5;
        }
        #endregion
    }
}
