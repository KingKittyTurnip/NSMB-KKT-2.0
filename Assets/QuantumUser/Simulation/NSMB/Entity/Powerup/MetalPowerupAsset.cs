using Photon.Deterministic;
using Quantum;

public unsafe class MetalPowerupAsset : PowerupAsset {

    public FP MetalDuration = 15;

    public override int CountPlayersWithState(Frame f) {
        int count = 0;
        foreach ((_, var marioPlayer) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            if (marioPlayer->IsMetal) {
                count++;
            }
        }
        return count;
    }

    public override unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        var marphys = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

        mario->MetalMushroomFrames = MetalDuration;
        //mario->DoKnockback(f, marioEntity, mario->FacingRight, 0, KnockbackStrength.CollisionBump, marioEntity, true);
        //marphys->Velocity = new FPVector2(0, 6);

        f.Signals.OnMarioPlayerBecameInvincible(marioEntity);
        return PowerupReserveResult.CollectNewIgnoreOld;
    }
}