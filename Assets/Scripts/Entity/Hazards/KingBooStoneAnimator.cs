using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using NSMB.Utilities.Extensions;
using UnityEngine.UIElements;

public unsafe class KingBooStoneAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    public AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Sprite Cooled;
    [SerializeField] private Sprite[] Fired;

    private float SpriteTimer = 0;

    public void Start() {
        QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var holdable = f.Unsafe.GetPointer<Holdable>(EntityRef);
        var throwable = f.Unsafe.GetPointer<ThrowingObject>(EntityRef);

        Vector3 modifiedZ = transform.position;
        //if (f.Exists(holdable->Holder)) {
            modifiedZ.z = -4.1f;
        //} else {
            //modifiedZ.z = 0;
        //}
        transform.position = modifiedZ;

        //Be Fire When Thrown by specifically the boss
        sRenderer.sprite = throwable->Thrown && !throwable->HitSomething && (f.Has<Boss>(holdable->PreviousHolder) || (f.Unsafe.TryGetPointer<MarioPlayer>(holdable->PreviousHolder, out var mar) && mar->IsBoss != EntityRef.None)) ? 
            Fired[(int) Mathf.Floor(SpriteTimer >= Fired.Length - (Time.deltaTime*10) ? SpriteTimer = 0 : SpriteTimer += Time.deltaTime*10)] : Cooled;
        sRenderer.flipX = throwable->Thrown && SpriteTimer >= Fired.Length/2;
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
        Instantiate(Enums.PrefabParticle.Enemy_Puff.GetGameObject(), transform.position, Quaternion.identity);
    }
}