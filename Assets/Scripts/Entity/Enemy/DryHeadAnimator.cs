using NSMB.Sound;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Scripting;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class DryHeadAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject Ratater;
        [SerializeField] private AudioSource sfx;

        [SerializeField] private LoopingSoundPlayer RevivingSound;

        private Quaternion modelRotationTarget;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventDryRetrive>(this, OnRetrive, FilterOutReplayFastForward);
        }
        public override void OnActivate(Frame f) {
            OnUpdateView();
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            var head = f.Unsafe.GetPointer<DryHead>(EntityRef);
            var phys = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

            if (f.Global->GameState >= GameState.Ended) {
                animator.speed = 0;
            } else {
                animator.speed = 1;

                Ratater.transform.rotation = Quaternion.Euler(0, head->FacingRight ? -140 : -40, Ratater.transform.rotation.eulerAngles.z + (phys->Velocity.X.AsFloat * Time.deltaTime * -150));
            }
        }

        private void OnRetrive(EventDryRetrive e) {
            var allCoins = e.f.Filter<DryBones>();
            while (allCoins.NextUnsafe(out EntityRef entity, out DryBones* dry)) {
                //our head
                if (e.Entity == entity && dry->DryHead == EntityRef) {
                    animator.SetTrigger("Return");
                    RevivingSound.Play();
                }
            }
        }
    }
}