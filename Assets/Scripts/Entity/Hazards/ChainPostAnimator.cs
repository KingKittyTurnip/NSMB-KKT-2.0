using Quantum;
using Quantum.Profiling;
using UnityEngine;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.QuantumViewUtils;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using static UnityEditor.PlayerSettings;

public unsafe class ChainPostAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject chainbreakParticle;

    [SerializeField] private List<Transform> Chains = new();
    [SerializeField] private AudioSource sfx;

    private Quaternion modelRotationTarget, facingRotationTarget;

    public void Start() {
        QuantumEvent.Subscribe<EventThrowObjSimple>(this, OnPostChainBreak);

        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var phys = f.Unsafe.GetPointer<PhysicsObject>(EntityRef);
        var chainPost = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        } else {
            modifiedZ.z = 0;
        }
        transform.position = modifiedZ;


        if (f.Exists(chainPost->ConnectedObject)) {
            CalculateChains(f, chainPost->ConnectedObject, chainPost->Varient == 1 ? 8 : 4, 5, transform.position);
        }
    }

    private void CalculateChains(Frame f, EntityRef Target, float Max, float Length, Vector3 Ownerpos) {
        var e = f.Unsafe.GetPointer<Transform2D>(Target)->Position;
        var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        Vector3 Ourpos = new((float) e.X, (float) e.Y, -1);
        float Dis = Mathf.Max((Length - UnwrapDistance(stage, Ownerpos, Ourpos)) * 0.05f, 0);

        // Cycle Through All Chains
        for (int i = 0; Max > i; i++) {
            float offset = (i + 1) / (Max + 1);
            float yoffset = (1 - Mathf.Abs(((i +1) / ((Max + 1) / 2)) - 1)) * 5;// * 0.25f;
            UnwrapWorldLocations(stage, Ownerpos, Ourpos, out Vector3 a, out Vector3 b);
            Chains[i].position = (a + ((b - a) * offset)) - new Vector3(0, (yoffset - ((yoffset * yoffset * 0.08f)) - 0.5f) * Dis, 0);
        }
    }
    public static void UnwrapWorldLocations(VersusStageData stage, Vector3 a, Vector3 b, out Vector3 newA, out Vector3 newB) {
        newA = a;
        newB = b;

        if (!stage.IsWrappingLevel) {
            return;
        }

        float width = stage.TileDimensions.X * 0.5f;
        if (Mathf.Abs(newA.x - newB.x) > width / 2) {
            newB.x += width * (newB.x > (float) stage.StageWorldMin.X + (width / 2) ? -1 : 1);
        }
    }
    public float UnwrapDistance(VersusStageData stage, Vector2 a, Vector2 b) {
        if (stage.IsWrappingLevel) {
            float width = stage.TileDimensions.X * 0.5f;
            if (Mathf.Abs(a.x - b.x) > width / 2) {
                b.x += width * (b.x > (float) stage.StageWorldMin.X + (width / 2) ? -1 : 1);
            }
        }
        return Vector2.Distance(a, b);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }

    private void OnPlayComboSound(EventPlayComboSound e) {
        if (e.Entity != EntityRef) {
            return;
        }

        sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(e.Combo));
    }

    private void OnPostChainBreak(EventThrowObjSimple e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.pos.Y == -999) { //a lil dumb but it works well
            //Yank
            sfx.PlayOneShot(QuantumUtils.GetComboSoundEffect(1));

        } else {
            //Break Chains
            foreach (var chain in Chains) {
                if (chain.position != transform.position) {
                    Instantiate(chainbreakParticle, transform.position, Quaternion.identity);
                }
                chain.gameObject.SetActive(false);
            }
        }
    }
}