namespace Nivtropy.Models
{
    public class DesignRow
    {
        public int Index { get; set; }
        public string Station { get; set; } = string.Empty;

        /// <summary>
        /// Код точки (BackCode или ForeCode)
        /// </summary>
        public string? PointCode { get; set; }

        /// <summary>
        /// Средняя горизонтальная длина хода (среднее между задним и передним измерениями)
        /// </summary>
        public double? Distance_m { get; set; }

        /// <summary>
        /// Исходное превышение (до уравнивания)
        /// </summary>
        public double? OriginalDeltaH { get; set; }

        /// <summary>
        /// Поправка для данного хода (пропорционально длине)
        /// </summary>
        public double Correction { get; set; }

        /// <summary>
        /// Уравненное превышение (после распределения невязки)
        /// </summary>
        public double? AdjustedDeltaH { get; set; }

        /// <summary>
        /// Проектная высота точки
        /// </summary>
        public double DesignedHeight { get; set; }

        /// <summary>
        /// Известная высота точки (если установлена вручную)
        /// </summary>
        public double? KnownHeight { get; set; }

        /// <summary>
        /// Флаг, что высота точки известна (задана вручную)
        /// </summary>
        public bool IsKnownHeight => KnownHeight.HasValue;

        /// <summary>
        /// Отображаемая высота (известная или рассчитанная)
        /// </summary>
        public string HeightDisplay => KnownHeight.HasValue
            ? $"{KnownHeight.Value:F4} (задано)"
            : $"{DesignedHeight:F4}";
    }
}
