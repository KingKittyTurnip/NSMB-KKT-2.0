using Quantum;
using Quantum.Profiling;
using Unity.Mathematics;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities;

public unsafe class TanoombaAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Models;
    [SerializeField] private GameObject Tears;
    [SerializeField] private Animator Main;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip laugh;
    [SerializeField] private GameObject PoofParticle;
    [Header("Have Them In The Same Order As The States In Tanoomba.qtn")]
    [SerializeField] private GameObject[] TransformModels;
    [SerializeField] private Animator[] TransformModelsAnimator;

    private MaterialPropertyBlock materialBlock;
    public SkinnedMeshRenderer renderer;
    private float blinkTimer = 0;

    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;


    private static readonly int ParamEyeState = Shader.PropertyToID("_EyeType");

    public void Start() {
        QuantumEvent.Subscribe<EventTanoombaAttack>(this, OnAttack);
        QuantumEvent.Subscribe<EventTanoombaFlee>(this, OnFlee);
        QuantumEvent.Subscribe<EventTanoombaLMAO>(this, OnLMAO);
        QuantumEvent.Subscribe<EventTanoombaPoof>(this, OnTanoombaPoof);

        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true);
        QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        //Vars
        var tanoomba = f.Unsafe.GetPointer<Tanoomba>(EntityRef);
        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        //Model Showings
        Models.SetActive(enemy->IsActive);
        Main.gameObject.SetActive(tanoomba->State != TanoombaState.Searching && tanoomba->State != TanoombaState.Transformed);
        for (int i = 0; i < TransformModels.Length; i++) {
            bool activeform = tanoomba->Form == (TanoombaFormState) i;
            TransformModels[i].SetActive(activeform);
            if (activeform && TransformModelsAnimator[i] != null)
                TransformModelsAnimator[i].SetInteger("Variant", tanoomba->FormVariant);
        }
        Models.transform.localScale = new Vector3(Main.isActiveAndEnabled ? 1 : (enemy->FacingRight ? -1 : 1), 1, 1);
        Tears.gameObject.SetActive(Main.GetCurrentAnimatorStateInfo(0).IsName("LMAO"));

        //rotation
        modelRotationTarget = Quaternion.Euler(0, enemy->FacingRight ? 120 : 240, 0);
        modelRotateInstantly = (tanoomba->State == TanoombaState.KnockedBack || tanoomba->State == TanoombaState.Searching || Main.GetCurrentAnimatorStateInfo(0).IsName("Flee"));
        InterpolateFacingDirection(tanoomba);

        //Animator
        Main.SetFloat("VelocityX", Mathf.Abs((float) physicsObject->Velocity.X));
        //Main.SetBool("Laughing", tanoomba->Laughing);
        Main.SetBool("Knockbacked", tanoomba->State == TanoombaState.KnockedBack || enemy->IsDead);
        Main.SetBool("Grounded", physicsObject->IsTouchingGround);

        //Eyes
        int Eyestate = 0;
        blinkTimer += Time.deltaTime;
        if (Main.GetCurrentAnimatorStateInfo(0).IsName("LMAO") || Main.GetCurrentAnimatorStateInfo(0).IsName("Flee")) {
            Eyestate = 5;
        } else if (Main.GetCurrentAnimatorStateInfo(0).IsName("Laugh")) {
            Eyestate = 4;
        } else if (tanoomba->State == TanoombaState.Attacking) {
            Eyestate = 1;
        } else if (tanoomba->State == TanoombaState.KnockedBack) {
            Eyestate = 2;
        } else if (blinkTimer > 4) {
            if (blinkTimer > 4.3) {
                Eyestate = 3;
                if (blinkTimer > 4.6) {
                    blinkTimer -= UnityEngine.Random.Range(4f, 5f);
                }
            } else {
                Eyestate = 2;
            }
        }
        materialBlock = new();
        materialBlock.SetFloat(ParamEyeState, Eyestate);
        renderer.SetPropertyBlock(materialBlock);
    }

    private void InterpolateFacingDirection(Tanoomba* tanoomba) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Main.gameObject.transform.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Main.gameObject.transform.rotation = Quaternion.RotateTowards(Main.gameObject.transform.rotation, modelRotationTarget, maxRotation);
        }
    }

    private unsafe void OnAttack(EventTanoombaAttack e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Main.SetTrigger("Attack");

        sfx.PlayOneShot(SoundEffect.Powerup_HammerSuit_Throw);
    }
    private unsafe void OnFlee(EventTanoombaFlee e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Main.SetTrigger("Flee");
        sfx.PlayOneShot(SoundEffect.Powerup_MiniMushroom_Jump);
    }
    private unsafe void OnLMAO(EventTanoombaLMAO e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Main.SetTrigger("LMAO");
        sfx.PlayOneShot(laugh);
    }
    private unsafe void OnTanoombaPoof(EventTanoombaPoof e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Instantiate(PoofParticle, transform.position, Quaternion.identity);
    }

    private void OnPlayBumpSound(EventPlayBumpSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Block_Bump);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}