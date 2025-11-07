using System.Globalization;

namespace Nivtropy.Models
{
    public class TraverseDesignStep
    {
        public TraverseDesignStep(int index,
                                  string point,
                                  string mode,
                                  double originalDelta,
                                  double adjustment,
                                  double projectedDelta,
                                  double projectedElevation)
        {
            Index = index;
            Point = point;
            Mode = mode;
            OriginalDelta = originalDelta;
            Adjustment = adjustment;
            ProjectedDelta = projectedDelta;
            ProjectedElevation = projectedElevation;
        }

        public int Index { get; }
        public string Point { get; }
        public string Mode { get; }
        public double OriginalDelta { get; }
        public double Adjustment { get; }
        public double ProjectedDelta { get; }
        public double ProjectedElevation { get; }

        public string OriginalDeltaDisplay => OriginalDelta.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture);
        public string AdjustmentDisplay => Adjustment.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture);
        public string ProjectedDeltaDisplay => ProjectedDelta.ToString("+0.0000;-0.0000;0.0000", CultureInfo.InvariantCulture);
        public string ProjectedElevationDisplay => ProjectedElevation.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
