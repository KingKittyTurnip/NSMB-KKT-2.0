using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct DryBones {

        public readonly void Respawn(Frame f, EntityRef entity) {
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
            var dry = f.Unsafe.GetPointer<DryBones>(entity);

            dry->State = DryState.Idle;
            dry->ReusableTimer = 0;
        }

        public readonly void DryBreak(Frame f, EntityRef dryEntity, bool Groundpound) {
            var dry = f.Unsafe.GetPointer<DryBones>(dryEntity);
            if (dry->State != DryState.Idle) {
                return;
            }

            //YUCK

            var enemy = f.Unsafe.GetPointer<Enemy>(dryEntity);
            var dryTransform = f.Unsafe.GetPointer<Transform2D>(dryEntity);
            var dryCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(dryEntity);
            var dryInteractable = f.Unsafe.GetPointer<Interactable>(dryEntity);
            FPVector2 center = dryTransform->Position + dryCollider->Shape.Centroid;
            FP GenericSpeed = (enemy->FacingRight ? 1 : -1) * dry->Speed * (Groundpound ? 3 : 1);

            // Spawn coin
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            var CoinEntity = gamemode.SpawnLooseCoin(f, center);
            f.Unsafe.GetPointer<PhysicsObject>(CoinEntity)->Velocity.X = -GenericSpeed;

            //Create Head (remember to make this pooled)
            dry->DryHead = f.Create(dry->DryHeadPrototype);
            var headphys = f.Unsafe.GetPointer<PhysicsObject>(dry->DryHead);
            var headTransform = f.Unsafe.GetPointer<Transform2D>(dry->DryHead);
            var head = f.Unsafe.GetPointer<DryHead>(dry->DryHead);
            headphys->Gravity.Y = -21;
            headphys->DisableCollision = false;
            head->BounceTimes = 3;
            head->FacingRight = enemy->FacingRight;
            headTransform->Position = dryTransform->Position + new FPVector2(enemy->FacingRight ? dry->HeadSpawnOffset.X : -dry->HeadSpawnOffset.X, dry->HeadSpawnOffset.Y);

            dryInteractable->ColliderDisabled = true;
            dry->State = DryState.Broken;
            dry->ReusableTimer = 5;
            headphys->Velocity.X = GenericSpeed;

            f.Events.DryBreak(dryEntity);
        }

        public readonly void Kill(Frame f, EntityRef dryEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(dryEntity);
            if (enemy->IsDead) {
                return;
            }
            var dry = f.Unsafe.GetPointer<DryBones>(dryEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(dryEntity);

            var dryTransform = f.Unsafe.GetPointer<Transform2D>(dryEntity);
            var dryCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(dryEntity);
            FPVector2 center = dryTransform->Position + dryCollider->Shape.Centroid;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, dryTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, dryEntity, ourPos.X > theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, dryEntity, false);
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
            f.Events.PlayComboSound(dryEntity, combo);

            enemy->IsDead = true;
            enemy->SetDelayedRespawn();
            f.Unsafe.GetPointer<Interactable>(dryEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(dryEntity);
            f.Events.EnemyKilled(dryEntity, killerEntity, reason, center);
        }
    }
}