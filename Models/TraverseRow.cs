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

        public string Station => string.IsNullOrWhiteSpace(BackCode) && string.IsNullOrWhiteSpace(ForeCode)
            ? LineName
            : $"{BackCode ?? "?"} → {ForeCode ?? "?"}";

        /// <summary>
        /// Известная высота задней точки (если установлена)
        /// </summary>
        public double? BackKnownHeight { get; set; }

        /// <summary>
        /// Известная высота передней точки (если установлена)
        /// </summary>
        public double? ForeKnownHeight { get; set; }

        /// <summary>
        /// Рассчитанная высота задней точки
        /// </summary>
        public double? BackCalculatedHeight { get; set; }

        /// <summary>
        /// Рассчитанная высота передней точки
        /// </summary>
        public double? ForeCalculatedHeight { get; set; }

        /// <summary>
        /// Отображаемая высота задней точки (известная или рассчитанная)
        /// </summary>
        public string BackHeightDisplay => BackKnownHeight.HasValue
            ? $"{BackKnownHeight.Value:F4} (задано)"
            : BackCalculatedHeight.HasValue
                ? $"{BackCalculatedHeight.Value:F4}"
                : "—";

        /// <summary>
        /// Отображаемая высота передней точки (известная или рассчитанная)
        /// </summary>
        public string ForeHeightDisplay => ForeKnownHeight.HasValue
            ? $"{ForeKnownHeight.Value:F4} (задано)"
            : ForeCalculatedHeight.HasValue
                ? $"{ForeCalculatedHeight.Value:F4}"
                : "—";
    }
}
