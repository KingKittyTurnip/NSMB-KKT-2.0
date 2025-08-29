using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class StarballgoalAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    public AudioSource sfx;

    public void Start() {
        QuantumEvent.Subscribe<EventStarBallDestroyed>(this, OnStarballComsumed, FilterOutReplayFastForward);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
        }
    }

    private void OnStarballComsumed(EventStarBallDestroyed e) {
        if (e.Starballgoal != EntityRef) {
            return;
        }
        sfx.Play();
        animator.Play("GetRemoved");
    }
}