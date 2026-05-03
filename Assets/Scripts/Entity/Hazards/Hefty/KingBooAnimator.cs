using NSMB.UI.Game;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;

public unsafe class KingBooAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Ratater, Model;
    [SerializeField] private Animator Animator;
    [SerializeField] private AudioSource sfx, Sucking;
    [Space]
    [SerializeField] private GameObject BossKillParticle;
    [SerializeField] private GameObject SuckDust;
    [Space]
    //Laugh Sound Is Played Automatically
    [SerializeField] private AudioClip Hide;
    [SerializeField] private AudioClip FireBallA, FireballB, Hurt, Cyote;

    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;
    bool ShootA;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();
    private static readonly int ParamRedness = Shader.PropertyToID("Redness");

    float SuckTimer = 0;

    public void Start() {
        /*QuantumEvent.Subscribe<EventBowserJump>(this, OnJump);
        QuantumEvent.Subscribe<EventBowserLanded>(this, OnLanded);
        QuantumEvent.Subscribe<EventBowserAttack>(this, OnAttack);
        QuantumEvent.Subscribe<EventBowserShoot>(this, OnShoot);*/
        QuantumEvent.Subscribe<EventKingBooKnockbacked>(this, OnKnockbacked);
        QuantumEvent.Subscribe<EventKingBooBarf>(this, OnBarf);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
        QuantumEvent.Subscribe<EventPlayBossHitSound>(this, OnPlayBossHitSound);

        renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));

        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }
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
        var kingboo = f.Unsafe.GetPointer<KingBoo>(EntityRef);
        var Boss = f.Unsafe.GetPointer<Boss>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Animator.speed = freezable->IsFrozen(f) ? 0 : 1;

        bool IsSucking = kingboo->State == KingBooState.Sucking && kingboo->ReusableTimer == 0;
        SuckTimer = Mathf.Clamp01(SuckTimer + ((IsSucking ? 2 : -2) * Time.deltaTime));

        Model.SetActive(Boss->BossAnimator_ShowModel(f) || Animator.GetCurrentAnimatorStateInfo(0).IsName("Knockbacked"));

        SuckDust.SetActive(SuckTimer != 0);
        Sucking.pitch = SuckTimer;
        if (SuckTimer != 0) {
            if (!Sucking.isPlaying) {
                Sucking.Play();
            }
        } else {
            Sucking.Stop();
        }

        materialBlock.SetFloat(ParamRedness, Boss->BossAnimator_GetRedness());
        materialBlock.SetFloat("_Transparency", 1f-SuckTimer);
        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }

        //rotation
        if (kingboo->State != KingBooState.Laughing) {
            modelRotationTarget = Quaternion.Euler(Mathf.Clamp(-physicsObject->Velocity.Y.AsFloat * 5, -40, 40), Boss->FacingRight ? 145 : -145, 0);
            InterpolateFacingDirection();
        }

        //Animator
        Animator.SetFloat("VelocityMagnitude", Mathf.Abs(physicsObject->Velocity.Magnitude.AsFloat));
        Animator.SetBool("FireBall", kingboo->State == KingBooState.Barfing && kingboo->ReusableTimer < 123);
        Animator.SetBool("Knockback", (kingboo->State == KingBooState.Barfing && kingboo->ReusableTimer >= 123) || kingboo->State == KingBooState.Knockback);
        Animator.SetBool("Sucking", IsSucking);
    }

    private void InterpolateFacingDirection() {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Ratater.transform.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 1000f * Time.deltaTime;
            Ratater.transform.rotation = Quaternion.RotateTowards(Ratater.transform.rotation, modelRotationTarget, maxRotation);
        }
    }
    private unsafe void OnKnockbacked(EventKingBooKnockbacked e) {
        if (e.Entity != EntityRef) {
            return;
        }

        float SuckTimer = 0;
        sfx.PlayOneShot(Hurt);
    }
    private unsafe void OnBarf(EventKingBooBarf e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(ShootA ? FireBallA : FireballB);
        ShootA = !ShootA;
        float SuckTimer = 0;
    }
    
    private unsafe void OnDeath(EventBossDeathAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        float SuckTimer = 0;
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