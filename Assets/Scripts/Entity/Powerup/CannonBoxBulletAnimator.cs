using Quantum;
using System.Collections.Generic;
using UnityEngine;

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
                i = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(proj->Owner)->CharacterAsset).Order+1;
            }
            materialBlock.SetInt(ParamBoxType, i);
            foreach (Renderer r in renderers) {
                r.SetPropertyBlock(materialBlock);
            }
        }

    }
}
