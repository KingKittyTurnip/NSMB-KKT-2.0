using Photon.Deterministic;
using UnityEngine.UIElements;

namespace Quantum {
    public unsafe partial struct Tanoomba {

        public void Respawn(Frame f, EntityRef entity) {
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(entity);
            tanoomba->GetupFrames = 0;
            tanoomba->State = TanoombaState.Idling;
            tanoomba->Form = TanoombaFormState.Max;
            tanoomba->Laughing = tanoomba->PlayerPassedBy = false;
        }

        public void HurtTanoomba(Frame f, EntityRef thisEntity, EntityRef killerEntity, bool FromRight) {
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->State == TanoombaState.KnockedBack)
                return;

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            tanoomba->State = TanoombaState.KnockedBack;
            Form = TanoombaFormState.Max;
            physicsObject->Velocity.X = (FromRight ? -1 : 1);
            physicsObject->Velocity.Y = 2;
            physicsObject->IsTouchingGround = false;
            tanoomba->GetupFrames = 35;
            tanoomba->TargetedPlayer = EntityRef.None;
            tanoomba->Laughing = false;
        }

        public void TanoombaStartTransform(Frame f, EntityRef thisEntity, EntityRef TurnedIntoObjectOverlay, bool Floating) {
            var enemy = f.Unsafe.GetPointer<Enemy>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = 0;
            physicsObject->IsFrozen = Floating;

            TransformedObject = TurnedIntoObjectOverlay;
            if (TransformedObject != EntityRef.None) {
                transform->Teleport(f, f.Unsafe.GetPointer<Transform2D>(TransformedObject)->Position);
            }
        }
        public void TanoombaStartTransform(Frame f, EntityRef thisEntity, EntityRef TurnedIntoObjectOverlay, bool Floating, FPVector2 Position) {
            var enemy = f.Unsafe.GetPointer<Enemy>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);

            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = 0;
            physicsObject->IsFrozen = Floating;

            TransformedObject = TurnedIntoObjectOverlay;
            transform->Teleport(f, Position);
        }

        public void TanoombaResetTransform(Frame f, EntityRef thisEntity, bool AttackMode) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = 0;
            physicsObject->IsFrozen = false;

            TransformedObject = EntityRef.None;
            State = AttackMode ? TanoombaState.Attacking : TanoombaState.Idling;
            Form = TanoombaFormState.Max;
        }


        public void Kill(Frame f, EntityRef tanoombaEntity, EntityRef killerEntity, KillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(tanoombaEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(tanoombaEntity);

            var koopaTransform = f.Unsafe.GetPointer<Transform2D>(tanoombaEntity);
            var koopaCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(tanoombaEntity);

            FPVector2 center = koopaTransform->Position + koopaCollider->Shape.Centroid;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, koopaTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, tanoombaEntity, ourPos.X < theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, tanoombaEntity, false);
            }

            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                combo = comboKeeper->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(tanoombaEntity, combo);

            f.Unsafe.GetPointer<Interactable>(tanoombaEntity)->ColliderDisabled = true;
            enemy->IsDead = true;

            physicsObject->IsFrozen = false;

            f.Events.EnemyKilled(tanoombaEntity, killerEntity, reason, center);
        }
    }
}