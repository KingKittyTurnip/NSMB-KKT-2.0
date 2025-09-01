using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Android;

using Quantum;

public class WeatherParticleAnimator : MonoBehaviour {

    /*
     * TODO: A New Renderlayer
     * This Layer Would Render loop particles BUT- Allow it To Render In Front (Ig-)
     * After Doing So Change The Code To Render It in Front
    */

    //---Public Variables
    public bool Enabled;
    private Vector3  Pos;
    public float IntensityModifier = 1;

    //---Weather Particle Variables
    [SerializeField] private ParticleSystem Particle;
    private ParticleSystem.ShapeModule area;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.VelocityOverLifetimeModule velocity;

    private VersusStageData stage;

    public void Start() {
        // Set Vars
        area = Particle.shape;
        emission = Particle.emission;
        velocity = Particle.velocityOverLifetime;

        // Enable?
        emission.enabled = Enabled;

        // Set Size And Amount Relitive To The Stage
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindFirstObjectByType<QuantumMapData>().Asset.UserAsset);

        emission.rateOverTimeMultiplier *= ((stage.TileDimensions.X * stage.TileDimensions.Y) / 4);
        area.scale = new Vector3(stage.TileDimensions.X / 2, stage.TileDimensions.Y / 2, 1);
        Pos = Particle.transform.position = new Vector3(0,0,-1); // new Vector3(stage.TilemapWorldPosition.X.AsFloat/4, stage.TilemapWorldPosition.Y.AsFloat/4, 1/*-6.5f*/);
    }
    public void FixedUpdate() {
        Particle.transform.position = Pos;
    }

    public void UpdateEmission(bool enabled) {
        emission.enabled = enabled;
    }

    public void UpdateVelocity(Vector3 vel) {
        velocity = Particle.velocityOverLifetime;
        var x = velocity.x; var y = velocity.y; var z = velocity.z;

        velocity.xMultiplier = vel.x;
        velocity.yMultiplier = vel.y;
        velocity.zMultiplier = vel.z;
    }
}
