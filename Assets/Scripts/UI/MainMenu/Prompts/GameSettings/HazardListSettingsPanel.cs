using System;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class HazardListSettingsPanel : GameSettingsPanel {

        [SerializeField] private bool IsItemsTab;

        public unsafe override void OnEnable() {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
            Canvas.ForceUpdateCanvases();
            //submenu.Canvas.EventSystem.SetSelectedGameObject(buttons.FirstOrDefault(ssb => ssb.stage == stage).gameObject ?? DefaultSelection);
            submenu.EditingItems = IsItemsTab;
            submenu.PopulateHazardOrItems(IsItemsTab, true);
        }
    }
}
