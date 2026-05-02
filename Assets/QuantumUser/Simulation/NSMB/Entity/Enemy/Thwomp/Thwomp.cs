using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Thwomp {

        public void Respawn(Frame f, EntityRef entity) {
            var thwomp = f.Unsafe.GetPointer<Thwomp>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

            thwomp->State = ThwompState.Idle;
            physicsObject->Velocity.X = 0;
            physicsObject->Velocity.Y = 0;
            physicsObject->Gravity.Y = 0;
        }

        public void Kill(Frame f, EntityRef thwompEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(thwompEntity);
            var thwomp = f.Unsafe.GetPointer<Thwomp>(thwompEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thwompEntity);

            var thwompTransform = f.Unsafe.GetPointer<Transform2D>(thwompEntity);
            var thwompCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(thwompEntity);
            FPVector2 center = thwompTransform->Position + thwompCollider->Shape.Centroid;

            physicsObject->DisableCollision = true;
            physicsObject->Gravity.Y = -18;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            //physicsObject->Gravity = new FPVector2(0, -Constants._14_75);
            thwomp->State = ThwompState.Idle;
            thwomp->Timer = 0;

            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                combo = comboKeeper->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(thwompEntity, combo);

            enemy->IsDead = true;
            f.Unsafe.GetPointer<Interactable>(thwompEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(thwompEntity);
            f.Events.EnemyKilled(thwompEntity, killerEntity, reason, center);
        }
    }
}