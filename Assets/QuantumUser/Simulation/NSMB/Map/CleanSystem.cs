using Photon.Deterministic;
using UnityEngine.UIElements;

namespace Quantum {
    public unsafe class CleanSystem : SystemMainThreadEntityFilter<Clean, CleanSystem.Filter>, ISignalOnQuestionSwitchSignal {
        public struct Filter {
            public EntityRef Entity;
            public Clean* Clean;
            public Transform2D* Transform;
            public QuestionSwitchReceiver* QuestionSwitchReceiver;
        }
        public override void OnInit(Frame f) {
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var clean = filter.Clean;
            var entity = filter.Entity;

            if (QuantumUtils.Decrement(ref clean->Countdown)) {
                var marios = f.ResolveList(clean->Marios);
                if (!clean->HasStarted) {
                    //build the list of marios
                    var allMarios = f.Filter<MarioPlayer>();
                    while (allMarios.NextUnsafe(out EntityRef marioEntity, out MarioPlayer* mar)) {
                        for (int m = 0; m < Constants.MaxPlayers; m++) {
                            if (!mar->IsValid(f)) {
                                break;
                            }

                            if (mar->PlayerRef == f.Global->PlayerInfo[m].PlayerRef) {
                                marios.Add(marioEntity);
                                break;
                            }
                        }
                    }

                    //destroy 2 switches
                    int rngA = f.RNG->Next(0, clean->Counter-1);
                    int rngB = f.RNG->Next(0, clean->Counter-2);
                    int numb = 0;
                    var allSwitches = f.Filter<QuestionSwitch>();
                    while (allSwitches.NextUnsafe(out EntityRef switchEntity, out QuestionSwitch* Qswitch)) {
                        if (numb == rngA || numb == rngB) {
                            numb--;
                            f.Destroy(switchEntity);
                        }
                        numb++;
                    }
                    clean->HasStarted = true;
                    clean->Countdown = f.RNG->Next(10, 15)*60;
                    return;
                }

                byte MaxChance = 200;

                //increment chances
                for (int i = 0; i < marios.Count; i++) {
                    if (f.Unsafe.TryGetPointer<MarioPlayer>(marios[i], out var mar) && mar->IsValid(f)) {
                        if (mar->IsDead) {
                            clean->TargetChances[i] = 0;
                        } else {
                            clean->TargetChances[i] = (byte) FPMath.Min(clean->TargetChances[i] + (clean->NearLightBlock(f, FPVector2.Zero) ? 2 : 1), MaxChance);
                        }
                    }
                }

                //decide next target(s)

            }

            //visible & invis behavior

            //control hands
        }
        public void OnQuestionSwitchSignal(Frame f, SwitchFlag flag, QBoolean Activated, EntityRef strikerEntity) {
            var allCleans = f.Filter<Clean>();
            while (allCleans.NextUnsafe(out EntityRef entity, out Clean* clean)) {
                clean->Counter--;
                f.Events.CleanCounterUpdate(clean->Counter);

                var marios = f.ResolveList(clean->Marios);
                for (byte i = 0; i < marios.Count; i++) {
                    if (marios[i] == strikerEntity) {
                        //hey don't do that.
                        clean->AddAmount(i, 20);
                        break;
                    }
                }
            }
        }
    }
}