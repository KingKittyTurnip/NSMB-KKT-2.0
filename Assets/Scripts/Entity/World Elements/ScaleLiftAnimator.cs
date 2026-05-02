using NSMB.Sound;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Player {
    public class ScaleLiftAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private Transform KnobL, KnobR;
        [SerializeField] private SpriteRenderer LineL, LineR, LineM;
        [SerializeField] private Animator LiftL, LiftR;
        [SerializeField] private LoopingSoundPlayer tickSound;

        public void OnValidate() {
            //this.SetIfNull(ref sfx, UnityExtensions.GetComponentType.Children);
        }
        public void Start() {
            QuantumEvent.Subscribe<EventScaleplatformStepped>(this, OnSteped, FilterOutReplayFastForward);
        }

        private float CameraAreaCoverage = 6.75f;
        public override unsafe void OnActivate(Frame f) {
            if (f.Unsafe.TryGetPointer<ScalePlatform>(EntityRef, out var scaleplatform)) {
                //we don't need to update these constantly
                float scaleLength = scaleplatform->Length.AsFloat;
                LineM.size = new Vector2((scaleLength*2) - 0.2f, LineM.size.y);
                LineM.transform.localPosition = new Vector3(-scaleLength + 0.1f, 0, 0);

                KnobL.localPosition = new Vector3(-scaleLength + 0.175f, -0.175f, 0);
                KnobR.localPosition = new Vector3(scaleLength - 0.175f, -0.175f, 0);
            }
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            var scaleplatform = f.Unsafe.GetPointer<ScalePlatform>(EntityRef);

            float scalelength = scaleplatform->Length.AsFloat;
            float scaleheight = scaleplatform->Height.AsFloat;
            float scaleoffset = scaleplatform->Offset.AsFloat;
            float Bonus = 0;
            if (scaleplatform->Timer > scaleplatform->PlatformbreakTime) {
                Bonus = ((scaleplatform->Timer - scaleplatform->PlatformbreakTime) * f.DeltaTime * 5).AsFloat;
            }

            //Rotate the knobs
            if (scaleplatform->Delay == 5 && scaleplatform->Timer == 0) //damn magic number
                KnobL.rotation = KnobR.rotation = Quaternion.Euler(0, 0, KnobL.rotation.eulerAngles.z + (scaleplatform->Velocity.AsFloat*Time.deltaTime*200));

            //set location of platforms
            LiftL.transform.localPosition = new Vector3(-scalelength, -scaleheight - scaleoffset - Bonus, 0);
            LiftR.transform.localPosition = new Vector3(scalelength, -scaleheight + scaleoffset - Bonus, 0);
            if (Bonus == 0) {
                //only change the lines if we need too
                LineL.transform.localPosition = LiftL.transform.localPosition;
                LineR.transform.localPosition = LiftR.transform.localPosition;
                LineL.size = new Vector2(scaleheight + scaleoffset - 0.25f, LineM.size.y);
                LineR.size = new Vector2(scaleheight - scaleoffset - 0.25f, LineM.size.y);
            }

            if (scaleplatform->WeightOnLift == 0) {
                //stop sound
                tickSound.Stop();
            }
        }

        public void OnSteped(EventScaleplatformStepped e) {
            if (e.Entity != EntityRef) {
                return;
            }
            //play a anim and start the tick sound
            if (!e.IsRightPlatform || e.Broken) {
                LiftL.SetTrigger("Hit");
            }
            if (e.IsRightPlatform || e.Broken) {
                LiftR.SetTrigger("Hit");
            }

            if (!e.Broken) {
                tickSound.Play();
            } else {
                tickSound.Stop();
            }
        }
    }
}
