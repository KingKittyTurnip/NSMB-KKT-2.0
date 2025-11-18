using NSMB.Cameras;
using NSMB.Particles;
using NSMB.Quantum;
using NSMB.Sound;
using NSMB.UI.Game;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Photon.Deterministic;
using Quantum;
using Quantum.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using static NSMB.Utilities.QuantumViewUtils;
using Input = Quantum.Input;

namespace NSMB.Entities.Player {
    public class CannonBoxBulletAnimator : QuantumEntityViewComponent {

        private MaterialPropertyBlock materialBlock;
        List<Renderer> renderers = new();
        private static readonly int ParamBoxType = Shader.PropertyToID("BoxType");

        public override unsafe void OnActivate(Frame f) {
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            materialBlock = new();

            if (!f.Exists(EntityRef)) {
                return;
            }
            var proj = f.Unsafe.GetPointer<Projectile>(EntityRef);

            int i = 0;
            if (f.Exists(proj->Owner)) {
                for (i = 0; i < f.SimulationConfig.CharacterDatas.Length; i++) {
                    if (f.SimulationConfig.CharacterDatas[i] == f.Unsafe.GetPointer<MarioPlayer>(proj->Owner)->CharacterAsset) {
                        i++;
                        break;
                    }
                }
            }
            materialBlock.SetInt(ParamBoxType, i);
            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(materialBlock);
            }
        }

    }
}
