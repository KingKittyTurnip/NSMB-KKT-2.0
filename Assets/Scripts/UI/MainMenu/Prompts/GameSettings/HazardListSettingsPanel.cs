using System;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class HazardListSettingsPanel : GameSettingsPanel {

        [SerializeField] private bool IsItemsTab;
        [SerializeField] private GameSettingsPromptSubmenu settingSubmenu;

        public unsafe override void OnEnable() {
            settingSubmenu.EditingItems = IsItemsTab;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
            Canvas.ForceUpdateCanvases();
            //submenu.Canvas.EventSystem.SetSelectedGameObject(buttons.FirstOrDefault(ssb => ssb.stage == stage).gameObject ?? DefaultSelection);
        }
    }
}
