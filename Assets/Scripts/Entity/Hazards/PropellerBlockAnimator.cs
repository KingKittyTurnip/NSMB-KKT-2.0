using Quantum;
using Quantum.Profiling;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class PropellerBlockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private float propellerVelocity;
    [SerializeField] private Transform Model;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;

    private Quaternion modelRotationTarget;
    private bool modelRotateInstantly;
    public bool wasTurnaround;

    public void Start() {
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;

        float delta = Time.deltaTime;
        if (f.Exists(holdable->Holder)) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);
            propellerVelocity = Mathf.Clamp(propellerVelocity + (30 * ((mario->IsSpinnerFlying || mario->IsPropellerFlying || mario->UsedPropellerThisJump) ? -1 : 1) * delta), -15, -1);
            animator.SetFloat("PropellerSpeed", propellerVelocity);


            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);

            //Model.rotation = Quaternion.Euler(0, holder.AnimationController.Rotation, 0);
        } else {
            propellerVelocity = Mathf.Clamp(propellerVelocity + (30 * delta), -15, -1);
            animator.SetFloat("PropellerSpeed", propellerVelocity);
            if (Model.rotation.y != 0)
                Model.rotation = Quaternion.Euler(0, 180, 0);
        }

    }

    private void SetFacingDirection(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.SetFacingDirection");
        //TODO: refactor
        /*
        if (GameManager.Instance.GameEnded) {
            if (mario->IsDead) {
                modelRotationTarget.Set(0, 180, 0);
                modelRotateInstantly = true;
            }
            return;
        }
        */

        float delta = Time.deltaTime;

        modelRotateInstantly = false;

        if (wasTurnaround || mario->IsSkidding || mario->IsTurnaround || animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround")) {
            bool flip = mario->FacingRight ^ (animator.GetCurrentAnimatorStateInfo(0).IsName("turnaround") || mario->IsSkidding);
            modelRotationTarget = Quaternion.Euler(0, flip ? 250 : 110, 0);
            modelRotateInstantly = true;

        } else if (f.Unsafe.TryGetPointer(mario->CurrentSpinner, out Spinner* spinner)
                   && physicsObject->IsTouchingGround && mario->ProjectileDelayFrames == 0
                   && Mathf.Abs(physicsObject->Velocity.X.AsFloat) < 0.3f && !mario->HeldEntity.IsValid
                   && !animator.GetCurrentAnimatorStateInfo(0).IsName("fireball")) {

            modelRotationTarget *= Quaternion.Euler(0, spinner->AngularVelocity.AsFloat * delta, 0);
            modelRotateInstantly = true;

        } else if (mario->IsSpinnerFlying || mario->IsPropellerFlying) {
            modelRotationTarget *= Quaternion.Euler(0, (-1200 - ((mario->PropellerLaunchFrames / 60f) * 1400) - (mario->IsDrilling ? 900 : 0) + (mario->IsPropellerFlying && mario->PropellerSpinFrames == 0 && physicsObject->Velocity.Y < 0 ? 700 : 0)) * delta, 0);
            modelRotateInstantly = true;

        } else if (mario->IsWallsliding) {
            modelRotationTarget = Quaternion.Euler(0, mario->WallslideRight ? 110 : 250, 0);
        } else {
            modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? 110 : 250, 0);
        }

        wasTurnaround = mario->IsTurnaround;
    }

    private void InterpolateFacingDirection(MarioPlayer* mario) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (modelRotateInstantly || wasTurnaround) {
            Model.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Model.rotation = Quaternion.RotateTowards(Model.rotation, modelRotationTarget, maxRotation);
        }
    }


        private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
        }
}