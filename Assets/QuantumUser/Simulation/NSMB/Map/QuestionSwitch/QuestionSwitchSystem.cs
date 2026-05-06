using Photon.Deterministic;

namespace Quantum {
    public unsafe class QuestionSwitchSystem : SystemSignalsOnly, ISignalOnQuestionSwitchSignal {
        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<MarioPlayer, QuestionSwitch>(f, OnQuestionSwitchMarioPlayerInteraction);
        }

        public static bool OnQuestionSwitchMarioPlayerInteraction(Frame f, EntityRef marioEntity, EntityRef switchEntity, PhysicsContact contact) {
            var transform = f.Unsafe.GetPointer<Transform2D>(switchEntity);

            FPVector2 up = FPVector2.Rotate(FPVector2.Up, transform->Rotation);
            FP dot = FPVector2.Dot(up, contact.Normal);

            if (dot > Constants.PhysicsGroundMaxAngleCos) {
                var QSwitch = f.Unsafe.GetPointer<QuestionSwitch>(switchEntity);
                if (!QSwitch->Pressed) {
                    f.Signals.OnQuestionSwitchSignal(QSwitch->SignalSent, true);
                }
            }
            return false;
        }
        public void OnQuestionSwitchSignal(Frame f, SwitchFlag flag, QBoolean Activated) {
            var allSwitches = f.Filter<QuestionSwitch, PhysicsCollider2D>();
            while (allSwitches.NextUnsafe(out EntityRef entity, out QuestionSwitch* QSwitch, out PhysicsCollider2D* collider)) {
                // Activate/Deactivate all switches of a type
                if (QSwitch->SignalSent == flag) {
                    QSwitch->Pressed = Activated;
                    collider->Shape.Box.Extents = Activated ? QSwitch->UnpressedSize : QSwitch->PressedSize;
                    collider->Shape.Centroid.Y = collider->Shape.Box.Extents.Y;
                    f.Events.QuestionSwitchAnimation(entity, Activated);
                }
            }
        }
    }
}