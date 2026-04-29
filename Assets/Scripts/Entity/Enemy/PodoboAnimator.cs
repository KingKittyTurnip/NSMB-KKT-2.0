using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class PodoboAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private GameObject Graphics;
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject specialKillParticle;
        [SerializeField] private AudioSource sfx;

        [SerializeField] private GameObject[] Particles;
        [SerializeField] private Sprite[] Sprites;

        [SerializeField] private AudioClip Leap;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventPodoboLeap>(this, OnPodoboLeap, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
        }
        public override void OnActivate(Frame f) {
            var id = (int)f.Unsafe.GetPointer<Podobo>(EntityRef)->Varient;
            sRenderer.sprite = Sprites[id];
            Particles[id].SetActive(true);
            OnUpdateView();
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            if (f.Global->GameState >= GameState.Ended) {
                animator.enabled = false;
                return;
            }

            var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);

            Graphics.SetActive(enemy->IsActive);
        }

        private void OnPodoboLeap(EventPodoboLeap e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(Leap);
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

            if (e.KillReason == EnemyKillReason.Special) {
                Instantiate(specialKillParticle, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            }
        }
    }
}