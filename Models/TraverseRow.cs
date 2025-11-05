using System;

namespace Nivtropy.Models
{
    public class TraverseRow
    {
        public string LineName { get; set; } = "?";
        public int Index { get; set; }            // № станции внутри линии
        public string? BackCode { get; set; }     // код "задней" точки/рейки (из StationCode)
        public string? ForeCode { get; set; }     // код "передней" точки/рейки

        public double? Rb_m { get; set; }
        public double? Rf_m { get; set; }
        public double? DeltaH => (Rb_m.HasValue && Rf_m.HasValue) ? Rb_m - Rf_m : null;

        public double? HdBack_m { get; set; }
        public double? HdFore_m { get; set; }
        public double? HdImbalance_m => (HdBack_m.HasValue && HdFore_m.HasValue)
            ? Math.Abs(HdBack_m.Value - HdFore_m.Value) : null;
    }
}
