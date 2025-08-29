using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System.Drawing.Drawing2D;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class StarballAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Transform Ball, Ring, Contents;
    [SerializeField] private GameObject breakPrefab;
    public AudioSource sfx;

    private Quaternion modelRotationTarget;

    public void Start() {
        //QuantumEvent.Subscribe<EventStarBallDestroyed>(this, StarBallDestroyed, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventStarBallLand>(this, OnStarBallLand, FilterOutReplayFastForward);
        //QuantumEvent.Subscribe<EventStarBallJump>(this, OnStarBallJump, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var starball = f.Unsafe.GetPointer<Starball>(EntityRef);
        float delta = Time.deltaTime;

        float inputstrength = 0;
        if (starball->Rider != EntityRef.None) {
            Quantum.Input inputs = *f.GetPlayerInput(f.Unsafe.GetPointer<MarioPlayer>(starball->Rider)->PlayerRef);
            if (inputs.Left.IsDown || inputs.Right.IsDown) {
                inputstrength = (inputs.Left.IsDown ? 1 : -1) * 40;
            }
        }

        Contents.rotation = Quaternion.RotateTowards(Contents.rotation, Quaternion.Euler(0, 0, Contents.rotation.eulerAngles.z + ((float) physicsObject->Velocity.X * -100 * Time.deltaTime)), 2000f * Time.deltaTime);
        Ball.rotation = Quaternion.RotateTowards(Contents.rotation, Quaternion.Euler(0, 0, Contents.rotation.eulerAngles.z), 200f * Time.deltaTime);
        Ring.localRotation = Quaternion.RotateTowards(Ring.localRotation, Quaternion.Euler(-5, 0, inputstrength), 400f * Time.deltaTime);

    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
        }
    }

    private void OnStarBallLand(EventStarBallLand e) {
        if (e.Entity != EntityRef) {
            return;
        }
        sfx.PlayOneShot(e.Hard ? SoundEffect.Powerup_MegaMushroom_Groundpound : SoundEffect.Player_Sound_Collision);
        Instantiate(
            Enums.PrefabParticle.Player_Groundpound.GetGameObject(),
            transform.position + (Vector3.down * 0.5f),
            Quaternion.identity);
        }
    }