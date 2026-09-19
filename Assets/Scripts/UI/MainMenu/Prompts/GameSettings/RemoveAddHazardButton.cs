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

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            if (Adding) {
                //open prompt
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
