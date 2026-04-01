using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class LemmyBallAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    //[SerializeField] private GameObject BoostParticles;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;

    private Quaternion modelRotationTarget;
    private bool wasTurnaround;

    public void Start() {
        QuantumEvent.Subscribe<EventLemmyBallLand>(this, OnBounce, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventLemmyBallHitEntity>(this, OnHitEntity, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var LemmyBall = f.Unsafe.GetPointer<LemmyBall>(EntityRef);
        var PhysObj = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        //float delta = Time.deltaTime;
        if (!PhysObj->IsTouchingGround) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            //Model.rotation = Quaternion.Euler(0, billblock->Facing ? 90 : -90, 0);
            //Model.rotation = Quaternion.Euler(0, -55, Model.transform.eulerAngles.z + (LemmyBall->FacingRight ? -3 : 3));
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, -55, Model.rotation.eulerAngles.z + ((LemmyBall->FacingRight ? -3 : 3) * 100 * Time.deltaTime)), 2000f * Time.deltaTime);
            //Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }
    }

    private void OnBounce(EventLemmyBallLand e) {
        if (e.Entity != EntityRef) {
            return;
        }
        animator.SetTrigger("Bounce");
    }

    private void OnHitEntity(EventLemmyBallHitEntity e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (!sfx.isPlaying)
            sfx.Play();
    }
}