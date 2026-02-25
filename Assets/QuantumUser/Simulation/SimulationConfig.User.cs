using UnityEngine;

namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public AssetRef<StageTile> InvisibleSolidTile;

        public AssetRef<GamemodeAsset>[] AllGamemodes;
        public AssetRef<GamemodeAsset> DefaultGamemode;
        public AssetRef<Map>[] AllStages;
        public AssetRef<CharacterAsset>[] CharacterDatas;
        public AssetRef<PaletteSet>[] Palettes;
        public AssetRef<TeamAsset>[] Teams;
        [Header("Projectiles")]
        public AssetRef<EntityPrototype> FireballPrototype;
        public AssetRef<EntityPrototype> IceballPrototype, HammerPrototype, BlockBumpPrototype, IceBlockPrototype, CannonBoxBulletPrototype;
        [Header("Technical")]
        public AssetRef<EntityPrototype> MainSpawn;
        public AssetRef<EntityPrototype> HazardSpawn;
        [Header("Enenties To Create Via Some Random Script")]
        public AssetRef<EntityPrototype> VoidWallWall;
        public AssetRef<EntityPrototype> StarballGoal;
        public AssetRef<EntityPrototype> Chainchomp;

        [Header("Global Music")]
        public AssetRef<LoopingMusicData>[] BossMusic;

        [Header("Gamemode Shenanigans")]
        public AssetRef<RulesBaser> BaseRules;
        public AssetRef<CurrentHazards> CurrentHazards;
    }
}