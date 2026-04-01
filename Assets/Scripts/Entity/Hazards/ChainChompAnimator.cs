using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using NSMB.Utilities;
using UnityEngine.UIElements;

public unsafe class ChainChompAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    //[SerializeField] private GameObject BoostParticles;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;

    [Space]
    [SerializeField] private AudioClip Launch;
    [SerializeField] private AudioClip Chain;
    [SerializeField] private AudioClip Bark;
    [Space]
    [SerializeField] private GameObject LaunchParticle;

    private Quaternion modelRotationTarget;


    public void Start() {
        QuantumEvent.Subscribe<EventChainChompSound>(this, OnChompSound);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var chainchomp = f.Unsafe.GetPointer<ChainChomp>(EntityRef);

        Vector3 modifiedZ = transform.position;
        //if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        //} else {
        //    modifiedZ.z = 0;
        //}
        transform.position = modifiedZ;

        animator.SetBool("Prepare", chainchomp->State == ChainChompState.Prepare);
        animator.speed = chainchomp->State == ChainChompState.Chomp ? 1.5f : 1;

        float delta = Time.deltaTime;
        if (chainchomp->State == ChainChompState.Chomp) {
        } else if (chainchomp->State == ChainChompState.Prepare || chainchomp->State == ChainChompState.Lunge) {
            float angle = Mathf.Atan2(transform.position.y - chainchomp->TargetPosition.Y.AsFloat, transform.position.x - chainchomp->TargetPosition.X.AsFloat) * Mathf.Rad2Deg;
            if (chainchomp->FacingRight) {
                modelRotationTarget = Quaternion.Euler(0, 180, 180 - angle);
            } else {
                modelRotationTarget = Quaternion.Euler(0, 0, angle);
            }

        } else {
            modelRotationTarget = Quaternion.Euler(0, chainchomp->FacingRight ? -170 : -10, 0);
        }
        InterpolateFacingDirection();
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private void InterpolateFacingDirection() {
         float maxRotation = 1000f * Time.deltaTime;
         Model.rotation = Quaternion.RotateTowards(Model.rotation, modelRotationTarget, maxRotation);
    }

    private unsafe void OnChompSound(EventChainChompSound e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(e.Lunged ? Launch : Chain);

        if (e.Lunged) {
            Instantiate(LaunchParticle, gameObject.transform.position, Quaternion.identity);
            sfx.PlayOneShot(Bark);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}