using Photon.Deterministic;
using Quantum.Collections;
using System;
using UnityEngine.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Quantum {
    public unsafe class TornadoSystem : SystemMainThreadEntityFilter<Tornado, TornadoSystem.Filter>, ISignalInitializeHazard {

        FP tornadoUpliftSpeed = FP._0_05; //1-2
        FP tornadoLaunchSpeed = 10;
        FP tornadoObjectAcceleration = 1;// FP._0_20;
        FP tornadoCone = Constants._0_66;
        FP tornadoTop = Constants._2_50;

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Tornado* Tornado;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, Tornado>(f, OnSpinnerMarioPlayerInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var tornado = filter.Tornado;
            var disTransform = filter.Transform;
            var disCollider = filter.Collider;

            QHashSet<EntityRef> stuffInside = f.ResolveHashSet(tornado->EntitiesInside);
            foreach (var insideEntity in stuffInside) {
                if (f.Exists(insideEntity)) {
                    QuantumUtils.UnwrapWorldLocations(f, disTransform->Position, f.Unsafe.GetPointer<Transform2D>(insideEntity)->Position, out FPVector2 tornadoPos, out FPVector2 theirPos);
                    var theirPhysics = f.Unsafe.GetPointer<PhysicsObject>(insideEntity);
                    var theirCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(insideEntity);
                    var theirTransform = f.Unsafe.GetPointer<Transform2D>(insideEntity);

                    /*FP YMultiplier = (theirPos.Y - tornadoPos.Y + 1)/tornadoCone;
                    FP XDif = FPMath.Abs(theirPos.X - tornadoPos.X);
                    FP cap = 2;
                    FP velCap = 5;// (FPMath.Abs(theirPos.X - tornadoPos.X) + 1);//YMultiplier;
                    FPVector2 damageDirection = (theirPos - tornadoPos).Normalized;
                    bool ExitingNextFrame = theirPos.Y - theirCollider->Shape.Box.Extents.Y + theirCollider->Shape.Centroid.Y + (tornadoUpliftSpeed * f.DeltaTime) > tornadoPos.Y + tornadoTop;

                    theirPhysics->Velocity.Y = ExitingNextFrame ? tornadoLaunchSpeed : tornadoUpliftSpeed;

                    FP velocityBonus =  ((theirPos.X - tornadoPos.X > cap && theirPhysics->Velocity.X > 0) ? tornadoObjectAcceleration : (theirPos.X - tornadoPos.X < -cap && theirPhysics->Velocity.X < 0) ? tornadoObjectAcceleration : (theirPhysics->Velocity.X > 0 ? tornadoObjectAcceleration : -tornadoObjectAcceleration));
                    theirPhysics->Velocity.X = FPMath.Clamp(theirPhysics->Velocity.X + velocityBonus, -velCap, velCap);

                    Debug.Log("manipulated object from list with the velocity: " + YMultiplier + " " + cap);*/

                    //theirTransform->Position = new Vector2((disTransform->Position.X + (1 + (tornadoTimer / 2.3f)) * Mathf.Sin(tornadoTimer / 0.5f)), ((tornado.transform.position.y - 2.5f) + tornadoTimer));
                    theirPhysics->Velocity.Y = tornadoLaunchSpeed;

                    //default velocity = 12f
                    //flying = true;
                    //onGround = false;
                    //body.position += Vector2.up * 0.075f;
                    //doGroundSnap = false;
                    //previousOnGround = false;
                    //crouching = false;
                    //inShell = false;
                    //drill = false;
                    //groundpound = false;
                    //DoorPound = false;
                    //bounce = false;
                    // knockback = false;
                    //singlejump = false;
                    //doublejump = true;
                    //triplejump = false;
                    //transform.position = body.position = new Vector2((tornado.transform.position.x + (1 + (tornadoTimer / 2.3f)) * Mathf.Sin(tornadoTimer / 0.5f)), ((tornado.transform.position.y - 2.5f) + tornadoTimer));

                    //have the animator record the last amount and then play a sound whenever it gets lower?
                }
            }

            stuffInside.Clear();
        }

        public static void OnSpinnerMarioPlayerInteraction(Frame f, EntityRef otherEntity, EntityRef tornadoEntity) {
            if (!f.Unsafe.TryGetPointer<PhysicsObject>(otherEntity, out var phys) || phys->WindImmune || phys->IsFrozen) {
                return;
            }

            var tornado = f.Unsafe.GetPointer<Tornado>(tornadoEntity);
            QHashSet<EntityRef> mariosSet = f.ResolveHashSet(tornado->EntitiesInside);

            mariosSet.Add(otherEntity);
            return;
        }

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, int index) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Tornado* tornado)) {
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