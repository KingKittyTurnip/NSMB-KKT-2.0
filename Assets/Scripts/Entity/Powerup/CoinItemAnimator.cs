using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using static UnityEngine.ParticleSystem;
using static UnityEngine.Rendering.DebugUI.Table;

namespace NSMB.Entities.CoinItems {
    public unsafe class CoinItemAnimator : QuantumEntityViewComponent {

        //---Serialized
        [SerializeField] private Transform graphicsRoot, moveBehindBlocksRoot;
        [SerializeField] private List<Renderer> renderers;
        [SerializeField] private Animator childAnimator;
        [SerializeField] private Animation childAnimation;
        [SerializeField] private float blinkingRate = 4, scaleRate = 0.1333f, scaleSize = 0.3f, actualscale = 1;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private ParticleSystem koopaSpawnParticles;

        //---Private
        private int originalSortingOrder;
        private bool inSpawnAnimation;
        private MaterialPropertyBlock mpb;
        private bool Previouslyenabled = false;

        public void OnValidate() {
            //this.SetIfNull(ref renderer, UnityExtensions.GetComponentType.Children);
            //this.SetIfNull(ref childAnimator, UnityExtensions.GetComponentType.Children);
            //this.SetIfNull(ref childAnimation, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventCoinItemBecameActive>(this, OnCoinItemBecameActive);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            //KKT Mod
            QuantumEvent.Subscribe<EventMetalLanded>(this, OnMetalLanded);
        }

        public override void OnActivate(Frame f) {
            if (!f.Unsafe.TryGetPointer(EntityRef, out CoinItem* coinItem)) {
                return;
            }
            var scriptable = QuantumUnityDB.GetGlobalAsset(coinItem->Scriptable);

            originalSortingOrder = renderers[0].sortingOrder;
            foreach (Renderer r in renderers) {
                r.enabled = true;
                r.GetPropertyBlock(mpb = new());
            }

            if (coinItem->SpawnReason == PowerupSpawnReason.BlueKoopa && koopaSpawnParticles) {
                koopaSpawnParticles.Play();
            }

            if (f.Exists(coinItem->ParentMarioPlayer)) {
                // Following mario
                SetSortingRange(15);
                if (childAnimator) {
                    childAnimator.enabled = false;
                }
            } else if (coinItem->BlockSpawn) {
                // Block spawn
                if (moveBehindBlocksRoot) {
                    Vector3 pos = moveBehindBlocksRoot.localPosition;
                    pos.z = 1;
                    moveBehindBlocksRoot.localPosition = pos;
                }

                SetSortingRange(-1000);
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
                if (childAnimation) {
                    childAnimation.Play();
                }
            } else if (coinItem->LaunchSpawn) {
                // Spawn with velocity
                if (moveBehindBlocksRoot) {
                    Vector3 pos = moveBehindBlocksRoot.localPosition;
                    pos.z = 1;
                    moveBehindBlocksRoot.localPosition = pos;
                }

                SetSortingRange(-1000);
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
            } else {
                // Spawned by any other means (blue koopa, usually.)
                if (!IsReplayFastForwarding) {
                    sfx.PlayOneShot(scriptable.BlockSpawnSoundEffect);
                }
                if (childAnimation) {
                    childAnimation.Play();
                }
                SetSortingRange(originalSortingOrder);
            }
        }

        private void SetPropertyBlocks() {
            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(mpb);
            }
        }
        private void SetSortingRange(int sortingnumber) {
            foreach (Renderer r in renderers) {
                r.sortingOrder = sortingnumber;
            }
        }

        public override void OnUpdateView() {
            Frame f = PredictedFrame;

            var hazard = f.Unsafe.GetPointer<Hazard>(EntityRef);
            if (!hazard->IsHazard && !hazard->IsCoinItem) { //Hide if in stage (for respawn shenanigans) this code is yuk otherwise.
                if (f.Unsafe.TryGetPointer<Enemy>(EntityRef, out var enemy)) {
                    foreach (Renderer r in renderers) {
                        r.enabled = enemy->IsActive;
                    }
                    return;
                }
            }

            if (f.Unsafe.TryGetPointer(EntityRef, out CoinItem* coinItem)) {
                //we are coinitem
                if (childAnimator) {
                    childAnimator.SetBool("blockSpawn", coinItem->BlockSpawn && coinItem->SpawnAnimationFrames > 0);
                }

                if (f.Unsafe.TryGetPointer(EntityRef, out PhysicsObject* physicsObject)) {
                    if (childAnimator) {
                        childAnimator.SetBool("onGround", physicsObject->IsTouchingGround);
                    }
                }

                HandleSpawningAnimation(f, coinItem);
            }
            HandleDespawningBlinking(hazard->LifeTime);
        }

        private void HandleSpawningAnimation(Frame f, CoinItem* coinItem) {
            if (f.Exists(coinItem->ParentMarioPlayer) && coinItem->SpawnAnimationFrames > 0) {
                // Following player
                float timeRemaining = coinItem->SpawnAnimationFrames / 60f;
                float adjustment = Mathf.PingPong(timeRemaining, scaleRate) / scaleRate * scaleSize;
                graphicsRoot.localScale = Vector3.one * actualscale * (1 + adjustment);

                if (!inSpawnAnimation) {
                    mpb.SetFloat("WaveEnabled", 0);
                    SetPropertyBlocks();
                    inSpawnAnimation = true;
                }
            } else if (inSpawnAnimation) {
                //renderer.transform.localScale = Vector3.one * actualscale;
                inSpawnAnimation = false;
                SetSortingRange(15);

                if (moveBehindBlocksRoot) {
                    Vector3 pos = moveBehindBlocksRoot.localPosition;
                    pos.z = 0;
                    moveBehindBlocksRoot.localPosition = pos;
                }

                mpb.SetFloat("WaveEnabled", 1);
                SetPropertyBlocks();
            }
        }

        private void HandleDespawningBlinking(float lifetime) {
            bool newlyEnabled = false;
            if (lifetime <= 60 && lifetime != 0 && blinkingRate != -1) {
                newlyEnabled = ((lifetime / 60f * blinkingRate) % 1) > 0.5f;
            } else {
                newlyEnabled = true;
            }
            if (Previouslyenabled != newlyEnabled) {
                Previouslyenabled = newlyEnabled;
                foreach (Renderer r in renderers) {
                    r.enabled = newlyEnabled;
                }
            }
        }

        private void OnCoinItemBecameActive(EventCoinItemBecameActive e) {
            if (e.Entity != EntityRef) {
                return;
            }

            SetSortingRange(originalSortingOrder);
            //renderer.gameObject.transform.localScale = Vector3.one;
            if (childAnimator) {
                childAnimator.enabled = true;
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            if (childAnimator) {
                childAnimator.enabled = false;
            }
            if (childAnimation) {
                childAnimation.enabled = false;
            }
        }

        //KKT Mod
        private void OnMetalLanded(EventMetalLanded e) {
            if (e.Entity != EntityRef) {
                return;
            }

            childAnimator.SetTrigger("Landed");
            Instantiate(Enums.PrefabParticle.Player_MetalLand.GetGameObject(), new Vector3(e.Position.X.AsFloat, e.Position.Y.AsFloat, -5), Quaternion.identity);
        }
    }
}
