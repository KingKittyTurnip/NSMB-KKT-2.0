using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct GameRules {
        public readonly bool IsLivesEnabled => Lives > 0;
        public readonly bool IsTimerEnabled => TimerMinutes > 0;

        //KKT Mod
        public readonly bool IsStarsEnabled => StarsToWin > 0;
        public readonly bool IsCoinsEnabled => CoinsForPowerup > 0;
        public readonly FP RealHeftyPercent => HeftyPercentage switch {//hardcoded nonsense
            0 => -1,
            1 => FP._0_01,
            2 => FP._0_05,
            3 => Constants._0_12,
            4 => FP._0_25,
            5 => FP._0_50,
            6 => FP._0_75,
            7 => FP._1,
            8 => FP._1_50,
            9 => FP._2,
            10 => FP._3,
            11 => FP._5,
            12 => FP._10,
            _ => -1,
        };

        public readonly bool CanIgnoreStageRestrictions(Frame f, VersusStageData stage) => (f.Global->Rules.DisableStageRestrictions && !stage.StageIsComplex) || f.Global->Rules.DisableComplexStageRestrictions;

        //KKT Mod Won't use This
        public readonly bool IsCoinItemDisabled(Frame f, AssetRef<CoinItemAsset> coinItem) {
            if (f.TryResolveDictionary(CoinItemCustomSpawnWeights, out var customWeights)) {
                if (customWeights.TryGetValue(coinItem, out FP customWeight)) {
                    return customWeight <= FP.UseableMin;
                }
            }
            return false;
        }
    }
}