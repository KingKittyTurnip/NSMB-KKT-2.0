using NSMB.Utilities.Extensions;
using Quantum;
using Quantum.Profiling;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using System.Collections.Generic;
using UnityEngine.InputSystem.XR;
using NSMB.Sound;

public unsafe class MontyTankAnimator : QuantumEntityViewComponent {

    [SerializeField] private Animator[] CannonSegments;
    [SerializeField] private ParticleSystem[] CannonJustFiredParticles;
    [SerializeField] private AudioSource[] CannonSfx;
    [Space]
    [SerializeField] private Animator MontyAnimator;
    [SerializeField] private GameObject Model;
    [SerializeField] private Animator TreadsAnimator;
    [SerializeField] private LoopingSoundPlayer TreadsSound;
    [SerializeField] private ParticleSystem TreadsParticle;
    [Space]
    [SerializeField] private GameObject jumpDust;
    [SerializeField] private GameObject groundpoundDust, BossKillParticle;
    [Space]
    [SerializeField] private AudioClip Land;
    [SerializeField] private AudioClip Cyote, CreateSegment, DestroySegment, Throw, CannonTurn, Warn, Shoot;

    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();

    public void Start() {
        QuantumEvent.Subscribe<EventTankCannonPrepareFire>(this, OnCannonPrepareFire);
        QuantumEvent.Subscribe<EventTankCannonFire>(this, OnCannonFire);
        QuantumEvent.Subscribe<EventTankAddRemoveSegment>(this, OnAddRemove);
        QuantumEvent.Subscribe<EventTankMontyAttack>(this, OnMontyAttack);
        QuantumEvent.Subscribe<EventTankStartTurn>(this, OnCannonTurn);
        QuantumEvent.Subscribe<EventBowserLanded>(this, OnLanded);
        QuantumEvent.Subscribe<EventBowserFall>(this, OnFall);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
        QuantumEvent.Subscribe<EventPlayBossHitSound>(this, OnPlayBossHitSound);

        renderers.AddRange(MontyAnimator.GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(MontyAnimator.GetComponentsInChildren<SkinnedMeshRenderer>(true));

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
        var tank = f.Unsafe.GetPointer<Tank>(EntityRef);
        if (!f.Exists(tank->MoleEntity)) {
            return;
        }
        var Boss = f.Unsafe.GetPointer<Boss>(tank->MoleEntity);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);

        Model.gameObject.SetActive(Boss->BossAnimator_ShowModel(f) || MontyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit"));
        MontyAnimator.gameObject.transform.localPosition = new Vector3(0, tank->LastMontyPos.AsFloat, 0);

        materialBlock.SetFloat("Redness", Boss->BossAnimator_GetRedness());
        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }

        //rotation, monty
        modelRotationTarget = Quaternion.Euler(0, (Boss->FacingRight ? -75 : 75), 0);
        InterpolateFacingDirection(MontyAnimator.gameObject, 500f);
        //rotation, cannons
        for (int i = 0; i < tank->FacingDirection.Length; i++) {
            modelRotationTarget = Quaternion.Euler(0, tank->FacingDirection[i].AsFloat * -1f, 0);
            InterpolateFacingDirection(CannonSegments[i].gameObject, 1000f);
        }

        //Animator
        float absVel = Mathf.Abs(physicsObject->Velocity.X.AsFloat);
        bool IsGrounded = tank->State != MontyState.Jumping;
        bool activetreads = absVel > 0.5f && IsGrounded;
        TreadsAnimator.SetFloat("VelX", physicsObject->Velocity.X.AsFloat);
        TreadsAnimator.SetBool("Air", !IsGrounded);
        if (TreadsSound.IsPlaying != activetreads) {
            if (activetreads) {
                TreadsSound.Play();
            } else {
                TreadsSound.Stop();
            }
        }
        var bru = TreadsParticle.emission; bru.enabled = activetreads;
    }

    private void InterpolateFacingDirection(GameObject Ratater, float speedMultiplier) {
        using var profilerScope = HostProfiler.Start("MontyTankAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Ratater.transform.localRotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = speedMultiplier * Time.deltaTime;
            Ratater.transform.localRotation = Quaternion.RotateTowards(Ratater.transform.localRotation, modelRotationTarget, maxRotation);
        }
    }


    private unsafe void OnCannonPrepareFire(EventTankCannonPrepareFire e) {
        if (e.Entity != EntityRef) {
            return;
        }

        CannonSegments[e.SegmentId].SetTrigger("PreFire");
        CannonSfx[e.SegmentId].PlayOneShot(Shoot);
    }

    private unsafe void OnCannonFire(EventTankCannonFire e) {
        if (e.Entity != EntityRef) {
            return;
        }

        CannonJustFiredParticles[e.SegmentId].Play();
        CannonSegments[e.SegmentId].SetTrigger("Fire");
    }

    private unsafe void OnAddRemove(EventTankAddRemoveSegment e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (e.SegmentId != 255)
            CannonSegments[e.SegmentId].SetTrigger(e.IsAdding ? "Create" : "Decreate");
        MontyAnimator.SetTrigger("Move");
        CannonSfx[e.SegmentId].PlayOneShot(e.IsAdding ? CreateSegment : DestroySegment);
    }

    private void OnCannonTurn(EventTankStartTurn e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.IsStarting) {
            CannonSfx[e.SegmentId].PlayOneShot(CannonTurn);
        } else {
            CannonSfx[e.SegmentId].Stop();
        }
    }

    private unsafe void OnMontyAttack(EventTankMontyAttack e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.Thrown) {
            CannonSfx[0].PlayOneShot(Throw);
        } else {
            MontyAnimator.SetTrigger("Throw");
        }
    }

    private unsafe void OnLanded(EventBowserLanded e) {
        if (e.Entity != EntityRef) {
            return;
        }

        CannonSfx[0].PlayOneShot(Land);
        Instantiate(groundpoundDust, transform.position, Quaternion.identity);
        if (!MontyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Throw") && !MontyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hit"))
            MontyAnimator.SetTrigger("Landed");
        if (e.Roar) {
            CannonSfx[0].PlayOneShot(Warn);
            TreadsAnimator.SetTrigger("Entry");
        }
    }

    private unsafe void OnKnockbacked(EventBowserKnockbacked e) {
        if (e.Entity != EntityRef) {
            return;
        }
        //Animator.SetTrigger("HitHard");
        //Instantiate(ParticleEffect., transform.position, Quaternion.identity);
        MontyAnimator.SetTrigger("Landed");
    }

    private unsafe void OnFall(EventBowserFall e) {
        if (e.Entity != EntityRef) {
            return;
        }

        foreach (var i in CannonSfx) {
            i.Stop();
        }
        CannonSfx[0].PlayOneShot(Cyote);
    }

    private unsafe void OnDeath(EventBossDeathAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        foreach (var i in CannonSfx) {
            i.Stop();
        }
        CannonSfx[0].PlayOneShot(Cyote);
        MontyAnimator.SetTrigger("Dead");
        Instantiate(BossKillParticle, transform.position, Quaternion.identity);
    }

    private void OnPlayBossHitSound(EventPlayBossHitSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        CannonSfx[0].PlayOneShot(SoundEffect.World_Boss_Hit);
    }
}