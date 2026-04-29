using Photon.Deterministic;
using Quantum;

public unsafe class DamagePowerupAsset : PowerupAsset {

    public override int CountPlayersWithState(Frame f) {
        return 0;
    }

    public override unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        if (!mario->Powerdown(f, marioEntity, false, EntityRef.None)) {
            f.Events.EnemyKicked(marioEntity, false);
        }

        return PowerupReserveResult.CollectNewIgnoreOld;
    }

    protected override unsafe void OnCollected(Frame f, EntityRef entity) {
        if (f.Unsafe.TryGetPointer<Enemy>(entity, out var enemy)) {
            enemy->IsActive = false;
            enemy->IsDead = true;
            if (!enemy->DisableRespawning) {
                enemy->SetDelayedRespawn();
            }
            if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                physicsObject->IsFrozen = true;
            }

            f.Signals.OnEnemyDespawned(entity);
        }
    }
}