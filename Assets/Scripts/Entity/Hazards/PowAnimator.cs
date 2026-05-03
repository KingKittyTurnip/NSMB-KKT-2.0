using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using NSMB.Utilities;

public unsafe class PowAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController RedPowController;
    [SerializeField] private AudioSource sfx;
    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnPowExplode);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override void OnActivate(Frame f) {
        if (RedPowController != null && f.Unsafe.TryGetPointer<ThrowingObject>(EntityRef, out var pow) && pow->Varient == 1) {
            animator.runtimeAnimatorController = RedPowController;
        }
        OnUpdateView();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;
    }
    private unsafe void OnPowExplode(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.Play();
        animator.SetTrigger("Explode");
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}