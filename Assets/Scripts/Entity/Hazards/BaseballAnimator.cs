using Quantum;
using Quantum.Profiling;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;
using System.Drawing.Drawing2D;
using NSMB.Utilities;

public unsafe class BaseballAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Transform Model, FacingModel;
    [SerializeField] private AudioSource sfx;

    private Quaternion modelRotationTarget, facingRotationTarget;

    public void Start() {
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var phys = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var baseball = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
            Model.localRotation = Quaternion.RotateTowards(Model.localRotation, Quaternion.Euler(0, 0, Model.localRotation.eulerAngles.z + ((float) phys->Velocity.X * -180 * Time.deltaTime)), 2000f * Time.deltaTime);
        }
        transform.position = modifiedZ;

        FacingModel.localRotation = Quaternion.RotateTowards(FacingModel.localRotation, Quaternion.Euler(0, baseball->Facing ? 30 : -30, 0), 2000f * Time.deltaTime);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
    }
}