using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using NSMB.Utilities;

public unsafe class CannonBoxAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private GameObject ChargeParticle;
    [SerializeField] private GameObject LaunchParticle;

    [Space]
    [SerializeField] private AudioClip Charge;

    private Quaternion modelRotationTarget;
    private bool wasTurnaround;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();
    private static readonly int ParamBoxType = Shader.PropertyToID("BoxType");

    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnCannonBoxBoom);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);

        renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
    }
    public override unsafe void OnActivate(Frame f) {
        materialBlock = new();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var cannonbox = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;

        ChargeParticle.SetActive((cannonbox->Varient == 0 && cannonbox->ReusableTimer <= 0) || (cannonbox->Varient > 0 && cannonbox->Varient < 3));
        float delta = Time.deltaTime;
        if (f.Exists(holdable->Holder)) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);

            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);

        } else if (!cannonbox->Thrown) {
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, cannonbox->Facing ? 110 : 250, 0), 200f * Time.deltaTime);
        } else {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }

        //Set Color
        int i = 0;
        if (f.Exists(holdable->PreviousHolder)) {
            i = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder)->CharacterAsset).Order+1;
        }
        materialBlock.SetInt(ParamBoxType, i);
        foreach (Renderer r in renderers) {
            r.SetPropertyBlock(materialBlock);
        }
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private void SetFacingDirection(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.SetFacingDirection");

        float delta = Time.deltaTime;

        modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? 110 : 250, 0);

        wasTurnaround = mario->IsTurnaround;
    }

    private void InterpolateFacingDirection(MarioPlayer* mario) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (wasTurnaround) {
            Model.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Model.rotation = Quaternion.RotateTowards(Model.rotation, modelRotationTarget, maxRotation);
        }
    }
    private unsafe void OnCannonBoxBoom(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }
        animator.SetTrigger("Boom");
    }
    public void CreateLaunchParticle() {
        LaunchParticle.SetActive(true);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}