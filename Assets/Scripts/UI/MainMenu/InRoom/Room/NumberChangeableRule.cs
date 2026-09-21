using NSMB.UI.Translation;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class NumberChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue => (int) value < maxValue;
        public override bool CanDecreaseValue => (int) value > minValue;

        //---Serialized Variables
        [SerializeField] protected int minValue = 0;
        [SerializeField] public int maxValue = 20;
        [SerializeField] protected int step = 1;
        [SerializeField] protected bool minimumValueIsOff, applyPrefixSuffixWhenOff = true;
        [SerializeField] public List<NumberValueTranslationOverride> translationOverrides;

        protected override void IncreaseValueInternal() {
            int intValue = (int) value;
            value = Mathf.Clamp(intValue + step, minValue, maxValue);

            if (intValue != (int) value) {
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override void DecreaseValueInternal() {
            int intValue = (int) value;
            value = Mathf.Clamp(intValue - step, minValue, maxValue);

            if (intValue != (int) value) {
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            if (settingSubmenu != null) {
                //hazard extras rule, ignore all else
                QuantumGame game2 = QuantumRunner.DefaultGame;
                PlayerRef host2 = game2.Frames.Predicted.Global->Host;
                //if (!game2.PlayerIsLocal(host2)) {
                //    return;
                //}
                Frame f = game2.Frames.Predicted;
                //var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects[settingSubmenu.CurrentlySelectedObject];
                var rules = f.ResolveList(settingSubmenu.EditingItems ? f.Global->Rules.Items : f.Global->Rules.Hazards);


                int a = (byte) (ExtrasId == 0 ? (int) value : rules[settingSubmenu.CurrentlySelectedObject].ExtraSlotA);
                int b = (byte) (ExtrasId == 1 ? (int) value : rules[settingSubmenu.CurrentlySelectedObject].ExtraSlotB);
                int c = (byte) (ExtrasId == 2 ? (int) value : rules[settingSubmenu.CurrentlySelectedObject].ExtraSlotC);
                int d = (byte) (ExtrasId == 3 ? (int) value : rules[settingSubmenu.CurrentlySelectedObject].ExtraSlotD);

                CommandChangeHazards cmd2 = new CommandChangeHazards {
                    EditingItems = settingSubmenu.EditingItems,
                    Index = settingSubmenu.CurrentlySelectedObject,
                    RemoveSingle = false,
                    RemoveAll = false,
                    //id
                    PrototypeRefId = rules[settingSubmenu.CurrentlySelectedObject].PrototypeRef,
                    TeamId = 255, //team code isn't used
                                  //Specific Data
                    ValueA = a,
                    ValueB = b,
                    ValueC = c,
                    ValueD = d,
                    UpdateUi = false,
                };

                game2.SendCommand(game2.GetLocalPlayerSlots()[game2.GetLocalPlayers().IndexOf(host2)], cmd2);
                return;
            }
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            switch (ruleType) {
            case CommandChangeRules.Rules.StarsToWin:
                cmd.StarsToWin = (int) value;
                break;
            case CommandChangeRules.Rules.CoinsForPowerup:
                cmd.CoinsForPowerup = (int) value;
                break;
            case CommandChangeRules.Rules.Lives:
                cmd.Lives = (int) value;
                break;
            case CommandChangeRules.Rules.TimerMinutes:
                cmd.TimerMinutes = (int) value;
                break;
            case CommandChangeRules.Rules.StarFountain:
                cmd.StarFountain = (int) value;
                break;
            case CommandChangeRules.Rules.CoinDeathPenalty:
                cmd.CoinDeathPenalty = (int) value;
                break;
            case CommandChangeRules.Rules.TeamAttack:
                cmd.TeamAttack = (int) value;
                break;

            case CommandChangeRules.Rules.StarFreq:
                cmd.StarFrequency = (int) value;
                break;
            case CommandChangeRules.Rules.MaxHazards:
                cmd.MaxHazards = (int) value;
                break;
            case CommandChangeRules.Rules.HazardFrequency:
                cmd.HazardFrequency = (int) value;
                break;
            case CommandChangeRules.Rules.HeftyPercentage:
                cmd.HeftyPercentage = (int) value;
                break;
            case CommandChangeRules.Rules.HazardLifetime:
                cmd.HazardLifetime = (int) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (game.PlayerIsLocal(host)) {
                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        public override void UpdateLabel() {
            TranslationManager tm = GlobalController.Instance.translationManager;
            if (value is int intValue) {
                string text;
                bool applyPrefixSuffix;
                if (translationOverrides.FirstOrDefault(to => to.Value == intValue) is { } translationOverride) {
                    text = tm.GetTranslation(translationOverride.Key);
                    applyPrefixSuffix = false;
                } else {
                    if (minimumValueIsOff && intValue == minValue) {
                        text = tm.GetTranslation("ui.generic.off");
                        applyPrefixSuffix = applyPrefixSuffixWhenOff;
                    } else {
                        text = intValue.ToString();
                        applyPrefixSuffix = true;
                    }
                }

                if (applyPrefixSuffix) {
                    text = labelPrefix + text + labelSuffix;
                }
                label.text = text;
            }
        }

        [Serializable]
        public class NumberValueTranslationOverride {
            public int Value;
            public string Key;
        }
    }
}