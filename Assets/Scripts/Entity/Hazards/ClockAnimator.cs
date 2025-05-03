using NSMB.Extensions;
using NSMB.Utils;
using Org.BouncyCastle.Asn1.Pkcs;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UnityEngine;

public unsafe class ClockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private GameObject breakPrefab, coinNumberParticle;

    private MaterialPropertyBlock materialBlock;
    [SerializeField] private Renderer clockRenderer = new();
    private static readonly int ParamClockType = Shader.PropertyToID("_ClockType");
    public void Start() {
        QuantumEvent.Subscribe<EventClockCollect>(this, ClockCollect, NetworkHandler.FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }

        if (materialBlock != null) {
            return;
        }
        var clock = f.Unsafe.GetPointer<Clock>(EntityRef);
        int Type = clock->ResetTime ? 1 : clock->TickTimeup ? 2 : clock->Time >= 0 ? 0 : 3;
        materialBlock = new();
        materialBlock.SetFloat(ParamClockType, Type);
        clockRenderer.SetPropertyBlock(materialBlock);
    }
    private unsafe void ClockCollect(EventClockCollect e) {
        if (e.Entity != EntityRef) {
            return;
        }
        animator.SetTrigger("Collected");
        sfx.Play();
        //TODO: Make Clock Collection Particle
        //Instantiate(
        //    Enums.PrefabParticle.Player_Groundpound.GetGameObject(),
        //    transform.position + (Vector3.back * 5) + (Vector3.up * 0.1f),
        //    Quaternion.identity);

        //TODO: Make Timer Text Blink Instead of Bouce
        GameObject number = Instantiate(coinNumberParticle, e.pos.ToUnityVector3() + new Vector3(0, 0, -1), Quaternion.identity);
        number.GetComponentInChildren<NumberParticle>().Initialize(
            Utils.GetSymbolString(e.Time.ToString(), Utils.numberSymbols),
            new Color32(51, 133, 255, 255),
            e.TickTimeup
        );
    }
}