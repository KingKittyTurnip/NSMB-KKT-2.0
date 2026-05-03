using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct CoinItem {

        public void Initialize(Frame f, EntityRef thisEntity, byte spawnAnimationLength, PowerupSpawnReason spawnReason) {
            SpawnReason = spawnReason;
            SpawnAnimationFrames = spawnAnimationLength;
            f.Unsafe.GetPointer<Hazard>(thisEntity)->LifeTime += spawnAnimationLength;

            if (f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                physicsObject->DisableCollision = true;
            }
            if (SpawnReason != PowerupSpawnReason.PowerupBlock && f.Unsafe.TryGetPointer(thisEntity, out Interactable* interactable)) {
                interactable->ColliderDisabled = true;
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                hazard->IsCoinItem = true;
                hazard->LifeTime = 600;
            }
        }

        public void InitializeBlockSpawn(Frame f, EntityRef thisEntity, byte spawnAnimationLength, FPVector2 spawnOrigin, FPVector2 spawnDestination) {
            Initialize(f, thisEntity, spawnAnimationLength, PowerupSpawnReason.PowerupBlock);

            BlockSpawn = true;
            BlockSpawnOrigin = spawnOrigin;
            BlockSpawnDestination = spawnDestination;
            BlockSpawnAnimationLength = spawnAnimationLength;
            f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position = spawnOrigin;

            if (f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                physicsObject->IsFrozen = true;
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)) {
                enemy->IsActive = true;
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                hazard->IsCoinItem = true;
            }
        }

        public void InitializeLaunchSpawn(Frame f, EntityRef thisEntity, bool facingRight, FPVector2 spawnOrigin) {
            Initialize(f, thisEntity, 20, PowerupSpawnReason.PowerupBlock);

            LaunchSpawn = true;
            f.Unsafe.GetPointer<Transform2D>(thisEntity)->Position = spawnOrigin;

            if (f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                // TODO: magic number
                physicsObject->Velocity = new FPVector2(facingRight ? 2 : -2, 9);
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Powerup* powerup)) {
                powerup->FacingRight = facingRight;
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Enemy* enemy)) {
                enemy->FacingRight = facingRight;
                enemy->IsActive = true;
            }
            if (f.Unsafe.TryGetPointer(thisEntity, out Hazard* hazard)) {
                hazard->IsCoinItem = true;
            }
        }

        public void InitializePlayerSpawn(Frame f, EntityRef thisEntity, EntityRef playerToFollow) {
            Initialize(f, thisEntity, 60, PowerupSpawnReason.Coins);
            ParentMarioPlayer = playerToFollow;
            IgnorePlayerFrames = 60;

            var marioTransform = f.Unsafe.GetPointer<Transform2D>(playerToFollow);
            var marioCamera = f.Unsafe.GetPointer<CameraController>(playerToFollow);

            var asset = f.FindAsset(Scriptable);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            transform->Position = 
                new FPVector2(marioTransform->Position.X, marioCamera->CurrentPosition.Y)
                    + asset.CameraSpawnOffset;
            
            if (f.Unsafe.TryGetPointer(thisEntity, out PhysicsObject* physicsObject)) {
                physicsObject->IsFrozen = true;
            }
        }
    }
}