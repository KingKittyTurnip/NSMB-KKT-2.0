using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Cataquack {

        public readonly void Respawn(Frame f, EntityRef entity) {
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
        }

        public readonly void Kill(Frame f, EntityRef duckEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(duckEntity);
            if (enemy->IsDead) {
                return;
            }
            if (Varient == CataquackVarient.SturdyGreen) {
                f.Events.IsNowResistantHit(f.Number, duckEntity);
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(duckEntity);

            var duckTransform = f.Unsafe.GetPointer<Transform2D>(duckEntity);
            var duckCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(duckEntity);
            FPVector2 center = duckTransform->Position + duckCollider->Shape.Centroid;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, duckTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, duckEntity, ourPos.X > theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, duckEntity, false);
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
            f.Events.PlayComboSound(duckEntity, combo);

            enemy->IsDead = true;
            enemy->SetDelayedRespawn();
            f.Unsafe.GetPointer<Interactable>(duckEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(duckEntity);
            f.Events.EnemyKilled(duckEntity, killerEntity, reason, center);
        }
    }
}