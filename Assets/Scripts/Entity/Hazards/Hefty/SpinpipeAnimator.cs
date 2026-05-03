using NSMB.Particles;
using NSMB.UI.Game;
using Quantum;
using System.Security.Policy;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using static NSMB.Utilities.QuantumViewUtils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

public unsafe class SpinpipeAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer srenderer;
    [SerializeField] private Sprite SpriteNormal, SpriteNormalBottom, SpriteSturdy, SpriteSturdyBottom;
    [SerializeField] private SimplePhysicsMover breakPrefab;
    [SerializeField] private AudioSource sfx;

    public void Start() {
        QuantumEvent.Subscribe<EventSpinpipeLand>(this, OnPipeLand);
        QuantumEvent.Subscribe<EventSpinpipeDestroy>(this, OnPipeBreak);
        QuantumEvent.Subscribe<EventIsNowResistantHit>(this, OnResist);
    }
    //public override unsafe void OnUpdateView() {
    //}
    public override void OnActivate(Frame f) {
        if (f.Unsafe.TryGetPointer<Spinpipe>(EntityRef, out var spinpipe)) {
            srenderer.sprite = spinpipe->Broken ? spinpipe->Sturdy ? SpriteSturdyBottom : SpriteNormalBottom : spinpipe->Sturdy ? SpriteSturdy : SpriteNormal;
        }
    }
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

        var spinpipe = e.f.Unsafe.GetPointer<Spinpipe>(EntityRef);

        srenderer.sprite = spinpipe->Sturdy ? SpriteSturdyBottom : SpriteNormalBottom;
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

    private int LastFrame = 0;
    private unsafe void OnResist(EventIsNowResistantHit e) {
        if (e.Entity != EntityRef) {
            return;
        }
        if (e.ThisFrame > LastFrame) {
            sfx.Play();
        }
        LastFrame = e.ThisFrame + 10;
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
            Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
        }
    }
}