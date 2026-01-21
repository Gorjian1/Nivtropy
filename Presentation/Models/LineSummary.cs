using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Constants;

namespace Nivtropy.Presentation.Models
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
            int knownPointsCount = 0,
            string? systemId = null,
            bool isActive = true,
            string? originalLineNumber = null)
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
            KnownPointsCount = knownPointsCount;
            SystemId = systemId;
            IsActive = isActive;
            OriginalLineNumber = originalLineNumber;
            Closures = Array.Empty<double>();
            SharedPointCodes = Array.Empty<string>();
        }

        public int Index { get; }

        /// <summary>
        /// Оригинальный номер хода из файла (из Start-Line, например "BF 3" -> "3")
        /// </summary>
        public string? OriginalLineNumber { get; }
        public string? StartTarget { get; }
        public string? StartStation { get; }
        public string? EndTarget { get; }
        public string? EndStation { get; }
        public int RecordCount { get; }
        public double? DeltaHSum { get; }

        /// <summary>
        /// Общая длина хода (назад) в метрах
        /// </summary>
        public double? TotalDistanceBack { get; set; }

        /// <summary>
        /// Общая длина хода (вперёд) в метрах
        /// </summary>
        public double? TotalDistanceFore { get; set; }

        /// <summary>
        /// Накопление разности плеч за ход (относительное значение с учетом знака) в метрах
        /// </summary>
        public double? ArmDifferenceAccumulation { get; set; }

        /// <summary>
        /// Общая длина хода: сумма длин назад и вперёд (в метрах)
        /// </summary>
        public double? TotalAverageLength => TotalDistanceBack.HasValue && TotalDistanceFore.HasValue
            ? TotalDistanceBack.Value + TotalDistanceFore.Value
            : null;

        /// <summary>
        /// ID системы, к которой принадлежит ход
        /// </summary>
        public string? SystemId { get; set; }

        /// <summary>
        /// Активен ли ход (участвует в расчётах и экспорте)
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Количество известных точек в этом ходе
        /// </summary>
        public int KnownPointsCount { get; set; }

        /// <summary>
        /// Флаг превышения допуска накопления разности плеч
        /// </summary>
        public bool IsArmDifferenceAccumulationExceeded { get; set; }

        /// <summary>
        /// Включить локальное уравнивание по секциям (когда в ходе несколько известных точек)
        /// </summary>
        public bool UseLocalAdjustment { get; set; }

        /// <summary>
        /// Нужна ли кнопка локального уравнивания (больше 1 известной точки)
        /// </summary>
        public bool NeedsLocalAdjustment => KnownPointsCount > 1;

        /// <summary>
        /// Общие точки с другими ходами
        /// </summary>
        public IReadOnlyList<string> SharedPointCodes { get; private set; }

        /// <summary>
        /// Есть ли общие точки, которые можно синхронизировать
        /// </summary>
        public bool HasSharedPoints => SharedPointCodes.Count > 0;

        /// <summary>
        /// Человекочитаемое представление общих точек (через запятую)
        /// </summary>
        public string SharedPointsDisplay => SharedPointCodes.Count > 0
            ? string.Join(", ", SharedPointCodes)
            : "—";

        /// <summary>
        /// Невязки, рассчитанные для хода (по секциям при локальном уравнивании)
        /// </summary>
        public IReadOnlyList<double> Closures { get; private set; }

        /// <summary>
        /// Человекочитаемое представление невязок (через запятую)
        /// </summary>
        public string ClosuresDisplay => Closures.Count > 0
            ? string.Join(", ", Closures.Select(c => c.ToString(DisplayFormats.DeltaH)))
            : DisplayFormats.EmptyValue;

        public void SetClosures(IEnumerable<double> values)
        {
            Closures = values?.ToArray() ?? Array.Empty<double>();
        }

        public void SetSharedPoints(IEnumerable<string> codes)
        {
            SharedPointCodes = codes?.Where(c => !string.IsNullOrWhiteSpace(c)).ToArray() ?? Array.Empty<string>();
        }

        private static string FormatPoint(string? target, string? station)
        {
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();
            if (!string.IsNullOrWhiteSpace(station)) return station.Trim();
            return "—";
        }

        public string StartLabel => FormatPoint(StartTarget, StartStation);
        public string EndLabel => FormatPoint(EndTarget, EndStation);

        public string DisplayName => !string.IsNullOrWhiteSpace(OriginalLineNumber)
            ? $"Ход {OriginalLineNumber}"
            : $"Ход {Index:D2}";
        public string DisplayNameWithPointCount => $"{DisplayName} ({RecordCount})";
        public string RangeDisplay => $"{StartLabel} → {EndLabel}";
        public string Header => $"{DisplayName}: {RangeDisplay}";

        public string CombinedStats
        {
            get
            {
                var delta = DeltaHSum.HasValue
                    ? string.Format(System.Globalization.CultureInfo.InvariantCulture, $"ΣΔh = {{0:{DisplayFormats.DeltaH}}} м", DeltaHSum.Value)
                    : $"ΣΔh = {DisplayFormats.EmptyValue}";
                return $"Отсчётов: {RecordCount}, {delta}";
            }
        }

        public string Tooltip => $"{Header}\n{CombinedStats}";
    }
}
