using JetBrains.Annotations;
using Photon.Deterministic;
using Quantum.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using static Quantum.CurrentHazards;
using static Quantum.CurrentHazards.HazardDataList;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class CauldronSystem : SystemMainThreadFilterStage<CauldronSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Cauldron* Cauldron;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, Cauldron>(f, OnObjectCauldronInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var cauldron = filter.Cauldron;
            var collider = filter.Collider;
            var transform = filter.Transform;

            if (cauldron->TransformingEntity != EntityRef.None || cauldron->Activated) {
                cauldron->EnteredFrames++;
                if (cauldron->EnteredFrames > 30) {
                    if (!cauldron->Activated) {
                        cauldron->Activated = true;
                        collider->Shape.Centroid.Y = cauldron->Hitboxheight;
                        collider->Shape.Box.Extents = new FPVector2(Constants._0_40, cauldron->Hitboxheight);
                        if (f.Unsafe.TryGetPointer(cauldron->TransformingEntity, out MarioPlayer* mario)) {
                            //keep mario loaded
                            mario->SetAsBoss(f, cauldron->TransformingEntity, filter.Entity);
                        } else {
                            //destroy this object
                            HazardSystem.DestroyHazard(f, cauldron->TransformingEntity);
                            cauldron->TransformingEntity = EntityRef.None;
                        }
                    } else if (cauldron->EnteredFrames > 130) {
                        //create boss hazard
                        List<HazardData> hazarddata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas, avaliblebosses = new();
                        for (int i = 0; i < hazarddata.Count; i++) {
                            if (hazarddata[i].Name == "Petey" || hazarddata[i].Name == "Bowser") {
                                //This is A Boss Entity
                                avaliblebosses.Add(hazarddata[i]);
                                continue;
                            }
                        }
                        int pick = f.RNG->Next(0, avaliblebosses.Count);

                        EntityRef newEntity = f.Create(avaliblebosses[pick].hazardAsset);
                        f.Unsafe.GetPointer<PhysicsObject>(newEntity)->Velocity.Y = 8;
                        if (cauldron->TransformingEntity != EntityRef.None) {
                            f.Unsafe.GetPointer<Boss>(newEntity)->MakeBossControllable(f, cauldron->TransformingEntity);
                            f.Unsafe.GetPointer<MarioPlayer>(cauldron->TransformingEntity)->IsBoss = newEntity;
                        }
                        f.Signals.InitializeHazard(newEntity, EntityRef.None, transform->Position, SpawnReason.Normal, pick);
                        f.Events.PlayPuffParticle(transform->Position);
                        cauldron->TransformingEntity = EntityRef.None;
                        cauldron->Activated = false;
                        HazardSystem.DestroyHazard(f, filter.Entity);
                    }
                } else {
                    var otherTransform = f.Unsafe.GetPointer<Transform2D>(cauldron->TransformingEntity);
                    f.Unsafe.GetPointer<PhysicsObject>(cauldron->TransformingEntity)->Velocity = FPVector2.Zero;

                    QuantumUtils.UnwrapWorldLocations(f, transform->Position + FPVector2.Up * FP._0_25, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                    otherTransform->Position += damageDirection * -FP._0_10;
                }
            }
        }

        #region Interactions
        public void OnObjectCauldronInteraction(Frame f, EntityRef otherEntity, EntityRef thisEntity) {
            var cauldron = f.Unsafe.GetPointer<Cauldron>(thisEntity);
            if (cauldron->TransformingEntity != EntityRef.None || (f.Unsafe.TryGetPointer(otherEntity, out Hazard* hazarde) && hazarde->IsHefty) || (hazarde == null && !f.Has<MarioPlayer>(otherEntity)))
                //Cauldron Cannot Accept This Object
                return;
            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var PhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var otherPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_05;

            if (attackedFromAbove && FPMath.Abs(damageDirection.X) < FP._0_50) {
                cauldron->TransformingEntity = otherEntity;
                PhysicsObject->Velocity.Y = 6;
                PhysicsObject->IsTouchingGround = false;
                f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
                collider->Shape.Centroid.Y = 0;
                collider->Shape.Box.Extents = new FPVector2(Constants._0_40, FP._0_01);
                var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
                if (hazard->IsHazard) {
                    hazard->LifeTime = 240;
                }
            }
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Cauldron* cauldron)) {
                return;
            }

            var hazardata = f.FindAsset(f.SimulationConfig.CurrentHazards).HazardGameData.HazardDatas[index];

            //Set The Value To What It Will ALWAYS convert into
            /*if (hazardata.SpecialValues[0].BaseValue == 0)
                cauldron->ConvertInto = hazardata.SpecialValues[0];*/
        }
        #endregion
    }
}
