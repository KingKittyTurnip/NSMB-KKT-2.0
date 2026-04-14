using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using Photon.Deterministic;

public unsafe class TornadoAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject Model;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip Launch;

    public void Start() {
        QuantumEvent.Subscribe<EventTornadoLaunched>(this, OnLaunch);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);

        if (hazard->IsHazard && hazard->LifeTime < 60) {
            Model.transform.localScale = Vector3.one * ((float)hazard->LifeTime)/60f;
        }
    }
    private unsafe void OnLaunch(EventTornadoLaunched e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(Launch);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }
}