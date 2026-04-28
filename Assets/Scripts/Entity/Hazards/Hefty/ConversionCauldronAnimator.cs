using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using NSMB.Utilities;
using Newtonsoft.Json.Linq;

public unsafe class ConversionCauldronAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private GameObject SplashParticle;
    [SerializeField] private GameObject ExplodeParticle;
    [SerializeField] private GameObject[] bossParticles;

    [Space]
    [SerializeField] private AudioClip Jump;//eeee prob put this in the particle
    [SerializeField] private AudioClip Expand, Wiggle, Splash;


    public void Start() {
        QuantumEvent.Subscribe<EventCauldronSplash>(this, OnCauldronSplash);
        QuantumEvent.Subscribe<EventCauldronHop>(this, OnCauldronHop);
        QuantumEvent.Subscribe<EventCauldronExpand>(this, OnCauldronExpand);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override void OnActivate(Frame f) {
        if (f.Unsafe.TryGetPointer<Cauldron>(EntityRef, out var cauldron)) {
            bossParticles[cauldron->ConvertIntoBossId].SetActive(true);
        }
        OnUpdateView();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private unsafe void OnCauldronSplash(EventCauldronSplash e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(Splash);
        SplashParticle.SetActive(true);
    }
    private unsafe void OnCauldronHop(EventCauldronHop e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(Jump);
        animator.SetTrigger("Entered");
        sfx.PlayOneShot(Wiggle);
    }
    private unsafe void OnCauldronExpand(EventCauldronExpand e) {
        if (e.Entity != EntityRef) {
            return;
        }
        animator.SetTrigger("Expand");
        sfx.PlayOneShot(Expand);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}