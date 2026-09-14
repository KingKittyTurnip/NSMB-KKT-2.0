using Photon.Deterministic;
using UnityEngine.SocialPlatforms;

namespace Quantum {
    public class CommandChangeHazards : DeterministicCommand, ILobbyCommand {
        
        //general
        public int Index;
        public bool EditingItems;
        public bool RemoveSingle, RemoveAll;
        //hazard
        public int PrototypeRefId;
        public byte TeamId;
        //extras list
        public byte ValueA;
        public byte ValueB;
        public byte ValueC;
        public byte ValueD;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Index);
            stream.Serialize(ref EditingItems);
            stream.Serialize(ref RemoveSingle);
            stream.Serialize(ref RemoveAll);

            stream.Serialize(ref PrototypeRefId);
            stream.Serialize(ref TeamId);

            stream.Serialize(ref ValueA);
            stream.Serialize(ref ValueB);
            stream.Serialize(ref ValueC);
            stream.Serialize(ref ValueD);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || !playerData->IsRoomHost(f)) {
                // Only the host can change rules.
                return;
            }

            var rules = f.ResolveList(EditingItems ? f.Global->Rules.Items : f.Global->Rules.Hazards);

            if (RemoveAll) {
                rules.Clear();
            } else if (RemoveSingle) {
                rules.RemoveAt(Index);
            } else {
                if (Index >= rules.Count) {
                    //add rule
                    if (rules.Count >= 64)
                        return;
                    rules.Add(new HazardList() {
                        PrototypeRef = PrototypeRefId,
                        //Sub Data
                        Team = TeamId,
                        //Specific Data
                        ExtraSlotA = ValueA,
                        ExtraSlotB = ValueB,
                        ExtraSlotC = ValueC,
                        ExtraSlotD = ValueD,
                    });
                } else {
                    //edit rule
                    rules[Index] = new HazardList() {
                        PrototypeRef = PrototypeRefId,
                        //Sub Data
                        Team = TeamId,
                        //Specific Data
                        ExtraSlotA = ValueA,
                        ExtraSlotB = ValueB,
                        ExtraSlotC = ValueC,
                        ExtraSlotD = ValueD,
                    };
                }
            }

            if (EditingItems) {
                f.Global->Rules.Items = rules;
            } else {
                f.Global->Rules.Hazards = rules;
            }
            //f.Events.TriggersChanged(f); // h, used for dem buttons ain't it?

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }
        }
    }
}