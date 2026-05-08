using Quantum;
using UnityEngine;

namespace NSMB.Entities.World {
    public unsafe class SeesawAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] protected Transform graphic;

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;
            if (!f.Exists(EntityRef)
                || f.Global->GameState < GameState.Playing) {
                return;
            }

            var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
            graphic.transform.rotation = Quaternion.Euler(0, 0, transform->Rotation.AsFloat * Mathf.Rad2Deg);
        }
    }
}
