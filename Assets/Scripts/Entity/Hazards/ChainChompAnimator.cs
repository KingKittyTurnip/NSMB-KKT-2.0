using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using System.Collections.Generic;

public unsafe class ChainChompAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    //[SerializeField] private GameObject BoostParticles;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;

    [Space]
    [SerializeField] private AudioClip Launch;
    [SerializeField] private AudioClip Chain;
    [SerializeField] private AudioClip Bark;
    [Space]
    [SerializeField] private GameObject LaunchParticle;

    private Quaternion modelRotationTarget;
    private bool wasTurnaround;


    public void Start() {
        //QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnCannonBoxBoom);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var chainchomp = f.Unsafe.GetPointer<ChainChomp>(EntityRef);
        //var cannonbox = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        //if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        //} else {
        //    modifiedZ.z = 0;
        //}
        transform.position = modifiedZ;

        //BoostParticles.SetActive(billblock->CanHit);
        /*float delta = Time.deltaTime;
        if (f.Exists(holdable->Holder)) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);

            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);

            //Model.rotation = Quaternion.Euler(0, holder.AnimationController.Rotation, 0);
        } else if (!cannonbox->Thrown) {
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, cannonbox->Facing ? 110 : 250, 0), 200f * Time.deltaTime);
        } else {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            //Model.rotation = Quaternion.Euler(0, billblock->Facing ? 90 : -90, 0);
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }*/
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
        sfx.PlayOneShot(Launch);
        animator.SetTrigger("Boom");

        Instantiate(LaunchParticle, e.pos.ToUnityVector3(), Quaternion.identity);
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }
}