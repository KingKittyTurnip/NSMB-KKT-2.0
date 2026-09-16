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
    public class HazardSelectionButton : Selectable, ISubmitHandler, IPointerClickHandler {
        //this is the button in the list that displays the hazards, it doesn't do much & can be interacted with by the non-host


        //---Public Variables
        [NonSerialized] public int HazardId;
        [NonSerialized] public int SlotInHazardList;

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private ScrollRect scroll;

        [SerializeField] private Image hazardIcon;
        [SerializeField] private TMP_Text hazardName;
        [SerializeField] private Image hazardIconBg;
        //[SerializeField] private Image hazardBg;

        [SerializeField] private TMP_Text DescriptionName;
        [SerializeField] private TMP_Text DescriptionBox;
        //modifier buttons

        public void Initialize(int hazardId, int slot) {
            this.HazardId = hazardId;
            this.SlotInHazardList = slot;
            //TranslationManager.OnLanguageChanged += OnLanguageChanged;
            UpdateText();
        }

        protected override void OnDestroy() {
            //TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        protected override void OnEnable() {
            base.OnEnable();
        }

        protected override void Start() {
            base.Start();
            //QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public override void OnSelect(BaseEventData eventData) {
            base.OnSelect(eventData);
            scroll.verticalNormalizedPosition = scroll.ScrollToCenter((RectTransform) transform, false);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            eventData.Use();

            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects[HazardId];

            DescriptionName.text = hazardName.text;
            DescriptionBox.text = stuff.Description;
            //create modifier buttons

            canvas.PlayConfirmSound();
        }

        public void OnPointerClick(PointerEventData eventData) {
            OnSubmit(eventData);
        }

        private void OnRulesChanged(EventRulesChanged e) {
            //UpdateEnabledVisuals();
        }

        public unsafe void UpdateText() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;
            var rules = f.ResolveList(f.Global->Rules.Hazards);

            hazardIcon.sprite = stuff[HazardId].Icon;
            hazardIconBg.color = stuff[HazardId].type switch {
                ObjectPrimaryType.Powerup => new Color(0.194f, 0.339f, 0.679f),
                ObjectPrimaryType.Hazard => new Color(1, 0.522f, 0),
                ObjectPrimaryType.Object => new Color(0.617f, 0.893f, 0),
                ObjectPrimaryType.Bonus => new Color(0.894f, 0, 0.684f),
                _ => Color.white,

            };

            string text = stuff[HazardId].Name;
            int number = 1;
            for (int i = 0; i <= SlotInHazardList-1; i++) {
                if (rules[i].PrototypeRef == rules[SlotInHazardList].PrototypeRef) {
                    number++;
                }
            }
            if (number > 1) {
                text += " (" + number + ")";
            }
            hazardName.text = text;
        }

        //private void OnLanguageChanged(TranslationManager tm) {
            //UpdateText();
        //}
    }
}
