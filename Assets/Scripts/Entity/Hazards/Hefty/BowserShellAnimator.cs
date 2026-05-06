using Quantum;
using Quantum.Profiling;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;
using System.Drawing.Drawing2D;
using NSMB.Utilities;

public unsafe class BowserShellAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Transform Model;
    [SerializeField] private AudioSource sfx;

    private Quaternion modelRotationTarget, facingRotationTarget;

    public void Start() {
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnPlayBump);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var phys = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
            Model.localRotation = Quaternion.RotateTowards(Model.localRotation, Quaternion.Euler(0, Model.localRotation.eulerAngles.y + ((float) phys->Velocity.X * -100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }
        transform.position = modifiedZ;
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
    private void OnPlayBump(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.World_Block_Bump);
    }
}