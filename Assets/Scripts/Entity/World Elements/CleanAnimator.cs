using NSMB.Particles;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using TMPro;
using UnityEngine;

public unsafe class CleanAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private TMP_Text RemainingText;
    [SerializeField] private GameObject GuideText;

    public void Start() {
        QuantumEvent.Subscribe<EventCleanCounterUpdate>(this, OnUpdateCounter);
    }

    private unsafe void OnUpdateCounter(EventCleanCounterUpdate e) {
        GuideText.SetActive(false);
        bool MessUpCounter = e.Counter <= 15 && Random.Range(0, 1f) > 0.9f;

        RemainingText.text = "Switches Remaining: " + (MessUpCounter ? e.Counter + 10 : e.Counter).ToString();
    }
}