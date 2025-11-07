namespace Nivtropy.Models
{
    public class DesignRow
    {
        public int Index { get; set; }
        public string Station { get; set; } = string.Empty;
        public double? OriginalDeltaH { get; set; }
        public double Correction { get; set; }
        public double? AdjustedDeltaH { get; set; }
        public double DesignedHeight { get; set; }
    }
}
