using NSMB.Particles;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

public unsafe class ClockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject breakPrefab, coinNumberParticle;

    private MaterialPropertyBlock materialBlock;
    [SerializeField] private Renderer clockRenderer = new();
    private static readonly int ParamClockType = Shader.PropertyToID("_ClockType");

    public void Start() {
        QuantumEvent.Subscribe<EventClockCollect>(this, ClockCollect);
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
        int Type = (int) clock->type;
        materialBlock = new();
        materialBlock.SetFloat(ParamClockType, Type);
        clockRenderer.SetPropertyBlock(materialBlock);
    }

    private unsafe void ClockCollect(EventClockCollect e) {
        if (e.Entity != EntityRef) {
            return;
        }
        string text = "";
        Color32 color = new Color32(51, 133, 255, 255);
        if (e.Overtime) { 
            text = "-";
            color = new Color32(255, 0, 0, 255);
        } else if (e.type == ClockType.Timeup) {
            text = Utils.SecondsToMinuteSeconds(e.Time);
            color = new Color32(255, 0, 0, 255);
        } else if (e.type == ClockType.Reset) { 
            text = Utils.SecondsToMinuteSeconds(e.Time);
            color = new Color32(17, 247, 33, 255);
        } else {
            text = (e.type == ClockType.Add ? "+" : "-") + Utils.SecondsToMinuteSeconds(e.Time);
            if (e.type == ClockType.Subtract) {
                text = "-";
                color = new Color32(201, 14, 186, 255);
            } else {
                text = "+";
            }
            text += Utils.SecondsToMinuteSeconds(e.Time);
        }

        GameObject number = Instantiate(coinNumberParticle, e.pos.ToUnityVector3() + new Vector3(0, 0, -1), Quaternion.identity);
        number.GetComponentInChildren<NumberParticle>().Initialize(
            Utils.GetSymbolString(text, Utils.numberSymbols),
            color,
            (e.type == ClockType.Timeup && !e.Overtime)
        );
        Instantiate(
            breakPrefab,
            transform.position + (Vector3.back * 6) + (Vector3.up * 0.1f),
            Quaternion.identity);
    }
}