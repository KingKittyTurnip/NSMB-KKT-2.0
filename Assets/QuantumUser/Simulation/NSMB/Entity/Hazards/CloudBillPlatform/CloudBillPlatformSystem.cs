using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine.UIElements;

namespace Quantum {
    
    public unsafe class CloudBillPlatformSystem : SystemMainThreadEntityFilter<CloudBillPlatform, CloudBillPlatformSystem.Filter>, ISignalInitializeHazard {
        public struct Filter {
            public EntityRef Entity;
            public CloudBillPlatform* cloudplatform;
            public PhysicsCollider2D* collider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, CloudBillPlatform>(f, OnCloudBillPlatformMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var cloudplatform = filter.cloudplatform;
            var entity = filter.Entity;

            if (f.Exists(cloudplatform->CloudBill)) {
                var collider = filter.collider;
                var thisTransform = f.Unsafe.GetPointer<Transform2D>(entity);
                var cloudbillTransform = f.Unsafe.GetPointer<Transform2D>(cloudplatform->CloudBill);
                QuantumUtils.UnwrapWorldLocations(stage, thisTransform->Position, cloudbillTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);

                if (FPMath.Abs(theirPos.X - ourPos.X) > FP._0_75) {
                    //Cloudbill has moved 1.5 tiles, shift everything over & create a new cloudtile
                    FP Offset = cloudplatform->FacingRight ? -FP._0_50 : FP._0_50;
                    var list = f.ResolveList(cloudplatform->ActiveClouds);
                    int listCount = list.Count;

                    //Scoot Us Over
                    thisTransform->Teleport(f, new FPVector2(thisTransform->Position.X - Offset, thisTransform->Position.Y));

                    //Scoot Bitset Over
                    for (int i = listCount-1; i > 0; i--) {
                        list[i] = list[i-1];
                    }
                    list[0] = true;

                    //Apply to hitboxes
                    collider->Shape.Compound.GetShapes(f, out var shape, out int count);
                    if (count < (listCount*2)-1) {
                        //We don't have every platform, add another!
                        Shape2D newShape = Shape2D.CreateEdge(FP._0_25, new FPVector2(count * FP._0_50, Constants._0_365));
                        collider->Shape.Compound.AddShape(f, ref newShape);
                        collider->Shape.Compound.AddShape(f, ref newShape);//we add an extra platform to fix looping issues
                        //Grab again
                        collider->Shape.Compound.GetShapes(f, out shape, out count);
                        f.Events.CloudBillCloudAnimation(f, entity, true);
                    } else {
                        f.Events.CloudBillCloudAnimation(f,entity, false);
                    }
                    cloudplatform->UpdateCollision(stage, thisTransform->Position, Offset, shape, count, list);
                }
            } else {
                if (true) {
                    //removed all clouds before destroying
                    f.Destroy(entity);
                }
            }
        }

        #region Interactions
        public static bool OnCloudBillPlatformMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

            if (mario->IsGroundpoundActive) {
                //Break Clouds
                var cloudplatform = f.Unsafe.GetPointer<CloudBillPlatform>(thisEntity);
                var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thisEntity);
                var marioCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(marioEntity);
                var thisTransform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
                var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
                QuantumUtils.UnwrapWorldLocations(f, thisTransform->Position, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                var XDif = FPMath.Abs(theirPos.X - ourPos.X)*2;
                var marioSize = marioCollider->Shape.Box.Extents.X*2;

                var list = f.ResolveList(cloudplatform->ActiveClouds);

                int Rightward = FPMath.RoundToInt(XDif + marioSize);
                int Leftward = FPMath.RoundToInt(XDif - marioSize);

                int clampRight = FPMath.Clamp(Rightward, 0, list.Count-1);
                int clampLeft = FPMath.Clamp(Leftward, 0, list.Count-1);


                if (Rightward == clampRight) {
                    list[clampRight] = false;
                    f.Events.CloudBillCloudBreak(thisEntity, (byte) clampRight);
                }
                if (Leftward == clampLeft) {
                    list[clampLeft] = false;
                    f.Events.CloudBillCloudBreak(thisEntity, (byte) clampLeft);
                }

                collider->Shape.Compound.GetShapes(f, out var shape, out int count);
                FP Offset = cloudplatform->FacingRight ? -FP._0_50 : FP._0_50;
                VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                cloudplatform->UpdateCollision(stage, thisTransform->Position, Offset, shape, count, list);
            }
            return false;
        }
        #endregion

        #region Signals
        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason, QListPtr<byte> spawnData) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out CloudBillPlatform* cloudplatform)
                || !f.Unsafe.TryGetPointer(thisEntity, out Transform2D* transform)) {
                return;
            }
            var specialValues = f.ResolveList(spawnData);

            //Get Direction
            cloudplatform->FacingRight = specialValues[1] == 0 ? (f.RNG->Next() < FP._0_50) : specialValues[1] == 2;
            transform->Position = spawnpoint;

            //create cloudbill
            cloudplatform->CloudBill = f.Create(cloudplatform->CloudBillPrototype);
            f.Signals.InitializeHazard(cloudplatform->CloudBill, EntityRef.None, transform->Position, SpawnReason.Normal, new QListPtr<byte>());
            f.Unsafe.GetPointer<Enemy>(cloudplatform->CloudBill)->FacingRight = cloudplatform->FacingRight;

            HazardSystem.ChangeHazardIcon(f, thisEntity, false);

            //Set Length
            int Length = specialValues[0] switch {
                0 => 3,
                1 => 7,
                2 => 12,
                3 => 18,
                4 => 30,
                5 => 64, //Secret Mode
                _ => 12,
            };
            var list = f.ResolveList(cloudplatform->ActiveClouds);
            for (int i = 0; i < Length; i++) {
                list.Add(false);
            }
        }
        #endregion
    }
}
