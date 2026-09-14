using Photon.Deterministic;
using System;
using static Quantum.CommandChangeRules;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand, ILobbyCommand {

        public Rules EnabledChanges;

        public AssetRef<Map> Stage;
        public AssetRef<GamemodeAsset> Gamemode;
        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public int TimerMinutes;
        public bool TeamsEnabled;
        /*
        public bool CustomPowerupsEnabled;
        */

        public int StarFountain;
        public int CoinDeathPenalty;
        public StageChooseMode ChooseMode;
        public int TeamAttack;

        //KKT Mod
        //public bool BulbEnabled;

        public int StarFrequency;
        public bool RouletteEnabled;
        public bool HazardEnabled;
        public int MaxHazards;
        public int HazardFrequency;
        public int HeftyPercentage;
        public int HazardLifetime;

        public bool DisableStageRestrictions;
        public bool DisableComplexStageRestrictions;
        public bool EveryItemHasTheSameChance;

        public override void Serialize(BitStream stream) {
            if (stream.Writing) {
                stream.WriteUInt((uint) EnabledChanges);
            } else {
                EnabledChanges = (Rules) stream.ReadUInt();
            }

            stream.Serialize(ref Stage);
            stream.Serialize(ref Gamemode);
            stream.Serialize(ref StarsToWin);
            stream.Serialize(ref CoinsForPowerup);
            stream.Serialize(ref Lives);
            stream.Serialize(ref TimerMinutes);
            stream.Serialize(ref TeamsEnabled);
            /*
            stream.Serialize(ref CustomPowerupsEnabled);
            stream.Serialize(ref DrawOnTimeUp);
            */
            stream.Serialize(ref StarFountain);
            stream.Serialize(ref CoinDeathPenalty);
            stream.Serialize(ref TeamAttack);

            //KKT Mod
            stream.Serialize(ref HazardEnabled);
            //stream.Serialize(ref BulbEnabled);

            stream.Serialize(ref StarFrequency);
            stream.Serialize(ref RouletteEnabled);

            stream.Serialize(ref MaxHazards);
            stream.Serialize(ref HazardFrequency);
            stream.Serialize(ref HeftyPercentage);
            stream.Serialize(ref HazardLifetime);

            stream.Serialize(ref DisableStageRestrictions);
            stream.Serialize(ref DisableComplexStageRestrictions);
            stream.Serialize(ref EveryItemHasTheSameChance);

            if (stream.Writing) {
                stream.WriteByte((byte) ChooseMode);
            } else {
                ChooseMode = (StageChooseMode) stream.ReadByte();
            }
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost(f)) {
                // Only the host can change rules.
                return;
            }

            Rules rulesChanges = EnabledChanges;
            var rules = f.Global->Rules;
            bool gamemodeChanged = false;
            bool levelChanged = false;

            var DefaultRules = f.FindAsset(f.SimulationConfig.BaseRules).Rules.BaseRulesList[0].DefaultRules;

            if (rulesChanges.HasFlag(Rules.Gamemode)) {
                gamemodeChanged = rules.Gamemode != Gamemode;
                GameRules newRules = default;
                f.FindAsset(Gamemode).DefaultRules.Materialize(f, ref newRules);
                newRules.Stage = rules.Stage;
                newRules.ChooseMode = rules.ChooseMode;
                newRules.RandomDisabledStages = rules.RandomDisabledStages;
                GameRules tempRules = default;
                f.FindAsset(Gamemode).DefaultRules.Materialize(f, ref tempRules);
                tempRules.Stage = rules.Stage;

                rules.StarFrequency = DefaultRules.StarFrequency;

                rules = newRules;
            }

            if (rulesChanges.HasFlag(Rules.Stage)) {
                levelChanged = rules.Stage != Stage;
                rules.Stage = Stage;
            }
            if (rulesChanges.HasFlag(Rules.StarsToWin)) {
                rules.StarsToWin = StarsToWin;
            }
            if (rulesChanges.HasFlag(Rules.CoinsForPowerup)) {
                rules.CoinsForPowerup = CoinsForPowerup;
            }
            if (rulesChanges.HasFlag(Rules.Lives)) {
                rules.Lives = Lives;
            }
            if (rulesChanges.HasFlag(Rules.TimerMinutes)) {
                rules.TimerMinutes = TimerMinutes;
            }
            if (rulesChanges.HasFlag(Rules.TeamsEnabled)) {
                rules.TeamsEnabled = TeamsEnabled;
            }
            /*
            if (rulesChanges.HasFlag(Rules.CustomPowerupsEnabled)) {
                rules.CustomPowerupsEnabled = CustomPowerupsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.DrawOnTimeUp)) {
                rules.DrawOnTimeUp = DrawOnTimeUp;
            }
            */
            if (rulesChanges.HasFlag(Rules.StarFountain)) {
                rules.StarFountain = StarFountain;
            }
            if (rulesChanges.HasFlag(Rules.CoinDeathPenalty)) {
                rules.CoinDeathPenalty = CoinDeathPenalty;
            }
            if (rulesChanges.HasFlag(Rules.StageChooseMode)) {
                rules.ChooseMode = ChooseMode;
            }
            if (rulesChanges.HasFlag(Rules.TeamAttack)) {
                rules.TeamAttack = (TeamAttackOptions) TeamAttack;
            }

            //KKT Mod Toggle Rules
            if (rulesChanges.HasFlag(Rules.ToggleHazards)) {
                if (HazardEnabled) {
                    rules.ModifierHazardsEnabled = true;
                    rules.MaxHazards = DefaultRules.MaxHazards;
                    rules.HazardFrequency = DefaultRules.HazardFrequency;
                    rules.HeftyPercentage = DefaultRules.HeftyPercentage;
                    rules.HazardLifetime = DefaultRules.HazardLifetime;
                } else {
                    rules.ModifierHazardsEnabled = false;
                    rules.MaxHazards = 0;
                    rules.HazardFrequency = 0;
                    rules.HeftyPercentage = 0;
                    rules.HazardLifetime = 3;

                    //i coppied this from the CodeGen.Prototypes script idk if it works
                    if (DefaultRules.Hazards.Length == 0) {
                        rules.Hazards = default;
                    } else {
                        var list = f.AllocateList(out rules.Hazards, DefaultRules.Hazards.Length);
                        for (int i = 0; i < DefaultRules.Hazards.Length; ++i) {
                            Quantum.HazardList tmp = default;
                            DefaultRules.Hazards[i].Materialize(f, ref tmp);
                            list.Add(tmp);
                        }
                    }
                }
            }
            /*if (rulesChanges.HasFlag(Rules.ToggleBulb)) {
                if (BulbEnabled) {
                    rules.ModifierBulbEnabled = true;
                    rules.BulbAbilityCount = DefaultRules.BulbAbilityCount;
                } else {
                    rules.ModifierBulbEnabled = false;
                    rules.BulbAbilityCount = 0;
                }
            }*/
            //KKT Mod
            if (rulesChanges.HasFlag(Rules.StarFreq)) {
                rules.StarFrequency = StarFrequency;
            }
            if (rulesChanges.HasFlag(Rules.Roulette)) {
                rules.RouletteBlocksEnabled = RouletteEnabled;
            }

            if (rulesChanges.HasFlag(Rules.MaxHazards)) {
                rules.MaxHazards = MaxHazards;
            }
            if (rulesChanges.HasFlag(Rules.HazardFrequency)) {
                rules.HazardFrequency = HazardFrequency;
            }
            if (rulesChanges.HasFlag(Rules.HeftyPercentage)) {
                rules.HeftyPercentage = HeftyPercentage;
            }
            if (rulesChanges.HasFlag(Rules.HazardLifetime)) {
                rules.HazardLifetime = HazardLifetime;
            }

            if (rulesChanges.HasFlag(Rules.DisableStageRestrictions)) {
                rules.DisableStageRestrictions = DisableStageRestrictions;
            }
            if (rulesChanges.HasFlag(Rules.DisableComplexStageRestrictions)) {
                rules.DisableComplexStageRestrictions = DisableComplexStageRestrictions;
            }
            if (rulesChanges.HasFlag(Rules.EveryItemHasTheSameChance)) {
                rules.EveryItemHasTheSameChance = EveryItemHasTheSameChance;
            }

            f.Global->Rules = rules;
            f.Events.RulesChanged(gamemodeChanged, levelChanged);
        }

        //[Flags]
        public enum Rules : uint {
            None = 0,
            Stage = 1 << 0,
            Gamemode = 1 << 1,
            StarsToWin = 1 << 2,
            CoinsForPowerup = 1 << 3,
            Lives = 1 << 4,
            TimerMinutes = 1 << 5,
            TeamsEnabled = 1 << 6,
            //CustomPowerupsEnabled = 1 << 7,
            //DrawOnTimeUp = 1 << 8,
            StarFountain = 1 << 7, // only for Star Chasers
            CoinDeathPenalty = 1 << 8, // only for Coin Runners
            StageChooseMode = 1 << 9,
            TeamAttack = 1 << 10,

            //KKT Mod enable rules
            ToggleHazards = 1 << 11,
            ToggleBulb = 1 << 12,
            //KKT Mod rules
            StarFreq = 1 << 13,
            Roulette = 1 << 14,

            MaxHazards = 1 << 15,
            HazardFrequency = 1 << 16,
            HeftyPercentage = 1 << 17,
            HazardLifetime = 1 << 18,

            DisableStageRestrictions = 1 << 19,
            DisableComplexStageRestrictions = 1 << 20,
            EveryItemHasTheSameChance = 1 << 21,
        }
    }
}