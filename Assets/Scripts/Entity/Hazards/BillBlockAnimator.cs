using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class BillBlockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    //[SerializeField] private GameObject BoostParticles;
    [SerializeField] private Animator animator;

    [SerializeField] private Transform Model;
    private Quaternion modelRotationTarget;
    private bool wasTurnaround;

    //TODO: This Code
    private MaterialPropertyBlock materialBlock;
    [SerializeField] private Renderer coinboxRenderer = new();
    private static readonly int ParamBoxType = Shader.PropertyToID("BoxType");
    private int CurrentType = 0;

    public void Start() {
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

        animator.SetBool("Powered", true);
        animator.SetBool("Failing", true);
        //BoostParticles.SetActive(billblock->CanHit);
        float delta = Time.deltaTime;
        if (f.Exists(holdable->Holder)) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);

            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);

            //Model.rotation = Quaternion.Euler(0, holder.AnimationController.Rotation, 0);
        } else {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            //Model.rotation = Quaternion.Euler(0, billblock->Facing ? 90 : -90, 0);
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }

    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
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

}