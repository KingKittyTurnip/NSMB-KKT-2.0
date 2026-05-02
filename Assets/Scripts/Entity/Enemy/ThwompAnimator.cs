using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Cameras;
using NSMB.Utilities;
using UnityEngine.UIElements;

public unsafe class ThwompAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject Model, BlurParent;
    [SerializeField] private Transform[] BlurEffects;
    [SerializeField] private AudioSource sfx;

    [SerializeField] private GameObject specialKillParticle;
    [SerializeField] private GameObject LandParticle;

    public Renderer renderer = new();
    private MaterialPropertyBlock materialBlock;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventThwompLanded>(this, OnThwompLanded, FilterOutReplayFastForward);
        if (materialBlock != null) {
            return;
        }

        materialBlock = new();
        renderer.SetPropertyBlock(materialBlock);
    }
    public override void OnActivate(Frame f) {
        if (f.Unsafe.GetPointer<Thwomp>(EntityRef)->Big) {
            Model.transform.localScale *= 2;
            BlurParent.transform.localScale *= 2;
        }
        materialBlock = new();
        renderer.SetPropertyBlock(materialBlock);
        OnUpdateView();
    }

    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        //if (f.Global->GameState >= GameState.Ended) {
        //    return;
        //}

        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        if (enemy->IsDead)
            return;

        var thwomp = f.Unsafe.GetPointer<Thwomp>(EntityRef);
        var physobj = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        int EyeState = 0;
        if (thwomp->State == ThwompState.Recover) {
        } else if (thwomp->State == ThwompState.Fall || thwomp->State == ThwompState.Landed) {
            EyeState = 2;
        } else if (thwomp->PlayerNear) {
            EyeState = 1;
        }

        materialBlock.SetFloat("_EyeState", EyeState);
        renderer.SetPropertyBlock(materialBlock);

        if (thwomp->State == ThwompState.Fall && physobj->Velocity.Y <= 0) {
            for (int i = 0; i < BlurEffects.Length; i++) {
                BlurEffects[i].localPosition = new Vector3((((float) physobj->Velocity.X) * (1 + i) * -0.011f), (((float) physobj->Velocity.Y) * (1 + i) * -0.011f), 0.16f);
                if (!BlurEffects[i].gameObject.activeSelf) {
                    BlurEffects[i].gameObject.SetActive(true);
                    return;
                }
            }
        } else {
            for (int i = 0; i < BlurEffects.Length; i++) {
                if (BlurEffects[i].gameObject.activeSelf) {
                    BlurEffects[i].gameObject.SetActive(false);
                    return;
                }
            }
        }
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

    private void OnThwompLanded(EventThwompLanded e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.Play();
        CameraAnimator.TriggerScreenshake(e.Big ? 0.2f : 0.05f);
        Instantiate(LandParticle, BlurParent.transform.position, Quaternion.identity);
    }
}
