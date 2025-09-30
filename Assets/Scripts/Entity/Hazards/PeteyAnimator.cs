using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

public unsafe class PeteyAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Ratater, Model;
    [SerializeField] private Animator Animator;
    [Space]
    [SerializeField] private GameObject jumpDust;
    [SerializeField] private GameObject groundpoundDust, BossKillParticle;

    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;

    public void Start() {
        QuantumEvent.Subscribe<EventPeteyWakeup>(this, OnWakup);
        QuantumEvent.Subscribe<EventPeteyGetUp>(this, OnGetup);
        QuantumEvent.Subscribe<EventPeteyJump>(this, OnJump);
        QuantumEvent.Subscribe<EventPeteyDive>(this, OnDive);
        QuantumEvent.Subscribe<EventPeteyLanded>(this, OnLanded);
        QuantumEvent.Subscribe<EventPeteyStomped>(this, OnStomped);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
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

        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Model.SetActive(f.Global->GameState >= GameState.Playing && (!(Boss->iframes > 0 && (f.Number * f.DeltaTime.AsFloat) * (Boss->iframes <= 0.75f ? 5 : 2) % 0.2f < 0.1f)));

        //rotation
        if (petey->State != PeteyState.Idling) {
            modelRotationTarget = Quaternion.Euler(0, Boss->FacingRight ? 130 : -130, 0);
            InterpolateFacingDirection();
        }

        //Animator
        Animator.speed = freezable->IsFrozen(f) ? 0 : 1;
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
        Instantiate(jumpDust, transform.position, Quaternion.identity);
        Animator.SetTrigger("Jump");
    }
    private unsafe void OnDive(EventPeteyDive e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Dive");
    }
    private unsafe void OnLanded(EventPeteyLanded e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Instantiate(groundpoundDust, transform.position, Quaternion.identity);
        Animator.SetTrigger(e.Weakened ? "Fell" : "Landed");
    }
    private unsafe void OnStomped(EventPeteyStomped e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger(e.IsDeath ? "Death" : "Stomped");
    }
    private unsafe void OnDeath(EventBossDeathAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("Death");
        Instantiate(BossKillParticle, transform.position, Quaternion.identity);
    }
}