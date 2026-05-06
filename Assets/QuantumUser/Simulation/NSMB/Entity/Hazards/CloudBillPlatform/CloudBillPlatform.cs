using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe partial struct CloudBillPlatform {

        public void UpdateCollision(VersusStageData stage, FPVector2 Position, FP Offset, Shape2D* shape, int count, QList<QBoolean> list) {
            FP middle = stage.StageWorldMin.X + stage.TileDimensions.X * FP._0_25;
            bool rightHalf = Position.X > middle;
            if (Position.X < stage.StageWorldMin.X || Position.X > stage.StageWorldMin.X + (stage.TileDimensions.X * FP._0_50)) {
                rightHalf = !rightHalf;
            }

            var StageOffset = stage.TileDimensions.X * (rightHalf ? -FP._0_50 : FP._0_50);
            var listCount = list.Count;
            for (int i = 0; i < count; i++) {
                int Reali = i >= listCount ? i - listCount : i;
                var offset = (Reali * Offset) + (i >= listCount ? StageOffset : 0);
                shape[i].Centroid = new FPVector2(offset, list[Reali].Value == 0 ? -945 : Constants._0_365);
            }
        }
    }
}