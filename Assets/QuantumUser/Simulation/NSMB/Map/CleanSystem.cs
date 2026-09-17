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
                //for (int i = 0; i < clean->TargetChances.Count; i++) {
                //}
            }
        }
        public void OnQuestionSwitchSignal(Frame f, SwitchFlag flag, QBoolean Activated, EntityRef strikerEntity) {
            var allCleans = f.Filter<Clean>();
            while (allCleans.NextUnsafe(out EntityRef entity, out Clean* clean)) {
                // Activate/Deactivate all receivers of a type
                clean->Counter--;
                f.Events.CleanCounterUpdate(clean->Counter);

                //create new switch
            }
        }
    }
}