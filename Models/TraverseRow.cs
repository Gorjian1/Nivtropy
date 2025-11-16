using System;

namespace Nivtropy.Models
{
    public class TraverseRow
    {
        public string LineName { get; set; } = "?";
        public int Index { get; set; }            // № станции внутри линии
        public string? BackCode { get; set; }     // код "задней" точки/рейки (из StationCode)
        public string? ForeCode { get; set; }     // код "передней" точки/рейки

        /// <summary>
        /// Ссылка на сводку хода (для доступа к длинам и прочей информации)
        /// </summary>
        public LineSummary? LineSummary { get; set; }

        public double? Rb_m { get; set; }
        public double? Rf_m { get; set; }
        public double? DeltaH => (Rb_m.HasValue && Rf_m.HasValue) ? Rb_m - Rf_m : null;

        public double? HdBack_m { get; set; }
        public double? HdFore_m { get; set; }

        /// <summary>
        /// Длина станции (сумма визирных лучей)
        /// </summary>
        public double? StationLength_m => (HdBack_m.HasValue && HdFore_m.HasValue)
            ? HdBack_m.Value + HdFore_m.Value : null;

        /// <summary>
        /// Разность плеч (неравенство плеч) - относительное значение с учетом знака
        /// </summary>
        public double? ArmDifference_m => (HdBack_m.HasValue && HdFore_m.HasValue)
            ? HdBack_m.Value - HdFore_m.Value : null;

        /// <summary>
        /// Устаревшее свойство для обратной совместимости
        /// </summary>
        [Obsolete("Используйте ArmDifference_m")]
        public double? HdImbalance_m => ArmDifference_m;

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
        /// Флаг превышения допуска разности плеч на станции
        /// </summary>
        public bool IsArmDifferenceExceeded { get; set; }

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

        /// <summary>
        /// Отображение кода точки: для виртуальных станций показывает BackCode, для обычных - ForeCode
        /// </summary>
        public string PointCode => string.IsNullOrWhiteSpace(ForeCode) ? (BackCode ?? "—") : ForeCode;

        /// <summary>
        /// Проверка, является ли это виртуальной станцией (только BackCode, без ForeCode и измерений)
        /// </summary>
        public bool IsVirtualStation => string.IsNullOrWhiteSpace(ForeCode) && !DeltaH.HasValue;

        /// <summary>
        /// Отображение высоты для таблицы: для виртуальных станций - BackHeight, для обычных - ForeHeight
        /// </summary>
        public string HeightDisplay
        {
            get
            {
                if (IsVirtualStation)
                {
                    return BackHeight.HasValue ? $"{BackHeight.Value:F4}" : "—";
                }
                else
                {
                    return ForeHeight.HasValue ? $"{ForeHeight.Value:F4}" : "—";
                }
            }
        }
    }
}
