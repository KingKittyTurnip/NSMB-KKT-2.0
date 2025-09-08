using NSMB.Particles;
using Quantum;
using UnityEngine;
using UnityEngine.UIElements;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class SpinpipeAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer srenderer;
    [SerializeField] private Sprite sprite;
    [SerializeField] private SimplePhysicsMover breakPrefab;

    public void Start() {
        QuantumEvent.Subscribe<EventSpinpipeLand>(this, OnPipeLand);
        QuantumEvent.Subscribe<EventSpinpipeDestroy>(this, OnPipeBreak);
    }
    //public override unsafe void OnUpdateView() {
    //}
    private unsafe void OnPipeLand(EventSpinpipeLand e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (!e.Despawn) {
            Instantiate(
            Enums.PrefabParticle.Player_Groundpound.GetGameObject(),
            transform.position + (Vector3.back * 5) + Vector3.down,
            Quaternion.identity);
        } else {
            animator.Play("Despawn");
        }
    }
    private unsafe void OnPipeBreak(EventSpinpipeDestroy e) {
        if (e.Entity != EntityRef) {
            return;
        }

        srenderer.sprite = sprite;
        //sumon break particle
        //var pipe = VerifiedFrame.Unsafe.GetPointer<BreakableObject>(e.Entity);

        SimplePhysicsMover particle = Instantiate(breakPrefab, transform.position, transform.rotation);
        //particle.transform.localScale = transform.localScale;
        //SpriteRenderer sRenderer = particle.GetComponentInChildren<SpriteRenderer>();
        //sRenderer.transform.localPosition = new Vector2(0, -(e.Height / 2).AsFloat);

        particle.velocity = (new Vector2(e.Right ? -1 : 1, 1) * 9.5f) + (Vector2.up * 3.5f);

        Vector2 a = particle.velocity;
        Vector2 b = transform.up;
        float angularVelocity = (a.x * b.y) - (b.x * a.y);
        particle.angularVelocity = angularVelocity * -(400f/5);

        //activeParticle = particle;
        //currentEventKey = e;
        Destroy(particle.gameObject, 3f);
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }
}