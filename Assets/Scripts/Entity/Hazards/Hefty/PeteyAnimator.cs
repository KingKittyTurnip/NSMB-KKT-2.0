using NSMB.Utilities.Extensions;
using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

public unsafe class PeteyAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Ratater, Model;
    [SerializeField] private Animator Animator;
    [SerializeField] private AudioSource sfx;
    [Space]
    [SerializeField] private GameObject jumpDust;
    [SerializeField] private GameObject groundpoundDust, BossKillParticle;
    [Space]
    [SerializeField] private AudioClip Attack;
    [SerializeField] private AudioClip HeadBonk, Damage, Dizzy, Flap, Slip, Snore, Sleeping;
    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();

    public void Start() {
        QuantumEvent.Subscribe<EventPeteyWakeup>(this, OnWakup);
        QuantumEvent.Subscribe<EventPeteyGetUp>(this, OnGetup);
        QuantumEvent.Subscribe<EventPeteyJump>(this, OnJump);
        QuantumEvent.Subscribe<EventPeteyDive>(this, OnDive);
        QuantumEvent.Subscribe<EventPeteyLanded>(this, OnLanded);
        QuantumEvent.Subscribe<EventPeteyStomped>(this, OnStomped);
        QuantumEvent.Subscribe<EventPeteyAttack>(this, OnAttack);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
        QuantumEvent.Subscribe<EventPlayBossHitSound>(this, OnPlayBossHitSound);

        renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

        renderers[0].SetPropertyBlock(materialBlock);
    }
    public override void OnActivate(Frame f) {
        materialBlock = new();
        OnUpdateView();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        //Vars
        var petey = f.Unsafe.GetPointer<Petey>(EntityRef);
        var Boss = f.Unsafe.GetPointer<Boss>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Model.SetActive(Boss->BossAnimator_ShowModel(f));

        materialBlock.SetFloat("Redness", Boss->BossAnimator_GetRedness());
        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }

        //rotation
        if (petey->State != PeteyState.Idling) {
            modelRotationTarget = Quaternion.Euler(0, Boss->FacingRight ? 130 : -130, 0);
            InterpolateFacingDirection();
        }

        //Animator
        Animator.speed = freezable->IsFrozen(f) ? 0 : (petey->Flying && petey->State == PeteyState.Flying && transform->Position.Y - (petey->PreviousLandLevel - 1) < 0 ? 1.5f : 1);
        Animator.SetBool("Flying", petey->Flying && petey->State == PeteyState.Flying);
    }

    private void InterpolateFacingDirection() {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Ratater.transform.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Ratater.transform.rotation = Quaternion.RotateTowards(Ratater.transform.rotation, modelRotationTarget, maxRotation);
        }
    }

    [Preserve]
    public void PeteyFlap() {
        sfx.PlayOneShot(Flap);
    }
    [Preserve]
    public void PeteyHeadBonk() {
        sfx.PlayOneShot(HeadBonk);
        //play a particle effect?
    }
    [Preserve]
    public void PeteySleeping() {
        sfx.PlayOneShot(Sleeping);
    }
    [Preserve]
    public void PeteySnoring() {
        sfx.PlayOneShot(Snore);
    }
    [Preserve]
    public void PeteyDizzy() {
        sfx.PlayOneShot(Dizzy);
    }


    private unsafe void OnWakup(EventPeteyWakeup e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger(e.Interupted ? "Hitup" : "Wakeup");
    }
    private unsafe void OnGetup(EventPeteyGetUp e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Getup");
    }
    private unsafe void OnJump(EventPeteyJump e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(Flap);
        Instantiate(jumpDust, transform.position, Quaternion.identity);
        Animator.SetTrigger("Jump");
    }
    private unsafe void OnDive(EventPeteyDive e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Dive");
        sfx.PlayOneShot(Attack);
    }
    private unsafe void OnLanded(EventPeteyLanded e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (e.Weakened)
            sfx.PlayOneShot(Slip);
        Instantiate(groundpoundDust, transform.position, Quaternion.identity);
        Animator.SetTrigger(e.Weakened ? "Fell" : "Landed");
    }
    private unsafe void OnStomped(EventPeteyStomped e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(Damage);
        Animator.SetTrigger(e.IsDeath ? "Death" : "Stomped");
    }
    private unsafe void OnAttack(EventPeteyAttack e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Melee");
    }
    private unsafe void OnDeath(EventBossDeathAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Death");
        Instantiate(BossKillParticle, transform.position, Quaternion.identity);
    }
    private void OnPlayBossHitSound(EventPlayBossHitSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Boss_Hit);
    }
}