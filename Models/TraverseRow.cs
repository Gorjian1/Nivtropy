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
        /// Отметка (высота) задней точки
        /// </summary>
        public double? BackHeight { get; set; }

        /// <summary>
        /// Отметка (высота) передней точки
        /// </summary>
        public double? ForeHeight { get; set; }

        /// <summary>
        /// Флаг, что высота задней точки известна (задана вручную)
        /// </summary>
        public bool IsBackHeightKnown { get; set; }

        /// <summary>
        /// Флаг, что высота передней точки известна (задана вручную)
        /// </summary>
        public bool IsForeHeightKnown { get; set; }

        /// <summary>
        /// Поправка в превышение (для распределения невязки)
        /// </summary>
        public double? Correction { get; set; }

        /// <summary>
        /// Исправленное превышение (с учётом поправки)
        /// </summary>
        public double? AdjustedDeltaH => DeltaH.HasValue && Correction.HasValue
            ? DeltaH.Value + Correction.Value
            : DeltaH;

        /// <summary>
        /// Отображение высоты задней точки
        /// </summary>
        public string BackHeightDisplay => BackHeight.HasValue
            ? $"{BackHeight.Value:F4}"
            : "—";

        /// <summary>
        /// Отображение высоты передней точки
        /// </summary>
        public string ForeHeightDisplay => ForeHeight.HasValue
            ? $"{ForeHeight.Value:F4}"
            : "—";
    }
}
