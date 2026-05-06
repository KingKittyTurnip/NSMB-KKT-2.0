using Photon.Deterministic;
using System;

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
                    if (transform->Rotation == 0) {
                        var marPhys = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
                        var marTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
                        if (marPhys->Velocity.Y < 0)
                            marPhys->Velocity.Y = 0;
                        marPhys->IsTouchingGround = true;
                        marTransform->Position.Y = transform->Position.Y + Constants._0_66;
                    } else if (FPMath.Abs(transform->Rotation) == FP.Pi/2) {
                        f.Unsafe.GetPointer<PhysicsObject>(marioEntity)->Velocity.X = 0;
                    }

                    f.Events.QuestionSwitchAnimation(true, switchEntity, true);
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
                    f.Events.QuestionSwitchAnimation(false, entity, Activated);
                }
            }
        }
    }
}