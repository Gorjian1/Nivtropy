namespace Nivtropy.Models
{
    public class TraverseMeasurementRow
    {
        public TraverseMeasurementRow(int index,
                                       int? sequence,
                                       int? shotIndex,
                                       string point,
                                       string mode,
                                       string normalizedClass,
                                       double? backSight,
                                       double? foreSight,
                                       double? deltaH,
                                       double? distance,
                                       double? cumulative)
        {
            Index = index;
            Sequence = sequence;
            ShotIndex = shotIndex;
            Point = point;
            Mode = mode;
            NormalizedClass = normalizedClass;
            BackSight = backSight;
            ForeSight = foreSight;
            DeltaH = deltaH;
            Distance = distance;
            CumulativeDelta = cumulative;
        }

        public int Index { get; }
        public int? Sequence { get; }
        public int? ShotIndex { get; }
        public string Point { get; }
        public string Mode { get; }
        public string NormalizedClass { get; }
        public double? BackSight { get; }
        public double? ForeSight { get; }
        public double? DeltaH { get; }
        public double? Distance { get; }
        public double? CumulativeDelta { get; }
    }
}
