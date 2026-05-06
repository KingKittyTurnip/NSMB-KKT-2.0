using Photon.Deterministic;
using System;
using System.Runtime.Remoting.Contexts;

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
        public bool DrawOnTimeUp;
        */
        public byte StarFrequency;
        //TODO: add the rest of the settings (also note the buttons don't seem to work, might be lack of a label)

        public bool CoinsEnabled;
        public bool HazardEnabled;
        public bool LivesEnabled;
        public bool TimerEnabled;
        public bool BulbEnabled;
        public bool ExtrasEnabled;

        public override void Serialize(BitStream stream) {
            if (stream.Writing) {
                stream.WriteUShort((ushort) EnabledChanges);
            } else {
                EnabledChanges = (Rules) stream.ReadUShort();
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
            //KKT Mod
            stream.Serialize(ref StarFrequency);

            stream.Serialize(ref CoinsEnabled);
            stream.Serialize(ref HazardEnabled);
            stream.Serialize(ref LivesEnabled);
            stream.Serialize(ref TimerEnabled);
            stream.Serialize(ref BulbEnabled);
            stream.Serialize(ref ExtrasEnabled);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || !playerData->IsRoomHost) {
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

                GameRules tempRules = default;
                //f.FindAsset(Gamemode).DefaultRules.Materialize(f, ref tempRules);
                if (f.FindAsset(Gamemode) is StarChasersGamemode) {
                    rules.StarsToWin = DefaultRules.StarsToWin;
                    rules.StarFrequency = DefaultRules.StarFrequency;
                } else if (f.FindAsset(Gamemode) is CoinRunnersGamemode) {
                    rules.StarsToWin = 0;
                    rules.StarFrequency = DefaultRules.StarFrequency;
                } else if (false) {
                    rules.StarsToWin = DefaultRules.StarsToWin;
                } else if (false) {

                }
                tempRules.Stage = rules.Stage;

                rules = tempRules;
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
                rules.ModifierTeamsEnabled = TeamsEnabled;
            }
            /*
            if (rulesChanges.HasFlag(Rules.CustomPowerupsEnabled)) {
                rules.CustomPowerupsEnabled = CustomPowerupsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.DrawOnTimeUp)) {
                rules.DrawOnTimeUp = DrawOnTimeUp;
            }
            */
            //KKT Mod Toggle Rules
            if (rulesChanges.HasFlag(Rules.ToggleCoins)) {
                UnityEngine.Debug.Log("Toggle coins: " + CoinsEnabled);
                if (CoinsEnabled) {
                    rules.ModifierCoinsEnabled = true;
                    rules.CoinsForPowerup = DefaultRules.CoinsForPowerup;
                    rules.RouletteBlocksEnabled = DefaultRules.RouletteBlocksEnabled;
                } else {
                    rules.ModifierCoinsEnabled = false;
                    rules.CoinsForPowerup = 0;
                    rules.RouletteBlocksEnabled = false;

                    //i coppied this from the CodeGen.Prototypes script idk if it works
                    if (DefaultRules.Items.Length == 0) {
                        rules.Items = default;
                    } else {
                        var list = f.AllocateList(out rules.Items, DefaultRules.Items.Length);
                        for (int i = 0; i < DefaultRules.Items.Length; ++i) {
                            Quantum.ItemList tmp = default;
                            DefaultRules.Items[i].Materialize(f, ref tmp);
                            list.Add(tmp);
                        }
                    }
                }
                UnityEngine.Debug.Log("Toggled!: " + rules.ModifierCoinsEnabled);
            }
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
            if (rulesChanges.HasFlag(Rules.ToggleLives)) {
                if (LivesEnabled) {
                    rules.ModifierLivesEnabled = true;
                    rules.Lives = DefaultRules.Lives;
                } else {
                    rules.ModifierLivesEnabled = false;
                    rules.Lives = 0;
                }
            }
            if (rulesChanges.HasFlag(Rules.ToggleTimer)) {
                if (TimerEnabled) {
                    rules.ModifierTimerEnabled = true;
                    rules.TimerMinutes = 8; //Forced Default
                } else {
                    rules.ModifierTimerEnabled = false;
                    rules.TimerMinutes = 0;
                }
            }
            if (rulesChanges.HasFlag(Rules.ToggleBulb)) {
                if (BulbEnabled) {
                    rules.ModifierBulbEnabled = true;
                    rules.BulbAbilityCount = DefaultRules.BulbAbilityCount;
                } else {
                    rules.ModifierBulbEnabled = false;
                    rules.BulbAbilityCount = 0;
                }
            }
            if (rulesChanges.HasFlag(Rules.ToggleTeams)) {
                if (TeamsEnabled) {
                    rules.ModifierTeamsEnabled = true;
                } else {
                    rules.ModifierTeamsEnabled = false;
                }
            }
            //KKT Mod
            if (rulesChanges.HasFlag(Rules.StarFreq)) {
                rules.StarFrequency = StarFrequency;
            }

            f.Global->Rules = rules;
            f.Events.RulesChanged(gamemodeChanged, levelChanged);

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }
        }

        [Flags]
        public enum Rules : int {
            None = 0,
            Stage = 1 << 0,
            Gamemode = 1 << 1,
            StarsToWin = 1 << 2,
            CoinsForPowerup = 1 << 3,
            Lives = 1 << 4,
            TimerMinutes = 1 << 5,
            TeamsEnabled = 1 << 6,
            CustomPowerupsEnabled = 1 << 7, //deprecated
            DrawOnTimeUp = 1 << 8, //deprecated
            //KKT Mod enable rules
            ToggleCoins = 1 << 9,
            ToggleHazards = 1 << 10,
            ToggleLives = 1 << 11,
            ToggleTimer = 1 << 12,
            ToggleTeams = 1 << 13,
            ToggleBulb = 1 << 14,
            ToggleExtras = 1 << 15,
            //KKT Mod rules
            StarFreq = 1 << 16,
            //StarCoinFreq = 1 << 17,
        }
    }
}