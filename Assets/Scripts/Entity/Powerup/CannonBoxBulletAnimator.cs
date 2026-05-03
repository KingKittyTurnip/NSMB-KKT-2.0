using Quantum;
using System.Collections.Generic;
using UnityEngine;

namespace NSMB.Entities.Player {
    public class CannonBoxBulletAnimator : QuantumEntityViewComponent {

        private MaterialPropertyBlock materialBlock;
        List<Renderer> renderers = new();
        [SerializeField] private Texture BaseTexture, InvalidTexture;

        public override unsafe void OnActivate(Frame f) {
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            materialBlock = new();

            if (!f.Exists(EntityRef)) {
                return;
            }

            if (f.Unsafe.TryGetPointer<Projectile>(EntityRef, out var proj)) {
                //Set Color
                var i = BaseTexture;
                if (f.Exists(proj->Owner)) {
                    i = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(proj->Owner)->CharacterAsset).CannonboxTexture;
                    if (i == null) {
                        i = InvalidTexture;
                    }
                }
                materialBlock.SetTexture("Texture", i);
                foreach (Renderer r in renderers) {
                    r.SetPropertyBlock(materialBlock);
                }
            }
        }

    }
}
