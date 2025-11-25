using System;

namespace Nivtropy.Models
{
    public enum JournalRowType
    {
        BackPoint,    // Задняя точка (первая строка станции)
        Elevation,    // Превышение (средняя строка станции)
        ForePoint     // Передняя точка (третья строка станции)
    }

    /// <summary>
    /// Представляет одну строку в журнальном отображении нивелирования.
    /// Каждая станция состоит из 3 строк: BackPoint -> Elevation -> ForePoint
    /// </summary>
    public class JournalRow
    {
        /// <summary>
        /// Тип строки (точка или превышение)
        /// </summary>
        public JournalRowType RowType { get; set; }

        /// <summary>
        /// Номер станции (отображается только для первой строки станции)
        /// </summary>
        public int StationNumber { get; set; }

        /// <summary>
        /// Название хода для группировки
        /// </summary>
        public string LineName { get; set; }

        /// <summary>
        /// Ссылка на сводку хода (для отображения данных в заголовке группы)
        /// </summary>
        public LineSummary? LineSummary { get; set; }

        // === Данные для точек (BackPoint, ForePoint) ===

        /// <summary>
        /// Код точки
        /// </summary>
        public string PointCode { get; set; }

        /// <summary>
        /// Высота без поправки (Z0)
        /// </summary>
        public double? Z0 { get; set; }

        /// <summary>
        /// Высота с поправкой (Z)
        /// </summary>
        public double? Z { get; set; }

        // === Данные для превышения (Elevation) ===

        /// <summary>
        /// Длина станции (HDback + HDfore), м
        /// </summary>
        public double? StationLength { get; set; }

        /// <summary>
        /// Превышение (Rb - Rf), м
        /// </summary>
        public double? DeltaH { get; set; }

        /// <summary>
        /// Поправка за невязку, м
        /// </summary>
        public double? Correction { get; set; }

        /// <summary>
        /// Исправленное превышение (Δh + поправка), м
        /// </summary>
        public double? AdjustedDeltaH { get; set; }

        // === Свойства для отображения ===

        /// <summary>
        /// Отображаемый номер станции (пустая строка для Elevation и ForePoint)
        /// </summary>
        public string StationNumberDisplay => RowType == JournalRowType.BackPoint ? StationNumber.ToString() : string.Empty;

        /// <summary>
        /// Отображаемая длина станции
        /// </summary>
        public string StationLengthDisplay => StationLength.HasValue ? StationLength.Value.ToString("0.00") : string.Empty;

        /// <summary>
        /// Отображаемое превышение
        /// </summary>
        public string DeltaHDisplay => DeltaH.HasValue ? DeltaH.Value.ToString("+0.0000;-0.0000;0.0000") : string.Empty;

        /// <summary>
        /// Отображаемая поправка
        /// </summary>
        public string CorrectionDisplay => Correction.HasValue ? Correction.Value.ToString("+0.0000;-0.0000;0.0000") : string.Empty;

        /// <summary>
        /// Отображаемое исправленное превышение
        /// </summary>
        public string AdjustedDeltaHDisplay => AdjustedDeltaH.HasValue ? AdjustedDeltaH.Value.ToString("+0.0000;-0.0000;0.0000") : string.Empty;

        /// <summary>
        /// Отображаемая высота Z0
        /// </summary>
        public string Z0Display => Z0.HasValue ? Z0.Value.ToString("0.0000") : string.Empty;

        /// <summary>
        /// Отображаемая высота Z
        /// </summary>
        public string ZDisplay => Z.HasValue ? Z.Value.ToString("0.0000") : string.Empty;
    }
}
