using Photon.Deterministic;
using Quantum;

public unsafe class TornadoPowerupAsset : PowerupAsset {
    public override int CountPlayersWithReserve(Frame f) {
        return 0;
    }

    public override int CountPlayersWithState(Frame f) {
        return 0;
    }

    public override void InitializeFromBlockBump(Frame f, EntityRef entity, ref BlockBumpSystem.Filter blockBumpFilter) {
        var blockBump = blockBumpFilter.BlockBump;
        var coinItem = f.Unsafe.GetPointer<CoinItem>(entity);
        BreakableBrickTile tile = (BreakableBrickTile) f.FindAsset(blockBump->StartTile);

        FPVector2 origin = blockBumpFilter.Transform->Position;
        origin.Y += (tile.BumpSize.Y / 2) - FP._0_50;

        coinItem->InitializeBlockSpawn(f, entity, 60,
            origin,
            origin + new FPVector2(0, FP._0_50));
        coinItem->IgnorePlayerFrames = 5;
    }
}