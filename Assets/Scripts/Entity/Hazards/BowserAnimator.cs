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

public unsafe class BowserAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject DryModel;
    [SerializeField] private RuntimeAnimatorController DryController;
    [SerializeField] private Avatar DryAvatar;
    [Space]
    [SerializeField] private GameObject Ratater, Model;
    [SerializeField] private Animator Animator;
    [SerializeField] private AudioSource sfx;
    [Space]
    [SerializeField] private GameObject jumpDust;
    [SerializeField] private GameObject groundpoundDust, BossKillParticle;
    [Space]
    [SerializeField] private AudioClip Land;
    [SerializeField] private AudioClip Foot, Fireball, Roar, Cyote;
    [SerializeField] private AudioClip DryFoot, Throw, DryBreak, DryUnite;

    //---Serialized Variables
    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;

    public void Start() {
        QuantumEvent.Subscribe<EventBowserJump>(this, OnJump);
        QuantumEvent.Subscribe<EventBowserLanded>(this, OnLanded);
        QuantumEvent.Subscribe<EventBowserAttack>(this, OnAttack);
        QuantumEvent.Subscribe<EventBowserShoot>(this, OnShoot);
        QuantumEvent.Subscribe<EventBowserKnockbacked>(this, OnKnockbacked);
        QuantumEvent.Subscribe<EventBowserFall>(this, OnFall);

        QuantumEvent.Subscribe<EventBossDeathAnimation>(this, OnDeath);
        QuantumEvent.Subscribe<EventPlayBossHitSound>(this, OnPlayBossHitSound);
    }
    public override void OnActivate(Frame f) {
        if (f.Unsafe.GetPointer<Bowser>(EntityRef)->IsDry) {
            Model = DryModel;
            Animator.avatar = DryAvatar;
            Animator.runtimeAnimatorController = DryController;
        }
        OnUpdateView();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }


        //Vars
        var bowser = f.Unsafe.GetPointer<Bowser>(EntityRef);
        var Boss = f.Unsafe.GetPointer<Boss>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var transform = f.Unsafe.GetPointer<Transform2D>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Model.SetActive(f.Global->GameState >= GameState.Playing && (!(Boss->iframes > 0 && (f.Number * f.DeltaTime.AsFloat) * (Boss->iframes <= 0.75f ? 5 : 2) % 0.2f < 0.1f) || Animator.GetCurrentAnimatorStateInfo(0).IsName("Knockbacked")));

        //rotation
        if (bowser->State != BowserState.Roaring) {
            modelRotationTarget = Quaternion.Euler(0, Boss->FacingRight ? 120 : -120, 0);
            InterpolateFacingDirection();
        }

        //Animator
        Animator.SetFloat("VelX", Mathf.Abs(physicsObject->Velocity.X.AsFloat));
        Animator.SetBool("Falling", !physicsObject->IsTouchingGround && !physicsObject->WasTouchingGround && physicsObject->Velocity.Y < 0);
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

    [Preserve]
    public void BowserStep() {
        sfx.PlayOneShot(Foot);
    }

    [Preserve]
    public void DryStep() {
        sfx.PlayOneShot(Foot);
    }
    [Preserve]
    public void LandRoar() {
        sfx.PlayOneShot(Roar);
    }
    [Preserve]
    public void DryLand() {
        sfx.PlayOneShot(DryBreak);
    }
    [Preserve]
    public void Dryassemble() {
        sfx.PlayOneShot(DryUnite);
    }

    private unsafe void OnJump(EventBowserJump e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (e.f.Unsafe.GetPointer<Bowser>(EntityRef)->State == BowserState.Attacking) {
            return;
        }
        //sfx.PlayOneShot(Flap);
        Instantiate(jumpDust, transform.position, Quaternion.identity);
        Animator.SetTrigger("Jump");
    }
    private unsafe void OnLanded(EventBowserLanded e) {
        if (e.Entity != EntityRef) {
            return;
        }
        var bowser = e.f.Unsafe.GetPointer<Bowser>(EntityRef);
        if (bowser->State == BowserState.Attacking && !e.Roar) {
            return;
        }

        sfx.PlayOneShot(Land);
        Instantiate(groundpoundDust, transform.position, Quaternion.identity);
        Animator.SetTrigger(e.Roar ? "Roar" : "Landed");
    }
    private unsafe void OnAttack(EventBowserAttack e) {
        if (e.Entity != EntityRef) {
            return;
        }

        Animator.SetTrigger(e.AttackType switch {
            BowserAttackType.FireBall => "Fireball",
            BowserAttackType.JumpFireBall => "JumpFire",
            BowserAttackType.MegaAttack => "MegaFire",
            BowserAttackType.BoneThrow => "Bone",
            _ => "Fireball",
        });
        if (e.AttackType == BowserAttackType.MegaAttack) {
            //sfx.Play();
        }
    }
    private unsafe void OnShoot(EventBowserShoot e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(e.IsBone ? Throw : Fireball);
    }
    private unsafe void OnKnockbacked(EventBowserKnockbacked e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Animator.SetTrigger("HitHard");
        //Instantiate(ParticleEffect., transform.position, Quaternion.identity);
    }
    private unsafe void OnFall(EventBowserFall e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.Stop();
        sfx.PlayOneShot(Cyote);
        Animator.Play("Fallen");
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