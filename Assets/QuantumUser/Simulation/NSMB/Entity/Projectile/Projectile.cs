using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Projectile {

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool FromPlayer) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) { 

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X / 3);
            }

            if (asset.LockTo45Degrees) {
                physicsObject->TerminalVelocity = -Speed;
            }

            Lifetime = asset.LifeTime;
            SpawnedFromPlayer = FromPlayer;

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
        }

        public void InitializeHammer(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool FromPlayer, bool playerHoldingUp) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Initial Velocity
            FPVector2 velocity = playerHoldingUp ? new FPVector2(FP.FromString("3.8822"), FP.FromString("14.4888")) : new FPVector2(FP.FromString("6.25"), FP.FromString("7.5"));
            Speed = velocity.X;

            Lifetime = asset.LifeTime;
            SpawnedFromPlayer = FromPlayer;

            // Apply
            transform->Position = spawnpoint;
            physicsObject->Velocity = velocity;
            physicsObject->Gravity = FPVector2.Up * (playerHoldingUp ? FP.FromString("-37.512") : FP.FromString("-28.125"));
        }

        //bad practice but whatever
        public void InitializeBubble(Frame f, VersusStageData stage, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, bool right, bool FromPlayer) {
            var asset = f.FindAsset(Asset);
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // Vars
            Owner = owner;
            FacingRight = right;

            // Speed
            Speed = asset.Speed;
            physicsObject->Gravity = asset.Gravity;
            if (asset.InheritShooterVelocity
                && f.Unsafe.TryGetPointer(owner, out PhysicsObject* ownerPhysicsObject)
                &&  f.Unsafe.TryGetPointer(owner, out MarioPlayer* mario)
                // Moving in same direction
                && FPMath.Sign(ownerPhysicsObject->Velocity.X) == 1 == FacingRight) {

                Speed += FPMath.Abs(ownerPhysicsObject->Velocity.X * FP._0_75);
                if (mario->WalljumpFrames > 0)
                    physicsObject->TerminalVelocity -= FP._0_75;
            }

            FP GetMaxCam = stage.CameraMinPosition.Y + FPMath.Max(stage.CameraMaxPosition.Y - stage.CameraMinPosition.Y, FP._7) - 1;
            if (spawnpoint.Y > GetMaxCam) {
                //No bubbles allowed up here
                physicsObject->TerminalVelocity -= (spawnpoint.Y - GetMaxCam);
            }

            Lifetime = asset.LifeTime;
            SpawnedFromPlayer = FromPlayer;

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(Speed * (FacingRight ? 1 : -1), -Speed);
            Speed = FP._1_50;
        }
    }
}