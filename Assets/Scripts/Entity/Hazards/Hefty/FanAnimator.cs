using Newtonsoft.Json.Linq;
using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

public unsafe class FanAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Transform Blades, Head;
    [SerializeField] private Animator Base;
    [SerializeField] private GameObject BrokenParticles;
    [SerializeField] private AudioSource ClickSound;
    [SerializeField] private AudioSource BrokenSound;
    //[SerializeField] private AudioSource FanTwirl;
    [SerializeField] private AudioSource GlobalStorm;

    private float BladeVelocity;
    private bool Broken = false;

    //public List<Material> mats = new();
    public Texture GreenFanTexture;
    private bool SturdyComplete = false;
    List<Renderer> renderers = new();

    public WeatherParticleAnimator weatherPar;

    public void Start() {
        QuantumEvent.Subscribe<EventOnFanHit>(this, OnFanHit);
        QuantumEvent.Subscribe<EventOnFanSwitch>(this, OnFanSwitch);
    }
    public override unsafe void OnUpdateView() {
        Frame f = PredictedFrame;

        if (!f.Exists(EntityRef)) {
            return;
        }
        var fan = f.Unsafe.GetPointer<Fan>(EntityRef);
        var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);
        float delta = Time.deltaTime;

        BladeVelocity = Mathf.Clamp(BladeVelocity + (delta * ((hazard->LifeTime < 180 && hazard->LifeTime != 0) || fan->Cooldown > 0 ? -500 : 500)), 0, 1222);
        //FanTwirl.volume = BladeVelocity/1222;
        GlobalStorm.volume = BladeVelocity/6110;

        bool gladios = (f.Number % 300) > 150;
        if (fan->Broken && !BrokenSound.isPlaying) {
            BrokenSound.Play();
        }
        BrokenParticles.SetActive(fan->Broken);
        Blades.localRotation = Quaternion.Euler(0, 0, Blades.localRotation.eulerAngles.z - (BladeVelocity * delta));
        Head.localRotation = Quaternion.Euler(0, fan->FellOver ? 0 : (fan->FacingRight ? 45 - fan->TurnEffectorDowntime : -45 + fan->TurnEffectorDowntime), 0);
        weatherPar.UpdateVelocity(new Vector3(fan->FacingRight ? -40 : -40 * ((float) (fan->TurnEffectorDowntime / (FP) 45) - 1), 0, 0));

        if (fan->Sturdy && !SturdyComplete) {
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            Debug.Log("SturdyFan!");
            foreach (Renderer r in renderers) {
                r.material.SetTexture("_BaseMap", GreenFanTexture);
            }
            SturdyComplete = true;
        }
    }
    private unsafe void OnFanHit(EventOnFanHit e) {
        if (e.Entity != EntityRef) {
            return;
        }
        Broken = true;
        Base.SetTrigger(e.Broken ? "Kill" : "Break");
    }
    private unsafe void OnFanSwitch(EventOnFanSwitch e) {
        weatherPar.UpdateEmission(e.SwitchOn || Broken);
        if (e.Entity != EntityRef) {
            return;
        }
        ClickSound.Play();
    }

    public override void OnDeactivate() {
        if (!IsReplayFastForwarding) {
        }
    }
}