using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Tanoomba {

        public void Respawn(Frame f, EntityRef entity) {
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(entity);
            tanoomba->GetupFrames = 0;
            tanoomba->DamageInvincibilityFrames = 0;
            tanoomba->State = TanoombaState.Idling;
            tanoomba->LastKnockback = 255;
        }

        public void HurtTanoomba(Frame f, EntityRef thisEntity, EntityRef killerEntity, bool FromRight, byte ThisKnockback) {
            var tanoomba = f.Unsafe.GetPointer<Tanoomba>(thisEntity);
            if (tanoomba->LastKnockback == ThisKnockback || (ThisKnockback == (byte) KnockbackStrength.FireballBump && (tanoomba->LastKnockback == (byte) KnockbackStrength.Normal || tanoomba->LastKnockback == (byte) KnockbackStrength.Groundpound)))
                return;
            tanoomba->LastKnockback = ThisKnockback;

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            if (tanoomba->State == TanoombaState.KnockedBack) {
                //Properly Implement Tanoomba Death
                tanoomba->Kill(f, thisEntity, thisEntity, KillReason.Normal);
                physicsObject->DisableCollision = true;
            } else {
                tanoomba->State = TanoombaState.KnockedBack;
                physicsObject->Velocity.X = (tanoomba->LastKnockback == (byte) KnockbackStrength.Groundpound ? 4 : 1) * (FromRight ? -1 : 1);
                physicsObject->Velocity.Y = 2;
                physicsObject->IsTouchingGround = false;
                tanoomba->HitFrame = f.Number + 12;
                tanoomba->GetupFrames = 35;
            }
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

            f.Events.EnemyKilled(tanoombaEntity, killerEntity, reason, center);
        }
    }
}