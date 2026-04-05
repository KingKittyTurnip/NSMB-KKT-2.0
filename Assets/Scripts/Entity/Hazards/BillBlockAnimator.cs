using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using NSMB.Utilities;

public unsafe class BillBlockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    //[SerializeField] private GameObject BoostParticles;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;

    [Space]
    [SerializeField] private AudioClip Fail;
    [Space]
    [SerializeField] private GameObject ExplosionFail;

    private Quaternion modelRotationTarget;
    private bool wasTurnaround;

    private MaterialPropertyBlock materialBlock;
    List<Renderer> renderers = new();
    [SerializeField] private Texture BaseTexture, InvalidTexture;

    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnBillBlockFail);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);

        renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
        renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
        materialBlock = new();
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var billblock = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;

        //BoostParticles.SetActive(billblock->CanHit);
        float delta = Time.deltaTime;
        if (f.Exists(holdable->Holder)) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);

            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);

            //Model.rotation = Quaternion.Euler(0, holder.AnimationController.Rotation, 0);
            bool Powered = billblock->ReusableTimer != 0 && f.GetPlayerInput(mario->PlayerRef)->PowerupAction.IsDown;
            animator.SetBool("Powered", Powered && billblock->ReusableTimer < 240);
            animator.SetBool("Failing", Powered && billblock->ReusableTimer <= 60);
        } else if (billblock->Thrown) {
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, billblock->Facing ? 110 : 250, 0), 200f * Time.deltaTime);
            animator.SetBool("Powered", false);
            animator.SetBool("Failing", true);
        } else {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            //Model.rotation = Quaternion.Euler(0, billblock->Facing ? 90 : -90, 0);
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
            animator.SetBool("Powered", false);
            animator.SetBool("Failing", false);
        }

        //Set Color
        var i = BaseTexture;
        if (f.Exists(holdable->PreviousHolder)) {
            i = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder)->CharacterAsset).BillblockTexture;
            if (i == null) {
                i = InvalidTexture;
            }
        }
        materialBlock.SetTexture("Texture", i);
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
    private unsafe void OnBillBlockFail(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(Fail);
        animator.SetTrigger("Fail");

        Instantiate(ExplosionFail, e.pos.ToUnityVector3(), Quaternion.identity);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
        }
}