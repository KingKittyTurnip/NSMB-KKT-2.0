using Quantum;
using Quantum.Profiling;
using Unity.Mathematics;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities;
using NSMB.Utilities.Components;
using static UnityEngine.LowLevelPhysics2D.PhysicsShape;
using static LoopingMusicData;

public unsafe class TanoombaAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Models;
    [SerializeField] private GameObject Tears;
    [SerializeField] private Animator Main;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip laugh;
    [SerializeField] private GameObject PoofParticle;

    private MaterialPropertyBlock materialBlock;
    public SkinnedMeshRenderer renderer;
    private float blinkTimer = 0;

    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;


    private static readonly int ParamEyeState = Shader.PropertyToID("_EyeType");

    [Header("TransformModels")]
    [SerializeField] private GameObject Leaf;
    [SerializeField] private SpriteRenderer Tail;
    [SerializeField] private LegacyAnimateSpriteRenderer SpriteModel;
    [SerializeField] private GameObject[] TransformModels;
    [Space]
    [SerializeField] private AudioClip[] UniqueSound;
    [SerializeField] private GameObject[] UniqueParticle;
    private TanoombaTransformationAsset.TanoombaFormFlipType FlipType;

    public void Start() {
        QuantumEvent.Subscribe<EventTanoombaAttack>(this, OnAttack);
        QuantumEvent.Subscribe<EventTanoombaFlee>(this, OnFlee);
        QuantumEvent.Subscribe<EventTanoombaLMAO>(this, OnLMAO);
        QuantumEvent.Subscribe<EventTanoombaPoof>(this, OnTanoombaPoof);
        QuantumEvent.Subscribe<EventTanoombaTransform>(this, OnTanoombaTransform);
        QuantumEvent.Subscribe<EventTanoombaPlaySoundType>(this, OnTanoombaUniquSound);

        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true);
        QuantumEvent.Subscribe<EventPlayBumpSound>(this, OnPlayBumpSound, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        if (!f.Exists(EntityRef)) {
            return;
        }

        //Vars
        var tanoomba = f.Unsafe.GetPointer<Tanoomba>(EntityRef);
        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

        Main.speed = freezable->IsFrozen(f) ? 0 : 1;

        //Model Showings
        Models.SetActive(enemy->IsActive);
        Main.gameObject.SetActive(tanoomba->State != TanoombaState.Searching && tanoomba->State != TanoombaState.Transformed);
        Models.transform.localScale = FlipType switch {
            TanoombaTransformationAsset.TanoombaFormFlipType.AlwaysRight => new Vector3(-1, 1, 1),
            TanoombaTransformationAsset.TanoombaFormFlipType.FromFacing => new Vector3(enemy->FacingRight ? 1 : -1, 1, 1),
            TanoombaTransformationAsset.TanoombaFormFlipType.FromFacingReversed => new Vector3(enemy->FacingRight ? -1 : 1, 1, 1),
            TanoombaTransformationAsset.TanoombaFormFlipType.AlwaysLeft or _ => new Vector3(1, 1, 1),
        };
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
    private unsafe void OnTanoombaTransform(EventTanoombaTransform e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (e.FormId == -1) {
            FlipType = TanoombaTransformationAsset.TanoombaFormFlipType.AlwaysLeft;
            //disable all models
            Leaf.SetActive(false);
            Tail.gameObject.SetActive(false);
            SpriteModel.gameObject.SetActive(false);
            foreach (var model in TransformModels) {
                model.SetActive(false);
            }
        } else {
            var modelData = e.f.FindAsset(e.f.Unsafe.GetPointer<Tanoomba>(e.Entity)->FormData).ListOfTransforms[e.FormId].ModelData;

            FlipType = modelData.FlipType;

            //Enabled Leaf/Tail
            if (modelData.UsesLeaf) {
                Leaf.transform.localPosition = modelData.LeafLocation;
            }
            if (modelData.UsesTail) {
                Tail.transform.localPosition = modelData.TailLocation;
                Tail.flipX = FlipType != TanoombaTransformationAsset.TanoombaFormFlipType.FromFacingReversed;
            }
            Leaf.SetActive(modelData.UsesLeaf);
            Tail.gameObject.SetActive(modelData.UsesTail);

            //enable sprites
            SpriteModel.gameObject.transform.localPosition = modelData.Offset;
            SpriteModel.gameObject.SetActive(modelData.sprites.Length > 0);
            SpriteModel.frames = modelData.sprites;
            SpriteModel.fps = modelData.FPS;

            //enable model
            if (modelData.ModelId != -1) {
                TransformModels[modelData.ModelId].SetActive(true);
            }
        }
    }

    private unsafe void OnTanoombaUniquSound(EventTanoombaPlaySoundType e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (UniqueSound[e.SoundId] != null)
            sfx.PlayOneShot(UniqueSound[e.SoundId]);
        if (UniqueParticle[e.SoundId] != null)
            Instantiate(UniqueParticle[e.SoundId], transform.position, Quaternion.identity);
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