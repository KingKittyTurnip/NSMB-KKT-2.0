using Quantum;

public unsafe class PowerupTile : PowerupTileBase {

    public AssetRef<PowerupAsset> smallPowerup, largePowerup;

    public override CoinItemAsset GetItemAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        return f.FindAsset(f.FindAsset(mario->CurrentPowerupAsset).OnDamagedAsset == null ? smallPowerup : largePowerup);
    }
}
