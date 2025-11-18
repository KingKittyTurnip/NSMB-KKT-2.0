using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace NSMB.Entities.Player {
    public class BowserFireAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform Model;

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }
            var phys = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

            float direc = (((float) FPMath.Atan2(phys->Velocity.Y - 0, phys->Velocity.X - 0)) * (-180 / Mathf.PI)) + 180;
            Model.rotation = Quaternion.Euler(0, 180, direc);
        }

    }
}
