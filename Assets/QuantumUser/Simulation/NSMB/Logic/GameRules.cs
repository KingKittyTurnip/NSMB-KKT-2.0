using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct GameRules {

        public readonly bool IsStageCoinsEnabled => CoinsForPowerup > 0;
        public readonly bool IsLivesEnabled => Lives > 0;
        public readonly bool IsTimerEnabled => TimerMinutes > 0;

    }
}