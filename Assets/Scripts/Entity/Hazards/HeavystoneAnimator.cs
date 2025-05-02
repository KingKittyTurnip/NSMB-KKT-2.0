using NSMB.Extensions;
using Org.BouncyCastle.Asn1.Pkcs;
using Quantum;
using Quantum.Profiling;
using UnityEngine;

public unsafe class HeavystoneAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    public AudioSource sfx;
    [SerializeField] private GameObject breakPrefab;
    public void Start() {
        QuantumEvent.Subscribe<EventHeavyStoneLand>(this, OnHeavyStoneLand, NetworkHandler.FilterOutReplayFastForward);
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
    private unsafe void OnHeavyStoneLand(EventHeavyStoneLand e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.Play();
        Instantiate(
            Enums.PrefabParticle.Player_Groundpound.GetGameObject(),
            transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
            Quaternion.identity);
    }

    public override void OnDeactivate() {
        if (!NetworkHandler.IsReplayFastForwarding) {
            Instantiate(breakPrefab, transform.position, Quaternion.identity);
        }
    }
}