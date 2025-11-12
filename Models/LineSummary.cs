using System;

namespace Nivtropy.Models
{
    public class LineSummary
    {
        public LineSummary(
            int index,
            string? startTarget,
            string? startStation,
            string? endTarget,
            string? endStation,
            int recordCount,
            double? deltaHSum,
            double? totalDistanceBack = null,
            double? totalDistanceFore = null,
            double? armDifferenceAccumulation = null,
            int segmentIndex = 0)
        {
            Index = index;
            StartTarget = startTarget;
            StartStation = startStation;
            EndTarget = endTarget;
            EndStation = endStation;
            RecordCount = recordCount;
            DeltaHSum = deltaHSum;
            TotalDistanceBack = totalDistanceBack;
            TotalDistanceFore = totalDistanceFore;
            ArmDifferenceAccumulation = armDifferenceAccumulation;
            SegmentIndex = segmentIndex;
        }

        public int Index { get; }

        /// <summary>
        /// Индекс сегмента хода (0 = основной, 1+ = продолжения после Cont-Line)
        /// </summary>
        public int SegmentIndex { get; }

        public string? StartTarget { get; }
        public string? StartStation { get; }
        public string? EndTarget { get; }
        public string? EndStation { get; }
        public int RecordCount { get; }
        public double? DeltaHSum { get; }

        /// <summary>
        /// Общая длина хода (назад) в метрах
        /// </summary>
        public double? TotalDistanceBack { get; }

        /// <summary>
        /// Общая длина хода (вперёд) в метрах
        /// </summary>
        public double? TotalDistanceFore { get; }

        /// <summary>
        /// Накопление разности плеч за ход (сумма модулей) в метрах
        /// </summary>
        public double? ArmDifferenceAccumulation { get; }

        /// <summary>
        /// Общая длина хода: сумма длин назад и вперёд (в метрах)
        /// </summary>
        public double? TotalAverageLength => TotalDistanceBack.HasValue && TotalDistanceFore.HasValue
            ? TotalDistanceBack.Value + TotalDistanceFore.Value
            : null;

        /// <summary>
        /// Флаг превышения допуска накопления разности плеч
        /// </summary>
        public bool IsArmDifferenceAccumulationExceeded { get; set; }

        private static string FormatPoint(string? target, string? station)
        {
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();
            if (!string.IsNullOrWhiteSpace(station)) return station.Trim();
            return "—";
        }

        public string StartLabel => FormatPoint(StartTarget, StartStation);
        public string EndLabel => FormatPoint(EndTarget, EndStation);

        public string DisplayName
        {
            get
            {
                var baseName = $"Ход {Index:D2}";
                // Добавляем звездочки для продолжений (1 звездочка = 1 продолжение)
                if (SegmentIndex > 0)
                {
                    return baseName + new string('*', SegmentIndex);
                }
                return baseName;
            }
        }

        public string RangeDisplay => $"{StartLabel} → {EndLabel}";
        public string Header => $"{DisplayName}: {RangeDisplay}";

        public string CombinedStats
        {
            get
            {
                var delta = DeltaHSum.HasValue
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "ΣΔh = {0:+0.0000;-0.0000;0.0000} м", DeltaHSum.Value)
                    : "ΣΔh = —";
                return $"Отсчётов: {RecordCount}, {delta}";
            }
        }

        public string Tooltip => $"{Header}\n{CombinedStats}";
    }
}
