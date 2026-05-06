using Photon.Deterministic;
using UnityEngine.UIElements;

namespace Quantum {
    public unsafe class QuestionSwitchReceiverSystem : SystemMainThreadEntityFilter<QuestionSwitchReceiver, QuestionSwitchReceiverSystem.Filter>, ISignalOnQuestionSwitchSignal {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public QuestionSwitchReceiver* QuestionSwitchReceiver;
        }
        public override void OnInit(Frame f) {
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var transform = filter.Transform;
            var receiver = filter.QuestionSwitchReceiver;
            var entity = filter.Entity;

            if (receiver->TicksTimer && receiver->Timer > 0 && QuantumUtils.Decrement(f, ref receiver->Timer)) {
                if (f.Unsafe.TryGetPointer<GenericMover>(entity, out var genericMover)) {
                    //We Are generic mover, wait til we finish moving before the switches are hitable again
                    var asset = f.FindAsset(genericMover->MoverAsset);
                    FP totalDuration = 0;
                    for (int i = 0; i < asset.ObjectPath.Length; i++) {
                        totalDuration += asset.ObjectPath[i].TravelDuration;
                    }
                    FP currentTime = ((f.Number - f.Global->StartFrame) * f.DeltaTime) + genericMover->StartOffset;

                    if (currentTime < totalDuration) {
                        receiver->Timer = FP._0_10;
                        return;
                    }
                }

                //End Question Switch
                f.Signals.OnQuestionSwitchSignal(receiver->ListenFor, false);
            }
        }
        public void OnQuestionSwitchSignal(Frame f, SwitchFlag flag, QBoolean Activated) {
            var allReceivers = f.Filter<QuestionSwitchReceiver>();
            bool SelectedTickerTimer = false;
            while (allReceivers.NextUnsafe(out EntityRef entity, out QuestionSwitchReceiver* receiver)) {
                // Activate/Deactivate all receivers of a type
                if (receiver->ListenFor == flag) {
                    if (!SelectedTickerTimer) {
                        if (Activated) {
                            receiver->TicksTimer = true;
                            receiver->Timer = 8; //Switches last 8 Seconds

                            //Start
                            if (f.Unsafe.TryGetPointer<GenericMover>(entity, out var mover)) {
                                //restart mover? this could be deadly for replays idk
                                mover->StartOffset = -((f.Number - f.Global->StartFrame) * f.DeltaTime);
                            }
                        }
                    }
                }
            }
        }
    }
}