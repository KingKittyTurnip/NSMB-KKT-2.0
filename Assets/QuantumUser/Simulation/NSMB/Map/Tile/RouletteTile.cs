using Photon.Deterministic;
using Quantum;

public unsafe class RouletteTile : BreakableBrickTile {
        /* Deprecated
    public override unsafe CoinItemAsset GetItemAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
        return gamemode.GetRandomItem(f, mario, true);
        return null;
    }
        */
    //KKT Mod
    public StageTileInstance resultTile;
    public override bool Interact(Frame f, EntityRef entity, InteractionDirection direction, IntVector2 tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        if (base.Interact(f, entity, direction, tilePosition, tileInstance, out playBumpSound)) {
            return true;
        }

        bool allowSelfDamage = false;
        if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
            && f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
            && koopa->IsKicked
            && f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
            && f.Exists(holdable->PreviousHolder)) {

            // Talk to my dad, his name is mario :)
            f.Unsafe.TryGetPointer(holdable->PreviousHolder, out mario);
            entity = holdable->PreviousHolder;
            allowSelfDamage = true;
        }

        if (mario == null) {
            playBumpSound = true;
            return false;
        }

        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
        gamemode.NEWGetRandomItem(f, mario, true, out var entityPrototype, out var extraA, out var extraB, out var extraC, out var extraD);
        Bump(f, null, tilePosition, resultTile, direction, entity, allowSelfDamage, entityPrototype);
        playBumpSound = false;
        return false;
    }
}