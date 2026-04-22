using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Scripting;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class CataquackAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject Ratater;
        [SerializeField] private GameObject specialKillParticle;
        //[SerializeField] private GameObject respawnParticle;
        [SerializeField] private AudioSource sfx;

        [SerializeField] private AudioClip Footstep, Dead, Fling, Getup;

        private Quaternion modelRotationTarget;

        private MaterialPropertyBlock materialBlock;
        [SerializeField] private Renderer SkinRenderer;
        [SerializeField] private Texture[] Textures;

        public void OnValidate() {
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventCataquackFling>(this, OnFling, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventIsNowResistantHit>(this, OnResist);
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
            if (f.Global->GameState >= GameState.Ended || freezable->IsFrozen(f)) {
                animator.speed = 0;
            } else {
                animator.speed = 1;

                var duckman = f.Unsafe.GetPointer<Cataquack>(EntityRef);

                modelRotationTarget = Quaternion.Euler(0, enemy->FacingRight ? 110 : -110, 0);
                InterpolateFacingDirection();
                animator.SetBool("Dead", enemy->IsDead);

                materialBlock = new();
                materialBlock.SetTexture("Texture", Textures[(int) duckman->Varient]);
                SkinRenderer.SetPropertyBlock(materialBlock);
            }
        }
        private void InterpolateFacingDirection() {
            float maxRotation = 1000f * Time.deltaTime;
            Ratater.transform.rotation = Quaternion.RotateTowards(Ratater.transform.rotation, modelRotationTarget, maxRotation);
        }

        [Preserve]
        public void QuackStep() {
            sfx.PlayOneShot(Footstep);
        }

        private void OnFling(EventCataquackFling e) {
            if (e.Entity != EntityRef) {
                return;
            }

            animator.SetTrigger("Fling");
            sfx.PlayOneShot(Fling);

            modelRotationTarget = Quaternion.Euler(0, e.f.Unsafe.GetPointer<Enemy>(e.Entity)->FacingRight ? 110 : -110, 0);
            Ratater.transform.rotation = modelRotationTarget;
        }

        private int LastFrame = 0;
        private unsafe void OnResist(EventIsNowResistantHit e) {
            if (e.Entity != EntityRef) {
                return;
            }
            if (e.ThisFrame > LastFrame) {
                sfx.Play();
            }
            LastFrame = e.ThisFrame + 10;
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

            if (e.KillReason == EnemyKillReason.Groundpounded) {
                Instantiate(specialKillParticle, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            }
            sfx.PlayOneShot(Dead);
        }
    }
}