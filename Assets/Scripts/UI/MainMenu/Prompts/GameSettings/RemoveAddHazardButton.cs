using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class RemoveAddHazardButton : Selectable, ISubmitHandler, IPointerClickHandler {

        //---Public Variables
        [SerializeField] private GameSettingsPromptSubmenu settingSubmenu;

        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private bool Adding;
        [SerializeField] private int AddId = -1;
        [SerializeField] private GameObject HazardPickerPrompt;

        [SerializeField] private Image hazardIcon;
        [SerializeField] private TMP_Text hazardName;
        [SerializeField] private Image hazardIconBg;
        protected override void OnDestroy() {
            //TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        protected override void OnEnable() {
            base.OnEnable();
        }

        protected override void Start() {
            base.Start();
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public unsafe void Initialize(Frame f, int id) {
            AddId = id;

            //coppied
            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;
            var rules = f.ResolveList(f.Global->Rules.Hazards);

            hazardIcon.sprite = stuff[AddId].Icon;
            hazardIconBg.color = stuff[AddId].type switch {
                ObjectPrimaryType.Powerup => new Color(0.194f, 0.339f, 0.679f),
                ObjectPrimaryType.Hazard => new Color(1, 0.522f, 0),
                ObjectPrimaryType.Object => new Color(0.617f, 0.893f, 0),
                ObjectPrimaryType.Bonus => new Color(0.894f, 0, 0.684f),
                _ => Color.white,

            };

            hazardName.text = stuff[AddId].Name;
        }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            if (Adding) {
                if (AddId == -1) {
                    //open prompt
                    HazardPickerPrompt.SetActive(true);
                    settingSubmenu.PopulateHazardPicker(settingSubmenu.EditingItems);
                    canvas.PlaySound(SoundEffect.UI_WindowOpen);
                } else {
                    HazardPickerPrompt.SetActive(false);
                    eventData.Use();

                    QuantumGame game = QuantumRunner.DefaultGame;
                    PlayerRef host = game.Frames.Predicted.Global->Host;
                    if (!game.PlayerIsLocal(host)) {
                        canvas.PlaySound(SoundEffect.UI_Error);
                        return;
                    }

                    //Send Command
                    Frame f = game.Frames.Predicted;
                    DeterministicCommand cmd;
                    var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects[AddId];
                    var rules = f.ResolveList(settingSubmenu.EditingItems ? f.Global->Rules.Items : f.Global->Rules.Hazards);

                    cmd = new CommandChangeHazards {
                        EditingItems = settingSubmenu.EditingItems,
                        Index = rules.Count,
                        RemoveSingle = false,
                        RemoveAll = false,
                        //id
                        PrototypeRefId = AddId,
                        TeamId = 255, //team code isn't used
                                      //Specific Data
                        ValueA = stuff.ValueA.BaseValue,
                        ValueB = stuff.ValueB.BaseValue,
                        ValueC = stuff.ValueC.BaseValue,
                        ValueD = stuff.ValueD.BaseValue,
                    };

                    game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);

                    //Ui
                    settingSubmenu.PopulateHazardOrItems(settingSubmenu.EditingItems, false);
                }

            } else {
                //remove current hazard
                eventData.Use();

                QuantumGame game = QuantumRunner.DefaultGame;
                PlayerRef host = game.Frames.Predicted.Global->Host;
                if (!game.PlayerIsLocal(host)) {
                    canvas.PlaySound(SoundEffect.UI_Error);
                    return;
                }

                //Send Command
                Frame f = game.Frames.Predicted;
                DeterministicCommand cmd;

                cmd = new CommandChangeHazards {
                    RemoveSingle = true,
                    EditingItems = settingSubmenu.EditingItems,
                    Index = settingSubmenu.CurrentlySelectedObject
                };

                game.SendCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);

                //Ui
                settingSubmenu.PopulateHazardOrItems(settingSubmenu.EditingItems, false);

                //canvas.PlayConfirmSound();
            }
        }

        public void OnPointerClick(PointerEventData eventData) {
            OnSubmit(eventData);
        }

        private void OnRulesChanged(EventRulesChanged e) {
            //UpdateEnabledVisuals();
        }

        private void OnLanguageChanged(TranslationManager tm) {
            //UpdateText();
        }
    }
}
