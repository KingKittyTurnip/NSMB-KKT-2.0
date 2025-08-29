using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using Unity.Mathematics;
using UnityEngine;

public unsafe class TanoombaAnimator : QuantumEntityViewComponent {

    [SerializeField] private GameObject Models;
    [SerializeField] private GameObject Tears;
    [SerializeField] private Animator Main, Coin, Block, Star, HeavyStone, Shell;
    //---Serialized Variables
    [Space]
    [SerializeField] private GameObject PoofParticle;
    [SerializeField] private GameObject FleeParticle;

    private MaterialPropertyBlock materialBlock;
    [SerializeField] private List<Renderer> eyeRenders = new();
    private float blinkTimer = 0;

    private bool modelRotateInstantly;
    private quaternion modelRotationTarget;


    private static readonly int ParamEyeState = Shader.PropertyToID("_EyeState");

    public void Start() {
        //QuantumEvent.Subscribe<EventClockCollect>(this, ClockCollect);
        eyeRenders.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        eyeRenders.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
        /*foreach (Renderer r in eyeRenders) {
            // Get a copy of all materials.
            // This looks jank as hell, but it works, because
            // assigning to Renderer.material creates a COPY.
            List<Material> matList = new();
            r.GetSharedMaterials(matList);
            r.SetMaterials(matList);
            matList.Clear();
            r.GetMaterials(matList);
            eyeRenders[r] = matList;
        }*/
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        //if (materialBlock != null) {
        //    return;
        //}

        //Vars
        var tanoomba = f.Unsafe.GetPointer<Tanoomba>(EntityRef);
        var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        //Model Showings
        float remainingDamageInvincibility = tanoomba->DamageInvincibilityFrames / 60f;
        Models.SetActive(tanoomba->GetupFrames > 0 || enemy->IsDead || !(remainingDamageInvincibility > 0 && (f.Number * f.DeltaTime.AsFloat) * (remainingDamageInvincibility <= 0.75f ? 5 : 2) % 0.2f < 0.1f));
        Main.gameObject.SetActive(tanoomba->State <= TanoombaState.KnockedBack);
        Tears.gameObject.SetActive(Main.GetCurrentAnimatorStateInfo(0).IsName("LMAO"));

        //rotation
        modelRotationTarget = Quaternion.Euler(0, enemy->FacingRight ? -60 : 60, 0);
        modelRotateInstantly = (tanoomba->State == TanoombaState.KnockedBack || tanoomba->State == TanoombaState.Searching);
        InterpolateFacingDirection(tanoomba);

        //Animator
        Main.SetFloat("VelocityX", Mathf.Abs((float) physicsObject->Velocity.X));
        Main.SetBool("Laughing", tanoomba->Laughing);
        Main.SetBool("Knockbacked", tanoomba->State == TanoombaState.KnockedBack);
        Main.SetBool("Grounded", physicsObject->IsTouchingGround);

        //Eyes
        int Eyestate = 0;
        blinkTimer += Time.deltaTime;
        if (Main.GetCurrentAnimatorStateInfo(0).IsName("LMAO") || Main.GetCurrentAnimatorStateInfo(0).IsName("Flee")) {
            Eyestate = 5;
        } else if (Main.GetCurrentAnimatorStateInfo(0).IsName("Laugh")) {
            Eyestate = 4;
        } else if (Main.GetCurrentAnimatorStateInfo(0).IsName("Attack")) {
            Eyestate = 1;
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
        foreach (Renderer r in eyeRenders) {
            r.SetPropertyBlock(materialBlock);
        }
    }

    private void InterpolateFacingDirection(Tanoomba* tanoomba) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly) {
            Models.transform.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Models.transform.rotation = Quaternion.RotateTowards(Models.transform.rotation, modelRotationTarget, maxRotation);
        }
    }

    private unsafe void ClockCollect(EventClockCollect e) {
        if (e.Entity != EntityRef) {
            return;
        }
    }
}