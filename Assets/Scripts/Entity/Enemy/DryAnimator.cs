using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Scripting;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class DryAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject Ratater;
        [SerializeField] private AudioSource sfx;

        [SerializeField] private AudioClip Break, Revive;

        private Quaternion modelRotationTarget;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventDryBreak>(this, OnBreak, FilterOutReplayFastForward);
            //QuantumEvent.Subscribe<EventDryRetrive>(this, OnRetrive, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventDryGetup>(this, OnRevive, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
        }
        public override void OnActivate(Frame f) {
            OnUpdateView();
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
            animator.enabled = enemy->IsActive;
            animator.gameObject.transform.localPosition = enemy->IsActive ? Vector3.zero : new Vector3(0, -999, 0);

            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);
            if (f.Global->GameState >= GameState.Ended || freezable->IsFrozen(f) || enemy->IsDead) {
                animator.speed = 0;
            } else {
                animator.speed = 1;

                modelRotationTarget = Quaternion.Euler(0, enemy->FacingRight ? 130 : -130, 0);
                InterpolateFacingDirection();
            }
        }
        private void InterpolateFacingDirection() {
            float maxRotation = 1000f * Time.deltaTime;
            Ratater.transform.rotation = Quaternion.RotateTowards(Ratater.transform.rotation, modelRotationTarget, maxRotation);
        }

        private void OnBreak(EventDryBreak e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.SetTrigger("Break");
            sfx.PlayOneShot(Break);
        }
        private void OnRevive(EventDryGetup e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.SetTrigger("Getup");
            sfx.PlayOneShot(Revive);
        }

        private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
        }

        private void OnEnemyKilled(EventEnemyKilled e) {
            if (e.Enemy != EntityRef) {
                return;
            }
            animator.Play("Walk");
            sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(0));
        }
    }
}