using Photon.Deterministic;
using System.Diagnostics;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {
    
    public unsafe class HeavyStoneSystem : SystemMainThreadFilterStage<HeavyStoneSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEntityBumped, ISignalOnBeforeInteraction,
        ISignalOnTryLiquidSplash, ISignalInitializeHazard {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public HeavyStone* heavystone;
            public Holdable* holdable;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* PhysicsCollider;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<HeavyStone, Coin>(f, OnHeavyStoneCoinInteraction);
            f.Context.Interactions.Register<MarioPlayer, HeavyStone>(f, OnHeavyStoneMarioInteraction);
            f.Context.Interactions.Register<HeavyStone, Goomba>(f, OnHeavyStoneGoombaInteraction);
            f.Context.Interactions.Register<HeavyStone, Koopa>(f, OnHeavyStoneKoopaInteraction);
            f.Context.Interactions.Register<HeavyStone, Bobomb>(f, OnHeavyStoneBobombInteraction);
            //f.Context.Interactions.Register<HeavyStone, BulletBill>(f, OnHeavyStoneBulletBillInteraction);
            //f.Context.Interactions.Register<HeavyStone, PiranhaPlant>(f, OnHeavyStonePiranhaPlantInteraction);
            //f.Context.Interactions.Register<HeavyStone, Boo>(f, OnHeavyStoneBooInteraction);
            //f.Context.Interactions.Register<HeavyStone, IceBlock>(f, OnHeavyStoneIceBlockInteraction);
            //f.Context.Interactions.Register<HeavyStone, IceBlock>(f, OnHeavyStoneIceBlockInteractionStationary);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var stone = filter.heavystone;
            var physicsObject = filter.PhysicsObject;

            if (stone->Thrown && physicsObject->IsTouchingGround) {
                stone->Thrown = false;
                physicsObject->Velocity.X = 0;
            }
            if (physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) {
                var entity = filter.Entity;
                f.Events.HeavyStoneLand(entity, f.Unsafe.GetPointer<Transform2D>(entity)->Position);
            }
        }

        public static void Destroy(Frame f, EntityRef thisEntity) {
            f.Destroy(thisEntity);
        }

        #region Interactions
        public static void OnHeavyStoneMarioInteraction(Frame f, EntityRef marioEntity, EntityRef thisEntity, PhysicsContact contact) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var stone = f.Unsafe.GetPointer<HeavyStone>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            //Bug, Sometimes When Struck From bellow Doesn't Work
            if (stone->Thrown || !physicsObject->IsTouchingGround) {
                // It's A Stone Luigi
                mario->DoKnockback(f, marioEntity, contact.Normal.X > 0, 3, false, thisEntity);
                return;
            } else if (upDot >= PhysicsObjectSystem.GroundMaxAngle || upDot <= -PhysicsObjectSystem.GroundMaxAngle) {
                // Top/Bottom, Do Nothing
                return;
            } else {
                bool rightContact = contact.Normal.X > 0;
                if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    // HOMERUN
                    f.Events.PlayComboSound(thisEntity, 0);
                    stone->Thrown = true;
                    physicsObject->IsTouchingGround = false;
                    physicsObject->Velocity = new FPVector2(contact.Normal.X > 0 ? -8 : 8, 5);
                    return;
                }
            }

            if (!stone->Thrown) {
                //Only Allow Carry If No Team Or Same Team --- TOTEST
                /*var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
                var hazard = f.Unsafe.GetPointer<Hazard>(thisEntity);
                if (hazard->Team != 64 && mario->GetTeam(f) != hazard->Team) {
                    return;
                } */

                // Attempt pickup (assuming it isn't already picked up)

                if (!f.Exists(holdable->Holder) && mario->CanPickupItem(f, marioEntity, thisEntity)) {
                    // Pickup successful
                    holdable->Pickup(f, thisEntity, marioEntity);
                }
            }
        }
        // what? Why Don't You Work!
        public static void OnHeavyStoneCoinInteraction(Frame f, EntityRef thisEntity, EntityRef coinEntity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(thisEntity);
            var stone = f.Unsafe.GetPointer<HeavyStone>(thisEntity);

            if (!f.Exists(holdable->PreviousHolder) || !stone->Thrown) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public static void OnHeavyStoneGoombaInteraction(Frame f, EntityRef thisEntity, EntityRef goombaEntity) {
            var stone = f.Unsafe.GetPointer<HeavyStone>(thisEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (stone->Thrown || beingHeld) {
                // Destroy them
                goomba->Kill(f, goombaEntity, thisEntity, KillReason.Special);
            } else {
                //EnemySystem.EnemyBumpTurnaround(f, koopaEntity, goombaEntity);
            }
        }

        public static void OnHeavyStoneKoopaInteraction(Frame f, EntityRef thisEntity, EntityRef koopaEntity) {
            var stone = f.Unsafe.GetPointer<HeavyStone>(thisEntity);
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);

            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (stone->Thrown || beingHeld) {
                // Destroy them
                koopa->Kill(f, koopaEntity, thisEntity, KillReason.Special);
            } else {
                //EnemySystem.EnemyBumpTurnaround(f, koopaEntity, goombaEntity);
            }
        }

        public static void OnHeavyStoneBobombInteraction(Frame f, EntityRef thisEntity, EntityRef bobombEntity) {
            var stone = f.Unsafe.GetPointer<HeavyStone>(thisEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(thisEntity)->Holder);

            if (stone->Thrown || beingHeld) {
                // Destroy them
                bobomb->Kill(f, bobombEntity, thisEntity, KillReason.Special);
            } else {
                //EnemySystem.EnemyBumpTurnaround(f, koopaEntity, goombaEntity);
            }
        }

        #endregion

        #region Signals
        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out HeavyStone* stone)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysicsObject)) {
                return;
            }

            //TODO: Up key
            stone->Thrown = true;
            FP bonusSpeed = FPMath.Abs(marioPhysicsObject->Velocity.X / 3);
            if (FPMath.Sign(marioPhysicsObject->Velocity.X) != (mario->FacingRight ? 1 : -1)) {
                bonusSpeed *= -1;
            }
            physicsObject->Velocity.X = (Constants._3_50 + bonusSpeed) * (mario->FacingRight ? 1 : -1);
            physicsObject->Velocity.Y = 1;
            holdable->IgnoreOwnerFrames = 15;

            if (!dropped) {
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 tileWorldPosition, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out HeavyStone* stone)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || f.Exists(holdable->Holder)
                || holdable->IgnoreOwnerFrames > 0) {

                return;
            }

            f.Events.PlayComboSound(entity, 0);
            physicsObject->IsTouchingGround = false;
            physicsObject->Velocity.Y = 5;
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            *allowInteraction &= !f.Unsafe.TryGetPointer(entity, out Freezable* freezable) || !freezable->IsFrozen(f);
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquidEntity, QBoolean exit, bool* doSplash) {
            *doSplash = true;
        }

        public void InitializeHazard(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            if (!f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                return;
            }

            //uhh i would put specific hazard spawn data here
            //Like The Activate heavystone stuff so it hurts on spawn
        }
        #endregion
    }
}
