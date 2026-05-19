using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Prototypes;
using System.Collections.Generic;
using UnityEngine;

namespace Quantum {
    
    public unsafe class CauldronSystem : SystemMainThreadFilterStage<CauldronSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public Cauldron* Cauldron;
            public Hazard* hazard;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public CoinItem* CoinItem;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<PhysicsObject, Cauldron>(f, OnObjectCauldronInteraction);
            f.Context.Interactions.Register<PhysicsObject, Cauldron>(f, OnObjectCauldronSolidInteraction);
            f.Context.RegisterPreContactCallback(f, OnCauldronObjectSolidPreContact);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var cauldron = filter.Cauldron;
            var collider = filter.Collider;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var coinitem = filter.CoinItem;
            var hazard = filter.hazard;

            //Hacky Fix...
            if (coinitem->SpawnAnimationFrames == 1) {
                physicsObject->DisableCollision = false;
            }
            collider->Shape.Centroid.Y = physicsObject->DisableCollision ? 1160 : cauldron->Hitboxheight;

            if (cauldron->TransformingEntity != EntityRef.None || cauldron->Activated) {
                cauldron->EnteredFrames++;
                if (cauldron->EnteredFrames > 15) {
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
                        f.Events.CauldronHop(filter.Entity);
                    } else if (cauldron->EnteredFrames > 130) {
                        //create boss hazard
                        ConvertToBoss(f, filter.Entity, false);

                    } else if (cauldron->EnteredFrames == 100) {
                        f.Events.CauldronExpand(filter.Entity);
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

        public void ConvertToBoss(Frame f, EntityRef thisEntity, bool IsInstantVarient) {
            var cauldron = f.Unsafe.GetPointer<Cauldron>(thisEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);

            var bossesAsset = f.FindAsset(cauldron->BossData);
            EntityRef newEntity = f.Create(bossesAsset.ListOfOptions[cauldron->ConvertIntoBossId].EntityPrototype);

            f.Unsafe.GetPointer<PhysicsObject>(newEntity)->Velocity.Y = 8;

            if (cauldron->TransformingEntity != EntityRef.None) {
                f.Unsafe.GetPointer<Boss>(newEntity)->MakeBossControllable(f, cauldron->TransformingEntity);
                f.Unsafe.GetPointer<MarioPlayer>(cauldron->TransformingEntity)->IsBoss = newEntity;
            }
            //Setup Extradata
            ExtrasList j = new ExtrasList();
            bossesAsset.ListOfOptions[cauldron->ConvertIntoBossId].Extra.Materialize(f, ref j);

            f.Signals.InitializeHazard(newEntity, EntityRef.None, transform->Position, SpawnReason.Normal, j.Extra);
            f.Events.PlayPuffParticle(transform->Position);
            cauldron->TransformingEntity = EntityRef.None;
            cauldron->Activated = true;
            if (IsInstantVarient) {
                hazard->LifeTime = 3;
                transform->Position.Y = 1346;//idk
                physicsObject->DisableCollision = true;
            } else {
                if (hazard->IsHazard || hazard->IsCoinItem) {
                    HazardSystem.DestroyHazard(f, thisEntity);
                } else {
                    f.Destroy(thisEntity);
                }
            }
        }

        #region Interactions
        public static void OnObjectCauldronInteraction(Frame f, EntityRef otherEntity, EntityRef thisEntity) {
            TryEnterCauldron(f, otherEntity, thisEntity);
        }
        public static bool OnObjectCauldronSolidInteraction(Frame f, EntityRef anyEntity, EntityRef thisEntity, PhysicsContact contact) {
            return TryEnterCauldron(f, anyEntity, thisEntity);
        }
        private void OnCauldronObjectSolidPreContact(Frame f, VersusStageData stage, EntityRef entity, PhysicsContact contact, ref bool keepContacts) {
            if (f.Has<Cauldron>(entity) && f.Has<PhysicsObject>(contact.Entity)) {
                keepContacts = TryEnterCauldron(f, contact.Entity, entity);
            }
        }
        public static bool TryEnterCauldron(Frame f, EntityRef otherEntity, EntityRef thisEntity) {
            var cauldron = f.Unsafe.GetPointer<Cauldron>(thisEntity);
            if (cauldron->TransformingEntity != EntityRef.None || (f.Unsafe.TryGetPointer(otherEntity, out Hazard* hazarde) && hazarde->IsHefty) || (hazarde == null && !f.Has<MarioPlayer>(otherEntity)))
                //Cauldron Cannot Accept This Object
                return false;

            if (f.Has<BigStar>(otherEntity) || f.Has<ChainChomp>(otherEntity) || (f.Unsafe.TryGetPointer<ThrowingObject>(otherEntity, out ThrowingObject* throwable) && throwable->Type == ThrowingObjectType.KingBooStone)) {
                //Cauldron ALSO Cannot Accept These more specific edge cases
                return false;
            }

            var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var PhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var otherTransform = f.Unsafe.GetPointer<Transform2D>(otherEntity);
            var otherPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(otherEntity);

            QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position + FPVector2.Up * FP._0_10, otherTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_05;

            if (attackedFromAbove && FPMath.Abs(damageDirection.X) < FP._0_50) {
                cauldron->TransformingEntity = otherEntity;
                PhysicsObject->IsFrozen = PhysicsObject->DisableCollision = true;
                PhysicsObject->Velocity = FPVector2.Zero;
                f.Unsafe.GetPointer<Interactable>(thisEntity)->ColliderDisabled = true;
                var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
                collider->Shape.Centroid.Y = 0;
                collider->Shape.Box.Extents = new FPVector2(Constants._0_40, FP._0_01);

                var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
                if (hazard->IsHazard) {
                    hazard->LifeTime = 240;
                }
                f.Events.CauldronSplash(thisEntity);
                return true;
            }
            return false;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)
                || !f.Unsafe.TryGetPointer(thisEntity, out Cauldron* cauldron)) {
                return;
            }
            var specialValues = f.ResolveList(spawnData);

            //Set The Value To What It Will ALWAYS convert into
            if (specialValues[0] == 0) {
                //pick random
                cauldron->ConvertIntoBossId = (byte)f.RNG->Next(0, f.FindAsset(cauldron->BossData).ListOfOptions.Length);
            } else {
                //pick specific
                cauldron->ConvertIntoBossId = specialValues[0]--;
            }

            if (specialValues[1] == 1) {
                ConvertToBoss(f, thisEntity, true);
            }
        }
        #endregion
    }
}