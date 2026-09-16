using Quantum;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class HazardListSettingsPanel : GameSettingsPanel {

        public unsafe override void OnEnable() {
            var buttons = root.GetComponentsInChildren<StageSelectionButton>(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
            Canvas.ForceUpdateCanvases();
            //submenu.Canvas.EventSystem.SetSelectedGameObject(buttons.FirstOrDefault(ssb => ssb.stage == stage).gameObject ?? DefaultSelection);
        }
    }
}
