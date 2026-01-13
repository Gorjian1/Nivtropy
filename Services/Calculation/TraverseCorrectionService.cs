using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;

namespace Nivtropy.Services.Calculation
{
    /// <summary>
    /// Входные данные станции для расчёта поправок
    /// </summary>
    public class StationCorrectionInput
    {
        public int Index { get; init; }
        public string? BackCode { get; init; }
        public string? ForeCode { get; init; }
        public double? DeltaH { get; init; }
        public double? HdBack { get; init; }
        public double? HdFore { get; init; }
    }

    /// <summary>
    /// Результат расчёта поправок для одной станции
    /// </summary>
    public class StationCorrectionResult
    {
        public int Index { get; init; }
        public double? Correction { get; init; }
        public double? BaselineCorrection { get; init; }
        public CorrectionDisplayMode Mode { get; init; }
    }

    /// <summary>
    /// Результат расчёта поправок для хода
    /// </summary>
    public record CorrectionCalculationResult
    {
        public List<StationCorrectionResult> Corrections { get; } = new();
        public List<double> Closures { get; } = new();
        public TraverseClosureMode ClosureMode { get; init; }
        public int DistinctAnchorCount { get; init; }
    }

    /// <summary>
    /// Интерфейс сервиса расчёта поправок
    /// </summary>
    public interface ITraverseCorrectionService
    {
        /// <summary>
        /// Рассчитывает поправки для хода
        /// </summary>
        CorrectionCalculationResult CalculateCorrections(
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor,
            double methodOrientationSign,
            bool useLocalAdjustment);
    }

    /// <summary>
    /// Сервис расчёта поправок для нивелирного хода
    /// </summary>
    public class TraverseCorrectionService : ITraverseCorrectionService
    {
        private const double CorrectionRoundingStep = 0.0001;

        public CorrectionCalculationResult CalculateCorrections(
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor,
            double methodOrientationSign,
            bool useLocalAdjustment)
        {
            var result = new CorrectionCalculationResult();

            if (stations.Count == 0)
                return result;

            var anchorPoints = CollectAnchorPoints(stations, isAnchor);
            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var closureMode = DetermineClosureMode(stations, isAnchor, anchorPoints, useLocalAdjustment);

            var corrections = new Dictionary<int, (double? correction, double? baseline, CorrectionDisplayMode mode)>();

            // Инициализируем все станции
            foreach (var station in stations)
            {
                corrections[station.Index] = (null, null, CorrectionDisplayMode.None);
            }

            switch (closureMode)
            {
                case TraverseClosureMode.Open:
                    break;

                case TraverseClosureMode.Simple:
                    {
                        var closure = CalculateCorrectionsForSection(
                            stations.ToList(),
                            methodOrientationSign,
                            (idx, value) =>
                            {
                                corrections[idx] = (value, value, CorrectionDisplayMode.Single);
                            });

                        if (closure.HasValue)
                            result.Closures.Add(closure.Value);
                        break;
                    }

                case TraverseClosureMode.Local:
                    {
                        // Базовая поправка для наглядности
                        CalculateCorrectionsForSection(
                            stations.ToList(),
                            methodOrientationSign,
                            (idx, value) =>
                            {
                                var current = corrections[idx];
                                corrections[idx] = (current.correction, value, current.mode);
                            });

                        // Фактическое локальное уравнивание по секциям
                        CalculateCorrectionsWithSections(
                            stations.ToList(),
                            anchorPoints,
                            methodOrientationSign,
                            result.Closures,
                            (idx, value) =>
                            {
                                var current = corrections[idx];
                                corrections[idx] = (value, current.baseline, CorrectionDisplayMode.Local);
                            });
                        break;
                    }
            }

            // Если невязки не были рассчитаны, вычисляем ориентированную сумму
            if (result.Closures.Count == 0)
            {
                var orientedClosure = stations
                    .Where(s => s.DeltaH.HasValue)
                    .Sum(s => s.DeltaH!.Value * methodOrientationSign);
                result.Closures.Add(orientedClosure);
            }

            // Формируем результат
            foreach (var station in stations)
            {
                var (correction, baseline, mode) = corrections[station.Index];
                result.Corrections.Add(new StationCorrectionResult
                {
                    Index = station.Index,
                    Correction = correction,
                    BaselineCorrection = baseline,
                    Mode = mode
                });
            }

            return result with { ClosureMode = closureMode, DistinctAnchorCount = distinctAnchorCount };
        }

        private static List<(int Index, string? Code)> CollectAnchorPoints(
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor)
        {
            var knownPoints = new List<(int Index, string? Code)>();

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];

                if (!string.IsNullOrWhiteSpace(station.BackCode) && isAnchor(station.BackCode))
                {
                    if (knownPoints.All(p => p.Index != i))
                        knownPoints.Add((i, station.BackCode));
                }

                if (!string.IsNullOrWhiteSpace(station.ForeCode) && isAnchor(station.ForeCode))
                {
                    var anchorIndex = Math.Min(i + 1, stations.Count);
                    if (knownPoints.All(p => p.Index != anchorIndex))
                        knownPoints.Add((anchorIndex, station.ForeCode));
                }
            }

            return knownPoints.OrderBy(p => p.Index).ToList();
        }

        private static TraverseClosureMode DetermineClosureMode(
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor,
            List<(int Index, string? Code)> anchorPoints,
            bool useLocalAdjustment)
        {
            var startCode = stations.FirstOrDefault()?.BackCode ?? stations.FirstOrDefault()?.ForeCode;
            var endCode = stations.LastOrDefault()?.ForeCode ?? stations.LastOrDefault()?.BackCode;

            bool startKnown = !string.IsNullOrWhiteSpace(startCode) && isAnchor(startCode);
            bool endKnown = !string.IsNullOrWhiteSpace(endCode) && isAnchor(endCode);

            bool closesByLoop = !string.IsNullOrWhiteSpace(startCode)
                && !string.IsNullOrWhiteSpace(endCode)
                && string.Equals(startCode, endCode, StringComparison.OrdinalIgnoreCase);

            bool isClosed = closesByLoop || (startKnown && endKnown);

            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (!isClosed)
            {
                if (distinctAnchorCount >= 2)
                    return TraverseClosureMode.Local;
                return TraverseClosureMode.Open;
            }

            if (distinctAnchorCount > 1 || useLocalAdjustment)
                return TraverseClosureMode.Local;

            return TraverseClosureMode.Simple;
        }

        private static void CalculateCorrectionsWithSections(
            List<StationCorrectionInput> stations,
            List<(int Index, string? Code)> knownPoints,
            double methodSign,
            List<double> closures,
            Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0)
                return;

            if (knownPoints.Count < 2)
            {
                var closure = CalculateCorrectionsForSection(stations, methodSign, applyCorrection);
                if (closure.HasValue)
                    closures.Add(closure.Value);
                return;
            }

            for (int i = 0; i < knownPoints.Count - 1; i++)
            {
                int startIdx = knownPoints[i].Index;
                int endIdx = knownPoints[i + 1].Index;

                var sectionStations = stations.Skip(startIdx).Take(endIdx - startIdx).ToList();

                if (sectionStations.Count > 0)
                {
                    var closure = CalculateCorrectionsForSection(sectionStations, methodSign, applyCorrection);
                    if (closure.HasValue)
                        closures.Add(closure.Value);
                }
            }

            var firstAnchor = knownPoints.First();
            var lastAnchor = knownPoints.Last();

            if (!string.IsNullOrWhiteSpace(firstAnchor.Code)
                && string.Equals(firstAnchor.Code, lastAnchor.Code, StringComparison.OrdinalIgnoreCase)
                && lastAnchor.Index < stations.Count)
            {
                var wrapSection = stations.Skip(lastAnchor.Index).ToList();
                if (wrapSection.Count > 0)
                {
                    var closure = CalculateCorrectionsForSection(wrapSection, methodSign, applyCorrection);
                    if (closure.HasValue)
                        closures.Add(closure.Value);
                }
            }
        }

        private static double? CalculateCorrectionsForSection(
            List<StationCorrectionInput> stations,
            double methodSign,
            Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0)
                return null;

            var sectionClosure = stations
                .Where(s => s.DeltaH.HasValue)
                .Sum(s => s.DeltaH!.Value * methodSign);

            var adjustableStations = stations.Where(s => s.DeltaH.HasValue).ToList();
            if (adjustableStations.Count == 0)
                return sectionClosure;

            double totalDistance = 0;
            foreach (var station in stations)
            {
                var avgDistance = ((station.HdBack ?? 0) + (station.HdFore ?? 0)) / 2.0;
                totalDistance += avgDistance;
            }

            var allocations = new List<(int Index, double Raw, double Rounded)>();

            if (totalDistance <= 0)
            {
                var correctionPerStation = -sectionClosure / adjustableStations.Count;
                foreach (var station in adjustableStations)
                {
                    allocations.Add((station.Index, correctionPerStation, correctionPerStation));
                }
            }
            else
            {
                var correctionFactor = -sectionClosure / totalDistance;
                foreach (var station in adjustableStations)
                {
                    var avgDistance = ((station.HdBack ?? 0) + (station.HdFore ?? 0)) / 2.0;
                    var raw = correctionFactor * avgDistance;
                    allocations.Add((station.Index, raw, raw));
                }
            }

            ApplyRoundedCorrections(allocations, applyCorrection);
            return sectionClosure;
        }

        private static void ApplyRoundedCorrections(
            List<(int Index, double Raw, double Rounded)> allocations,
            Action<int, double>? applyCorrection)
        {
            if (allocations.Count == 0 || applyCorrection == null)
                return;

            // Округляем
            var rounded = allocations
                .Select(a => (a.Index, a.Raw, Rounded: Math.Round(a.Raw, 4, MidpointRounding.AwayFromZero)))
                .ToList();

            var targetTotal = rounded.Sum(a => a.Raw);
            var roundedTotal = rounded.Sum(a => a.Rounded);
            var remaining = Math.Round(targetTotal - roundedTotal, 4, MidpointRounding.AwayFromZero);

            var steps = (int)Math.Round(remaining / CorrectionRoundingStep, MidpointRounding.AwayFromZero);

            // Корректируем округление
            while (steps != 0)
            {
                var positive = steps > 0;
                var candidateIdx = rounded
                    .Select((r, i) => (r, i))
                    .OrderByDescending(x => positive ? (x.r.Raw - x.r.Rounded) : (x.r.Rounded - x.r.Raw))
                    .ThenByDescending(x => Math.Abs(x.r.Raw))
                    .Select(x => x.i)
                    .FirstOrDefault();

                var item = rounded[candidateIdx];
                rounded[candidateIdx] = (item.Index, item.Raw, item.Rounded + (positive ? CorrectionRoundingStep : -CorrectionRoundingStep));
                steps += positive ? -1 : 1;
            }

            foreach (var (index, _, value) in rounded)
            {
                applyCorrection(index, value);
            }
        }
    }
}
