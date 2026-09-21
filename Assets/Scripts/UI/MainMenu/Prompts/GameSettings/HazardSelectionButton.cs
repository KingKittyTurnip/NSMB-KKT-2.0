using NSMB.UI.MainMenu.Submenus.InRoom;
using Quantum;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static LoopingMusicData;
using static NSMB.UI.MainMenu.Submenus.InRoom.NumberChangeableRule;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class HazardSelectionButton : Selectable, ISubmitHandler, IPointerClickHandler {
        //this is the button in the list that displays the hazards, it doesn't do much & can be interacted with by the non-host


        //---Public Variables
        [NonSerialized] public int HazardId;
        [NonSerialized] public int SlotInHazardList;

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameSettingsPromptSubmenu settingSubmenu;

        [SerializeField] private Image hazardIcon;
        [SerializeField] private TMP_Text hazardName;
        [SerializeField] private Image hazardIconBg;
        [SerializeField] private Image hazardBg;

        [SerializeField] private TMP_Text DescriptionName;
        [SerializeField] private TMP_Text DescriptionBox;
        //modifier buttons

        public void Initialize(int hazardId, int slot) {
            this.HazardId = hazardId;
            this.SlotInHazardList = slot;
            //TranslationManager.OnLanguageChanged += OnLanguageChanged;
            InitializeText();
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
            //scroll.verticalNormalizedPosition = scroll.ScrollToCenter((RectTransform) transform, false);
        }

        public unsafe void OnSubmit(BaseEventData eventData) {
            eventData.Use();

            OnUpdateDescriptionBox();
        }
        public unsafe void OnUpdateDescriptionBox() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;
            if (stuff[SlotInHazardList] == null)
                return;//erm

            Select();

            //set description box
            DescriptionName.text = hazardName.text;
            DescriptionBox.text = stuff[SlotInHazardList].Description;

            //update game settings
            settingSubmenu.CurrentlySelectedObject = HazardId;

            //create modifier buttons
            List<NumberValueTranslationOverride> h = new List<NumberValueTranslationOverride>();
            SetExtra((settingSubmenu.EditingItems ? settingSubmenu.ItemExtra : settingSubmenu.HazardExtra)[0], stuff[SlotInHazardList].ValueA);
            h = new List<NumberValueTranslationOverride>();
            SetExtra((settingSubmenu.EditingItems ? settingSubmenu.ItemExtra : settingSubmenu.HazardExtra)[1], stuff[SlotInHazardList].ValueB);
            h = new List<NumberValueTranslationOverride>();
            SetExtra((settingSubmenu.EditingItems ? settingSubmenu.ItemExtra : settingSubmenu.HazardExtra)[2], stuff[SlotInHazardList].ValueC);
            h = new List<NumberValueTranslationOverride>();
            SetExtra((settingSubmenu.EditingItems ? settingSubmenu.ItemExtra : settingSubmenu.HazardExtra)[3], stuff[SlotInHazardList].ValueD);

            void SetExtra(NumberChangeableRule Extra, HValue hValue) {
                Extra.transform.parent.gameObject.SetActive(hValue.ButtonName != "");
                Extra.ExtraName.text = hValue.ButtonName;
                Extra.maxValue = hValue.ValueRange.Y;
                for (int i = 0; i < hValue.valuenames.Length; i++) {
                    h.Add(new NumberValueTranslationOverride());
                    h[i] = new NumberValueTranslationOverride();
                    h[i].Value = i;
                    h[i].Key = hValue.valuenames[i];
                }
                Extra.translationOverrides = h;
                Extra.FindValue(ref game.Frames.Predicted.Global->Rules);
            }

            canvas.PlayConfirmSound();
        }

        public void OnPointerClick(PointerEventData eventData) {
            OnSubmit(eventData);
        }

        private void OnRulesChanged(EventRulesChanged e) {
            //UpdateEnabledVisuals();
        }

        public unsafe void InitializeText() {
            QuantumGame game = QuantumRunner.DefaultGame;
            Frame f = game.Frames.Predicted;
            var stuff = f.FindAsset(f.SimulationConfig.BaseRules).Rules.ListOfAvalibleObjects;
            var rules = f.ResolveList(settingSubmenu.EditingItems ? f.Global->Rules.Items : f.Global->Rules.Hazards);

            hazardIcon.sprite = stuff[SlotInHazardList].Icon;
            hazardIconBg.color = stuff[SlotInHazardList].type switch {
                ObjectPrimaryType.Powerup => new Color(0.194f, 0.339f, 0.679f),
                ObjectPrimaryType.Hazard => new Color(1, 0.522f, 0),
                ObjectPrimaryType.Object => new Color(0.617f, 0.893f, 0),
                ObjectPrimaryType.Bonus => new Color(0.894f, 0, 0.684f),
                _ => Color.white,

            };

            string text = stuff[SlotInHazardList].Name;
            int number = 1;
            for (int i = 0; i <= HazardId-1; i++) {
                if (rules[i].PrototypeRef == rules[HazardId].PrototypeRef) {
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
