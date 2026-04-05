using NSMB.UI.Game;
using NSMB;
using NSMB.Utilities.Extensions;
using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using UnityEngine.TextCore.Text;
using System.Collections.Generic;

public unsafe class WhompKingAnimator : QuantumEntityViewComponent {

    [Space]
    [SerializeField] private GameObject Ratater, Model;
    [SerializeField] private Animator Animator;
    [SerializeField] private AudioSource sfx;
    [Space]
    [SerializeField] private GameObject jumpDust;
    [SerializeField] private GameObject groundpoundDust, BossKillParticle;
    [Space]
    [SerializeField] private AudioClip Land;
    [SerializeField] private AudioClip Jump, Foot, Slamhit, Cyote;

    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();

    public void Start() {
        QuantumEvent.Subscribe<EventWhompKingJump>(this, OnJump);
        QuantumEvent.Subscribe<EventWhompKingLand>(this, OnLanded);
        QuantumEvent.Subscribe<EventWhompKingKnockbacked>(this, OnKnockbacked);
        QuantumEvent.Subscribe<EventWhompKingpitfall>(this, OnFall);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
        QuantumEvent.Subscribe<EventPlayBossHitSound>(this, OnPlayBossHitSound);
        if (materialBlock != null) {
            return;
        }

        materialBlock = new();

        renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

        renderers[0].SetPropertyBlock(materialBlock);
    }
    public override void OnActivate(Frame f) {
        OnUpdateView();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }


        //Vars
        var whompking = f.Unsafe.GetPointer<WhompKing>(EntityRef);
        var Boss = f.Unsafe.GetPointer<Boss>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Model.SetActive(Boss->BossAnimator_ShowModel(f) || Animator.GetCurrentAnimatorStateInfo(0).IsName("Knockbacked"));

        materialBlock.SetFloat("Redness", Boss->BossAnimator_GetRedness());
        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }

        //rotation=
        if (whompking->State == WhompKingState.SlamAttacking && whompking->ReusableTimer <= 120) {
            modelRotationTarget = Quaternion.Euler(Mathf.Min(whompking->ReusableTimer * 3, 90), Boss->FacingRight ? 125 : -125, 0);
            Ratater.transform.localPosition = new Vector3((Boss->FacingRight ? -1 : 1) * Mathf.Min(whompking->ReusableTimer * 0.0266f, 0.8f), Mathf.Min(whompking->ReusableTimer * 0.005f, 0.15f), -3);
        } else {
            Ratater.transform.localPosition = Vector3.zero;
            modelRotationTarget = Quaternion.Euler(0, Boss->FacingRight ? 125 : -125, 0);
        }
        InterpolateFacingDirection();

        //Animator
        Animator.SetFloat("VelX", Mathf.Abs(physicsObject->Velocity.X.AsFloat));
        Animator.SetBool("Slamming", (!physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround) || whompking->State == WhompKingState.SlamAttacking);
        Animator.SetBool("Hit", whompking->State == WhompKingState.SlamHit);
    }

    private void InterpolateFacingDirection() {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Ratater.transform.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 500f * Time.deltaTime;
            Ratater.transform.rotation = Quaternion.RotateTowards(Ratater.transform.rotation, modelRotationTarget, maxRotation);
        }
    }

    [Preserve]
    public void WhompKingStep() {
        sfx.PlayOneShot(Foot);
    }

    private unsafe void OnJump(EventWhompKingJump e) {
        if (e.Entity != EntityRef) {
            return;
        }
        //sfx.PlayOneShot(Flap);
        Animator.SetTrigger("Jump");
        sfx.PlayOneShot(Jump);
    }
    private unsafe void OnLanded(EventWhompKingLand e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(e.Slam ? Slamhit : Land);
        Instantiate(e.Slam ? groundpoundDust : jumpDust, transform.position, Quaternion.identity);
    }
    private unsafe void OnKnockbacked(EventWhompKingKnockbacked e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("HitHard");
        //Instantiate(ParticleEffect., transform.position, Quaternion.identity);
    }
    private unsafe void OnFall(EventWhompKingpitfall e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.Stop();
        sfx.PlayOneShot(Cyote);
        //Instantiate(ParticleEffect., transform.position, Quaternion.identity);
    }

    private unsafe void OnDeath(EventBossDeathAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.Stop();
        sfx.PlayOneShot(Cyote);
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