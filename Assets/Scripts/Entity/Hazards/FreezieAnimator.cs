using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;

public unsafe class FreezieAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    public AudioSource sfx;
    [SerializeField] private GameObject breakPrefab;
    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnShatter);
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
    }
    private unsafe void OnShatter(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }

        Instantiate(
            breakPrefab,
            new Vector3(e.pos.X.AsFloat, e.pos.Y.AsFloat, 0),
            Quaternion.identity);
        sfx.PlayOneShot(SoundEffect.Powerup_Iceball_Break);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(breakPrefab, transform.position, Quaternion.identity);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(SoundEffect.Powerup_Iceball_Break);
    }
}