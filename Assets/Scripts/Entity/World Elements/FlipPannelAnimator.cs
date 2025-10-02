using Photon.Deterministic;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class FlipPannelAnimator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private SpriteMask mask;
        //[SerializeField] private int pointsPerTile = 8, splashWidth = 2;
        //[SerializeField] private float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, animationSpeed = 1f, minimumSplashStrength = 2f;
        [SerializeField] private SpriteRenderer boxRenderer, arrowRenderer;
        [SerializeField] private QuantumEntityView entity;

        //---Private Variables
        //private Texture2D heightTex;
        //private Color32[] colors;
        //private float[] pointHeights, pointVelocities;
        //private float animTimer;
        //private int totalPoints;
        //private bool initialized;

        private float heightTiles;
        private int widthTiles;

        public void OnValidate() {
            ValidationUtility.SafeOnValidate(() => {
                if (!this) {
                    return;
                }
                QPrototypeLiquid liquid = GetComponent<QPrototypeLiquid>();
                if (liquid) {
                    Initialize(liquid.Prototype.WidthTiles, liquid.Prototype.HeightTiles);
                }
            });
        }

        public void Start() {
            if (!entity) {
                Initialize(Mathf.RoundToInt(boxRenderer.size.x * 2), Mathf.RoundToInt(boxRenderer.size.y * 2));
            } else {
                //QuantumEvent.Subscribe<EventLiquidSplashed>(this, OnLiquidSplashed, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true, onlyIfEntityViewBound: true);
            }
        }

        public void Initialize(QuantumGame game) {
            var liquid = game.Frames.Predicted.Unsafe.GetPointer<Liquid>(entity.EntityRef);
            Initialize(liquid->WidthTiles, liquid->HeightTiles);
        }

        public void Initialize(int width, FP height) {
            widthTiles = width;
            heightTiles = height.AsFloat;

            //totalPoints = widthTiles * pointsPerTile;
            //pointHeights = new float[totalPoints];
            //pointVelocities = new float[totalPoints];
            //heightTex = new Texture2D(totalPoints, 1, TextureFormat.RGBA32, false);

            boxRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f);
            arrowRenderer.size = new(Mathf.FloorToInt(widthTiles * 0.5f), (heightTiles * 0.5f) + 2);
            if (mask) {
                mask.transform.localScale = new(widthTiles * (mask.sprite.pixelsPerUnit / 32f), heightTiles * (mask.sprite.pixelsPerUnit / 32f), 1f);
            }
        }
    }
}