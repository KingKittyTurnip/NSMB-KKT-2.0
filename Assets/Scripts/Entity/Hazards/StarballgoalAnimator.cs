using NSMB;
using NSMB.Entities.World;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class StarballgoalAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    public AudioSource sfx;
    public static event Action<Frame, StarballgoalAnimator> StarballgoalInitialized;
    public static event Action<Frame, StarballgoalAnimator> StarballgoalDestroyed;

    public void Start() {
        QuantumEvent.Subscribe<EventStarBallDestroyed>(this, OnStarballComsumed, FilterOutReplayFastForward);
    }
    public override unsafe void OnActivate(Frame f) {
        StarballgoalInitialized?.Invoke(f, this);
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
        StarballgoalDestroyed?.Invoke(VerifiedFrame, this);
    }
}