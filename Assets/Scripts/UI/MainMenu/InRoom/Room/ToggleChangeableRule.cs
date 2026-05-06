using NSMB.UI.Translation;
using Quantum;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ToggleChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => !(bool) value;
        public override bool CanDecreaseValue => (bool) value;

        protected override void IncreaseValueInternal() {
            if (!(bool) value) {
                value = true;
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override unsafe void DecreaseValueInternal() {
            if ((bool) value) {
                value = false;
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            switch (ruleType) {
            /*
            case CommandChangeRules.Rules.CustomPowerupsEnabled:
                cmd.CustomPowerupsEnabled = (bool) value;
                break;
                */
            case CommandChangeRules.Rules.TeamsEnabled:
                cmd.TeamsEnabled = (bool) value;
                break;
            //KKT Mod
            case CommandChangeRules.Rules.ToggleCoins:
                cmd.CoinsEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.ToggleHazards:
                cmd.HazardEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.ToggleLives:
                cmd.LivesEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.ToggleTimer:
                cmd.TimerEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.ToggleBulb:
                cmd.BulbEnabled = (bool) value;
                break;
            case CommandChangeRules.Rules.ToggleExtras:
                cmd.ExtrasEnabled = (bool) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            
            if (game.PlayerIsLocal(host)) {
                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        protected override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is bool boolValue) {
                label.text = labelPrefix + tm.GetTranslation(boolValue ? "ui.generic.on" : "ui.generic.off");
            }
        }
    }
}
