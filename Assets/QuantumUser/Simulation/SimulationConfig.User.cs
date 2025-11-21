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
        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, HammerPrototype, BlockBumpPrototype, IceBlockPrototype, CannonBoxBulletPrototype;
        public AssetRef<EntityPrototype> MainSpawn, HazardSpawn;
        public AssetRef<EntityPrototype> StarballGoal;

        [Header("Global Music")]
        public AssetRef<LoopingMusicData>[] BossMusic;

        [Space]
        public AssetRef<CurrentHazards> CurrentHazards;
    }
}