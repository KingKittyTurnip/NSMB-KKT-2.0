using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Podobo {
        public readonly void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            physicsObject->DisableCollision = true;
            physicsObject->Gravity = FPVector2.Down * 21;

            var podobo = f.Unsafe.GetPointer<Podobo>(entity);
            podobo->WaitTime = 2;
            podobo->IsHopB = true;
            if (podobo->HopBLocation == 999) {
                podobo->HopBLocation = f.Unsafe.GetPointer<Enemy>(entity)->Spawnpoint.X;
            }
        }

        public readonly void Kill(Frame f, EntityRef podoboEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(podoboEntity);
            if (enemy->IsDead) {
                return;
            }
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(podoboEntity);

            // Fall off screen
            var booTransform = f.Unsafe.GetPointer<Transform2D>(podoboEntity);
            var killerTransform = f.Unsafe.GetPointer<Transform2D>(killerEntity);
            if (reason != EnemyKillReason.Special) {
                QuantumUtils.UnwrapWorldLocations(f, booTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, podoboEntity, ourPos.X > theirPos.X);
                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (enemy->FacingRight ? 1 : -1),
                    Constants._2_50
                );
                physicsObject->Gravity = new FPVector2(0, -Constants._14_75);
            } else {
                //into the abyss we go
                f.Unsafe.GetPointer<Transform2D>(podoboEntity)->Position.Y = -999;
            }

            // Play combo sound
            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                combo = comboKeeper->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(podoboEntity, combo);

            enemy->IsDead = true;
            enemy->SetDelayedRespawn(sparklesTime: 120);

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(podoboEntity);
            FPVector2 center = booTransform->Position + collider->Shape.Centroid;
            f.Events.EnemyKilled(podoboEntity, killerEntity, reason, center);
        }
    }
}