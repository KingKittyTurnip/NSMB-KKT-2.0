using NSMB.Extensions;
using Org.BouncyCastle.Asn1.Pkcs;
using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;

public unsafe class FanAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Transform Blades, Head;
    [SerializeField] private Animator Base;
    [SerializeField] private GameObject BrokenParticles;

    private Quaternion modelRotationTarget;
    private float BladeVelocity;

    public void Start() {
        QuantumEvent.Subscribe<EventOnFanHit>(this, OnFanHit, NetworkHandler.FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var fan = f.Unsafe.GetPointer<Fan>(EntityRef);
        var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);
        float delta = Time.deltaTime;

        BladeVelocity = Mathf.Clamp(BladeVelocity + (delta * ((hazard->LifeTime < 180 && hazard->LifeTime != 0) ? -500 : 500)), 0, 1222);

        bool gladios = (f.Number % 300) > 150;
        BrokenParticles.SetActive(fan->Broken);
        Blades.localRotation = Quaternion.Euler(0, 0, Blades.localRotation.eulerAngles.z - (BladeVelocity * delta));
        Head.localRotation = Quaternion.Euler(0, fan->FellOver ? 0 : (fan->FacingRight ? 45 - fan->TurnEffectorDowntime : -45 + fan->TurnEffectorDowntime), 0);
    }
    private unsafe void OnFanHit(EventOnFanHit e) {
        if (e.Entity != EntityRef) {
            return;
        }
        //sfx.Play();
        Base.SetTrigger(e.Broken ? "Kill" : "Break");
    }

    public override void OnDeactivate() {
        if (!NetworkHandler.IsReplayFastForwarding) {
        }
    }
}