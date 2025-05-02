using Photon.Deterministic;
using static UnityEngine.UI.GridLayoutGroup;
using UnityEditor.VersionControl;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using System;

namespace Quantum {
    public unsafe partial struct Hazard {

        public static event Action<Frame, bool> HeftySpawned;
        public static event Action<Frame, bool> HeftyDespawned;

        public void Initialize(Frame f, EntityRef thisEntity, EntityRef owner, FPVector2 spawnpoint, SpawnReason spawnReason) {
            var transform = f.Unsafe.GetPointer<Transform2D>(thisEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(thisEntity);

            // IdeaBulb Carry On Creation
            if (spawnReason == SpawnReason.Bulb && f.Exists(owner)) {
                f.Unsafe.TryGetPointer(thisEntity, out Holdable* holdable);
                if (holdable != null) {
                    holdable->Holder = owner;
                }
            }

            // Create Icon on Map
            if (IsHefty) {
                HeftySpawned?.Invoke(f, true);
            }

            // Physics
            transform->Position = spawnpoint;
            physicsObject->Velocity = new(SpawningVelocityRange.X /*Insert RNG Calculator*/, SpawningVelocityRange.Y);
        }

        public void Despawned(Frame f) {
            if (IsHefty) {
                HeftySpawned?.Invoke(f, false);
            }
        }
    }
}