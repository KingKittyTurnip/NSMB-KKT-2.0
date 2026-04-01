using NSMB.UI.Game;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.Player {
    public class VoidwallAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private Sprite[] VoidwallSprites, TeamwallSprites;
        [SerializeField] private Sprite VoidWallInjured;

        //---Private Variables
        private bool CurrentlyTeamed;
        private float ExpandTimer = 0;
        private float SpriteTimer = 0;
        private bool Playsound;

        public void OnValidate() {
            this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        }

        private float CameraAreaCoverage = 6.75f;
        public override unsafe void OnActivate(Frame f) {
            //RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        //todo: animate
        //make work with teams
        //make freecam ignore the camera manipulation stuff
        //make it overlay the looping camera

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            var voidwall = f.Unsafe.GetPointer<Voidwall>(EntityRef);
            var Collider = f.Unsafe.GetPointer<PhysicsCollider2D>(EntityRef);
            UnwrapWorldLocations(f.FindAsset<VersusStageData>(f.Map.UserAsset), transform.position, Camera.main.transform.position, out var OurPos, out var CameraPos);

            //"Unable to see on the other side of it" calcs
            float distancefromcameraedge = (Mathf.Clamp(CameraAreaCoverage - Mathf.Abs(CameraPos.x - OurPos.x), 0, CameraAreaCoverage) * 2) + 0.5f;
            if (ExpandTimer > 0 && voidwall->DamageCooldown > 0 && !Playsound) {
                sfx.Play();
                Playsound = true;
            } else if (ExpandTimer == 0) {
                Playsound = false;
            }
            ExpandTimer = Mathf.Clamp01(ExpandTimer + ((voidwall->DamageCooldown > 0 ? -1 : 1) * Time.deltaTime * 10));

            //Set The Side We Are On
            sRenderer.flipX = !(OurPos.x < CameraPos.x || ExpandTimer == 0);
            sRenderer.transform.localPosition = new Vector3(OurPos.x < CameraPos.x ? 0.22f : -0.22f, 0, -9f);

            //Set size
            float Y = Collider->Shape.Box.Extents.Y.AsFloat;
            float Xsize = Y < 5 ? 0.87f : 0.87f + (Mathf.Min(Y - 5, 1) * distancefromcameraedge * ExpandTimer);
            sRenderer.size = new Vector2(Xsize, Y * 4);

            //Set Sprite
            float remainingDamage = voidwall->DamageCooldown.AsFloat;
            bool CurrentlyDamaged = !(remainingDamage > 0 && (f.Number * f.DeltaTime.AsFloat) * 3 % 0.2f < 0.1f);
            sRenderer.sprite = CurrentlyDamaged ? (CurrentlyTeamed ? TeamwallSprites : VoidwallSprites)[(int)Mathf.Floor(SpriteTimer >= VoidwallSprites.Length - (Time.deltaTime*5) ? SpriteTimer = 0 : SpriteTimer += Time.deltaTime*5)] : VoidWallInjured;

            //Set Color
            sRenderer.color = (remainingDamage == 0 || !CurrentlyDamaged) ? Color.white : new Color(1, 1, 1, 0.2f);
        }

        public override void OnDeactivate() {
            //RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera) {
            try {
                if (sRenderer) {
                    //CurrentlyTeamed = WeAreOnSameteam;
                    //sRenderer.color = IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor; //make transparent if on teams
                }
            } catch {
                // Debug.LogWarning("The bug happened");
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
    }
}
