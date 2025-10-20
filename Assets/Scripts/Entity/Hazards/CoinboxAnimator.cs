using Quantum;
using Quantum.Profiling;
using System.Collections.Generic;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;

public unsafe class CoinboxAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject coinFromBlockParticle, Breakparticle;
    [SerializeField] private Transform Nose;
    [SerializeField] private Animator animator;
    [SerializeField] private List<Vector3> NosePoints = new(); //Perhaps Put This In The Player Prefs????


    [SerializeField] private AudioSource sfx;
    [SerializeField] private Transform Model;
    private Quaternion modelRotationTarget;
    private bool wasTurnaround;

    private MaterialPropertyBlock materialBlock;
    [SerializeField] private Renderer coinboxRenderer = new();
    private static readonly int ParamBoxType = Shader.PropertyToID("BoxType");
    private int CurrentType = 0;

    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnCoinBoxCoin);
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
        Nose.localScale = NosePoints[0];
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var coinbox = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);
        bool hasHolder = f.Exists(holdable->Holder);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;

        //Set Visuals From Owner
        if (hasHolder && CurrentType == 0) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            for (int i = 0; f.SimulationConfig.CharacterDatas.Length > i; i++) {
                if (f.SimulationConfig.CharacterDatas[i] == mario->CharacterAsset)
                    SetMat(i + 1);
            }
        } else if (!coinbox->Thrown && !hasHolder && CurrentType != 0) {
            SetMat(0);
        }

            float delta = Time.deltaTime;
        if (hasHolder) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(holdable->Holder);

            SetFacingDirection(f, mario, marioPhysicsObject);
            InterpolateFacingDirection(mario);
        } else {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
            Model.rotation = Quaternion.RotateTowards(Model.rotation, Quaternion.Euler(0, Model.rotation.eulerAngles.y + ((float) physicsObject->Velocity.X * 100 * Time.deltaTime), 0), 2000f * Time.deltaTime);
        }
    }
    private unsafe void OnCoinBoxCoin(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }
        //sfx.Play();

        GameObject coin = Instantiate(coinFromBlockParticle, e.pos.ToUnityVector3(), Quaternion.identity);
        coin.GetComponentInChildren<Animator>().SetBool("down", false);
        Destroy(coin, 1);

        animator.SetTrigger("Collect");
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Breakparticle, transform.position, Quaternion.identity);
        }
    }

    private void SetFacingDirection(Frame f, MarioPlayer* mario, PhysicsObject* physicsObject) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.SetFacingDirection");

        float delta = Time.deltaTime;

        modelRotationTarget = Quaternion.Euler(0, mario->FacingRight ? 110 : 250, 0);

        wasTurnaround = mario->IsTurnaround;
    }

    private void InterpolateFacingDirection(MarioPlayer* mario) {
        using var profilerScope = HostProfiler.Start("MarioPlayerAnimator.InterpolateFacingDirection");
        if (wasTurnaround) {
            Model.rotation = modelRotationTarget;
        } else /* if (!GameManager.Instance.GameEnded) */ {
            float maxRotation = 2000f * Time.deltaTime;
            Model.rotation = Quaternion.RotateTowards(Model.rotation, modelRotationTarget, maxRotation);
        }
    }

    private void SetMat(int Type) {
        if (materialBlock == null)
            materialBlock = new();
        CurrentType = Type;
        materialBlock.SetFloat(ParamBoxType, Type);
        Nose.localScale = NosePoints[Type];
        coinboxRenderer.SetPropertyBlock(materialBlock);
        Instantiate(Breakparticle, transform.position, Quaternion.identity);
    }

        private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
        }
}