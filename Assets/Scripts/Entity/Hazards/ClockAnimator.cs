using NSMB.Extensions;
using NSMB.Utils;
using Org.BouncyCastle.Asn1.Pkcs;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Text;
using UnityEngine;

public unsafe class ClockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
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
        string text = e.Time.ToString();
        Color32 color = new Color32(51, 133, 255, 255);
        if (e.Overtime) { text = "-0-";  color = new Color32(255, 0, 0, 255);
        } else if (e.TickTimeup) { text = "0:10";  color = new Color32(255, 0, 0, 255);
        } else if (e.ResetTime) { text = Utils.SecondsToMinuteSeconds(e.Time);  color = new Color32(17, 247, 33, 255);
        } if (e.Time < 0) { color = new Color32(201, 14, 186, 255);
        }
        //TODO: Make Timer Text Blink Instead of Bouce
        GameObject number = Instantiate(coinNumberParticle, e.pos.ToUnityVector3() + new Vector3(0, 0, -1), Quaternion.identity);
        number.GetComponentInChildren<NumberParticle>().Initialize(
            Utils.GetSymbolString(text, Utils.numberSymbols),
            color,
            e.TickTimeup
        );
        Instantiate(
            breakPrefab,
            transform.position + (Vector3.back * 6) + (Vector3.up * 0.1f),
            Quaternion.identity);
    }
}