using NSMB.Networking;
using NSMB.UI.MainMenu;
using Quantum;
using UnityEngine;

public class RulesetSaverLoader : MonoBehaviour
{
    [SerializeField] private MainMenuCanvas Canvas;
    private const string CODE_SEPARATOR = "-";
    private const string CODE_List_SEPARATOR = ".";
    private const string CODE_List_SEPARATOR2 = ",";
    private const string CODE_VERSION = "K0";

    private const int MEGALIST_MAX = 64, EXTRALISTMAX = 10;

    public void OnSavePressed() {
        GUIUtility.systemCopyBuffer = RulesetToCode();
        Canvas.PlaySound(SoundEffect.UI_Decide);
    }

    public unsafe void OnLoadPressed() {
        QuantumGame game = NetworkHandler.Game;
        PlayerRef host = game.Frames.Predicted.Global->Host;
        if (!game.PlayerIsLocal(host)) {
            Canvas.PlaySound(SoundEffect.UI_Error);
        }
        if (CodeToRuleset(GUIUtility.systemCopyBuffer.ToUpper())) {
            // succeeded...
            Canvas.PlaySound(SoundEffect.UI_Decide);
            Canvas.GoBack();
            Canvas.GoBack();
        } else {
            // failed!!
            Canvas.PlaySound(SoundEffect.UI_Error);
        }
    }

    public static unsafe string RulesetToCode() {
        var code = "";
        Frame f = NetworkHandler.Game.Frames.Predicted;
        GameRules rules = f.Global->Rules;
        var disabledstages = f.ResolveHashSet(rules.RandomDisabledStages);
        var items = f.ResolveList(rules.Items);
        var hazards = f.ResolveList(rules.Hazards);

        //code += f.SimulationConfig.AllGamemodes.IndexOf(rules.Gamemode) + CODE_SEPARATOR; //gamemode doesn't exist
        /*code += rules.Stage.Id + CODE_SEPARATOR;
        code += (int) rules.ChooseMode + CODE_SEPARATOR;
        var disabledstagesCount = disabledstages.Count;
        foreach (var disabledstage in disabledstages) {
            code += disabledstage.Id + CODE_List_SEPARATOR2;
            disabledstagesCount--;
            if (disabledstagesCount > 0) code += CODE_List_SEPARATOR;
        }*/

        code += rules.StarsToWin + CODE_SEPARATOR;
        code += rules.CoinsForPowerup + CODE_SEPARATOR;
        code += rules.Lives + CODE_SEPARATOR;
        code += rules.TimerMinutes + CODE_SEPARATOR;
        code += BTI(rules.TeamsEnabled) + CODE_SEPARATOR;
        code += BTI(rules.ModifierHazardsEnabled) + CODE_SEPARATOR;

        code += (int) rules.TeamAttack + CODE_SEPARATOR;
        code += rules.StarFountain + CODE_SEPARATOR;

        code += rules.StarFrequency + CODE_SEPARATOR;

        code += BTI(rules.RouletteBlocksEnabled) + CODE_SEPARATOR;
        var itemCount = items.Count;
        if (itemCount <= MEGALIST_MAX) {
            foreach (var item in items) {
                code += item.PrototypeRef + CODE_List_SEPARATOR2;
                code += item.ExtraSlotA + CODE_List_SEPARATOR2;
                code += item.ExtraSlotB + CODE_List_SEPARATOR2;
                code += item.ExtraSlotC + CODE_List_SEPARATOR2;
                code += item.ExtraSlotD + CODE_List_SEPARATOR2;
                //code += item.Team + CODE_List_SEPARATOR2;//unused, when added we much put this after

                itemCount--;
                if (itemCount > 0) 
                    code += CODE_List_SEPARATOR;
            }
        }
        code += CODE_SEPARATOR;

        code += rules.MaxHazards + CODE_SEPARATOR;
        code += rules.HazardFrequency + CODE_SEPARATOR;
        code += rules.HeftyPercentage + CODE_SEPARATOR;
        code += rules.HazardLifetime + CODE_SEPARATOR;
        var hazardCount = hazards.Count;
        if (hazardCount <= MEGALIST_MAX) {
            foreach (var hazard in hazards) {
                code += hazard.PrototypeRef + CODE_List_SEPARATOR2;
                code += hazard.ExtraSlotA + CODE_List_SEPARATOR2;
                code += hazard.ExtraSlotB + CODE_List_SEPARATOR2;
                code += hazard.ExtraSlotC + CODE_List_SEPARATOR2;
                code += hazard.ExtraSlotD + CODE_List_SEPARATOR2;
                //code += hazard.Team + CODE_List_SEPARATOR2;//unused, when added we much put this after

                hazardCount--;
                if (hazardCount > 0) 
                    code += CODE_List_SEPARATOR;
            }
        }
        code += CODE_SEPARATOR;

        code += BTI(rules.DisableStageRestrictions) + CODE_SEPARATOR;
        code += BTI(rules.DisableComplexStageRestrictions) + CODE_SEPARATOR;
        code += BTI(rules.EveryItemHasTheSameChance) + CODE_SEPARATOR;

        //code += rules.CoinDeathPenalty + CODE_SEPARATOR; //coinrunners doesn't exist
        //dictionary<AssetRef<CoinItemAsset>, FP> CoinItemCustomSpawnWeights; //do not save.

        //bulb isn't implemented
        //code += rules.ModifierBulbEnabled + CODE_SEPARATOR;
        //code += rules.BulbAbilityCount + CODE_SEPARATOR;
        //code += rules.HostControl + CODE_SEPARATOR;

        code += CODE_VERSION + CODE_SEPARATOR;

        var sum = 0;
        foreach (var c in code) {
            sum += c;
        }
        code += (sum % 256).ToString("X2");

        return code;

        string BTI(bool boolean) {
            return boolean ? "1" : "0";
        }
    }

    private unsafe bool CodeToRuleset(string code) {
        // basically the reverse of above...
        Frame f = NetworkHandler.Game.Frames.Predicted;
        GameRules rules = f.Global->Rules;
        QuantumGame game = QuantumRunner.DefaultGame;
        int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(game.Frames.Predicted.Global->Host)];
        int code_version;
        
        var parts = code.Split(CODE_SEPARATOR);
        if (parts.Length != 21) //version 0 has 23 parts
            return false;
        
        var sum = 0;
        for (int i = 0; i < code.Length - 2; i++) {
            sum += code[i];
        }
        if (((sum % 256).ToString("X2")) != parts[^1]) 
            return false;

        //var enabled = CommandChangeRules.Rules.StarsToWin | CommandChangeRules.Rules.CoinsForPowerup | CommandChangeRules.Rules.Lives | 

        CommandChangeRules cmd = new CommandChangeRules {

            EnabledChanges = (CommandChangeRules.Rules) (((CommandChangeRules.Rules) int.MaxValue) - CommandChangeRules.Rules.Gamemode - CommandChangeRules.Rules.Stage - CommandChangeRules.Rules.StageChooseMode),
            Stage = rules.Stage,
            StarsToWin = int.Parse(parts[0]),
            CoinsForPowerup = int.Parse(parts[1]),
            Lives = int.Parse(parts[2]),
            TimerMinutes = int.Parse(parts[3]),
            TeamsEnabled = parts[4] == "1",

            TeamAttack = int.Parse(parts[6]),
            StarFountain = int.Parse(parts[7]),
            ChooseMode = rules.ChooseMode,

            StarFrequency = int.Parse(parts[8]),

            RouletteEnabled = parts[9] == "1",

            HazardEnabled = parts[5] == "1",
            MaxHazards = int.Parse(parts[11]),
            HazardFrequency = int.Parse(parts[12]),
            HeftyPercentage = int.Parse(parts[13]),
            HazardLifetime = int.Parse(parts[14]),

            DisableStageRestrictions = parts[16] == "1",
            DisableComplexStageRestrictions = parts[17] == "1", //can't be edited normally
            EveryItemHasTheSameChance = parts[18] == "1",

            //code += rules.CoinDeathPenalty + CODE_SEPARATOR; //coinrunners doesn't exist
            //dictionary<AssetRef<CoinItemAsset>, FP> CoinItemCustomSpawnWeights; //do not save.

            //bulb isn't implemented
            //code += rules.ModifierBulbEnabled + CODE_SEPARATOR;
            //code += rules.BulbAbilityCount + CODE_SEPARATOR;
            //code += rules.HostControl + CODE_SEPARATOR;
        };
        game.SendCommand(slot, cmd);

        var hazardIndex = 0;
        //clear both lists
        game.SendCommand(new CommandChangeHazards { 
            RemoveAll = true,
            EditingItems = false,
        });
        game.SendCommand(new CommandChangeHazards {
            RemoveAll = true,
            EditingItems = true,
        });

        //set items
        foreach (var hazardCode in parts[10].Split(CODE_List_SEPARATOR)) {
            var hazardParts = hazardCode.Split(CODE_List_SEPARATOR2);
            if (hazardParts.Length != 6)
                continue;
            game.SendCommand(slot, new CommandChangeHazards {
                //where
                Index = hazardIndex,
                EditingItems = true,
                RemoveSingle = false,
                RemoveAll = false,
                //id
                PrototypeRefId = int.Parse(hazardParts[0]),
                TeamId = 255, //team code isn't used
                //Specific Data
                ValueA = byte.Parse(hazardParts[1]),
                ValueB = byte.Parse(hazardParts[2]),
                ValueC = byte.Parse(hazardParts[3]),
                ValueD = byte.Parse(hazardParts[4]),
                UpdateUi = false,
            });
            hazardIndex++;
        }

        //set hazards
        hazardIndex = 0;
        foreach (var hazardCode in parts[15].Split(CODE_List_SEPARATOR)) {
            var hazardParts = hazardCode.Split(CODE_List_SEPARATOR2);
            if (hazardParts.Length != 6) 
                continue;
            game.SendCommand(slot, new CommandChangeHazards {
                //where
                Index = hazardIndex,
                EditingItems = false,
                RemoveSingle = false,
                RemoveAll = false,
                //id
                PrototypeRefId = int.Parse(hazardParts[0]),
                TeamId = 255, //team code isn't used
                //Specific Data
                ValueA = byte.Parse(hazardParts[1]),
                ValueB = byte.Parse(hazardParts[2]),
                ValueC = byte.Parse(hazardParts[3]),
                ValueD = byte.Parse(hazardParts[4]),
                UpdateUi = false,
            });
            hazardIndex++;
        }

        //update ui list
        game.SendCommand(slot, new CommandChangeHazards {
            EditingItems = false,
            UpdateUi = true,
        });
        game.SendCommand(slot, new CommandChangeHazards {
            EditingItems = true,
            UpdateUi = true,
        });

        // c'est fini, everyone clapped.
        // kkt claps in unison
        return true;
    }
}
