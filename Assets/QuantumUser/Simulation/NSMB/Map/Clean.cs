using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Clean {
        public bool NearLightBlock(Frame f, FPVector2 pos) {
            FP LightBlockProtectionRange = Constants._3_50;

            var allLightBlocks = f.Filter<ThrowingObject, Transform2D>();
            while (allLightBlocks.NextUnsafe(out EntityRef entity, out ThrowingObject* dis, out Transform2D* disTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, pos, disTransform->Position, out var ourPos, out var theirPos);
                FP e = FPVector2.Distance(ourPos, theirPos);
                if (LightBlockProtectionRange > e) {
                    //near light block
                    return true;
                }
            }
            return false;
        }

        public void AddAmount(byte id, byte Amount) {
            if (TargetChances[id] + Amount > 200) {
                TargetChances[id] = 200;
                return;
            }
            TargetChances[id] += Amount;
        }
    }
}