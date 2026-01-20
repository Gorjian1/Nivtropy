using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;

namespace Nivtropy.Domain.Services
{
    public class StationCorrectionResult
    {
        public int Index { get; init; }
        public double? Correction { get; init; }
        public double? BaselineCorrection { get; init; }
        public CorrectionDisplayMode Mode { get; init; }
    }

    public record CorrectionCalculationResult
    {
        public List<StationCorrectionResult> Corrections { get; } = new();
        public List<double> Closures { get; } = new();
        public TraverseClosureMode ClosureMode { get; init; }
        public int DistinctAnchorCount { get; init; }
    }

    public interface ITraverseCorrectionService
    {
        CorrectionCalculationResult CalculateCorrections(
            IReadOnlyList<StationDto> stations,
            Func<string?, double?> getKnownHeightMeters,
            double methodOrientationSign,
            AdjustmentMode adjustmentMode);
    }

    public class TraverseCorrectionService : ITraverseCorrectionService
    {
        private const double CorrectionRoundingStep = 0.0001;

        public CorrectionCalculationResult CalculateCorrections(
            IReadOnlyList<StationDto> stations,
            Func<string?, double?> getKnownHeightMeters,
            double methodOrientationSign,
            AdjustmentMode adjustmentMode)
        {
            var result = new CorrectionCalculationResult();
            if (stations.Count == 0)
                return result;

            var anchorPoints = CollectAnchorPoints(stations, getKnownHeightMeters);
            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var closureMode = DetermineClosureMode(stations, getKnownHeightMeters, anchorPoints, adjustmentMode);
            var corrections = new Dictionary<int, (double? correction, double? baseline, CorrectionDisplayMode mode)>();

            foreach (var station in stations)
                corrections[station.Index] = (null, null, CorrectionDisplayMode.None);

            switch (closureMode)
            {
                case TraverseClosureMode.Open:
                    {
                        var closure = stations.Where(s => s.DeltaH.HasValue)
                            .Sum(s => s.DeltaH!.Value * methodOrientationSign);
                        result.Closures.Add(closure);
                        break;
                    }
                case TraverseClosureMode.Simple:
                    {
                        var closure = CalculateCorrectionsForSection(
                            stations.ToList(),
                            methodOrientationSign,
                            requiredSum: 0,
                            (idx, value) => corrections[idx] = (value, value, CorrectionDisplayMode.Single));
                        if (closure.HasValue) result.Closures.Add(closure.Value);
                        break;
                    }
                case TraverseClosureMode.Local:
                    {
                        CalculateCorrectionsForSection(
                            stations.ToList(),
                            methodOrientationSign,
                            requiredSum: 0,
                            (idx, value) => { var c = corrections[idx]; corrections[idx] = (c.correction, value, c.mode); });
                        CalculateCorrectionsWithSections(
                            stations.ToList(), anchorPoints, getKnownHeightMeters, methodOrientationSign, result.Closures,
                            (idx, value) => { var c = corrections[idx]; corrections[idx] = (value, c.baseline, CorrectionDisplayMode.Local); });
                        break;
                    }
            }

            if (result.Closures.Count == 0)
            {
                var orientedClosure = stations.Where(s => s.DeltaH.HasValue).Sum(s => s.DeltaH!.Value * methodOrientationSign);
                result.Closures.Add(orientedClosure);
            }

            foreach (var station in stations)
            {
                var (correction, baseline, mode) = corrections[station.Index];
                result.Corrections.Add(new StationCorrectionResult { Index = station.Index, Correction = correction, BaselineCorrection = baseline, Mode = mode });
            }

            return result with { ClosureMode = closureMode, DistinctAnchorCount = distinctAnchorCount };
        }

        private static List<(int Index, string? Code)> CollectAnchorPoints(
            IReadOnlyList<StationDto> stations,
            Func<string?, double?> getKnownHeightMeters)
        {
            var knownPoints = new List<(int Index, string? Code)>();
            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                if (!string.IsNullOrWhiteSpace(station.BackCode) && getKnownHeightMeters(station.BackCode).HasValue)
                    if (knownPoints.All(p => p.Index != i))
                        knownPoints.Add((i, station.BackCode));
                if (!string.IsNullOrWhiteSpace(station.ForeCode) && getKnownHeightMeters(station.ForeCode).HasValue)
                {
                    var anchorIndex = Math.Min(i + 1, stations.Count);
                    if (knownPoints.All(p => p.Index != anchorIndex))
                        knownPoints.Add((anchorIndex, station.ForeCode));
                }
            }
            return knownPoints.OrderBy(p => p.Index).ToList();
        }

        private static TraverseClosureMode DetermineClosureMode(
            IReadOnlyList<StationDto> stations,
            Func<string?, double?> getKnownHeightMeters,
            List<(int Index, string? Code)> anchorPoints,
            AdjustmentMode adjustmentMode)
        {
            var startCode = stations.FirstOrDefault()?.BackCode ?? stations.FirstOrDefault()?.ForeCode;
            var endCode = stations.LastOrDefault()?.ForeCode ?? stations.LastOrDefault()?.BackCode;
            bool startKnown = !string.IsNullOrWhiteSpace(startCode) && getKnownHeightMeters(startCode).HasValue;
            bool endKnown = !string.IsNullOrWhiteSpace(endCode) && getKnownHeightMeters(endCode).HasValue;
            bool closesByLoop = !string.IsNullOrWhiteSpace(startCode) && !string.IsNullOrWhiteSpace(endCode) && string.Equals(startCode, endCode, StringComparison.OrdinalIgnoreCase);
            bool isClosed = closesByLoop || (startKnown && endKnown);
            var distinctAnchorCount = anchorPoints.Select(a => a.Code).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).Count();

            if (adjustmentMode == AdjustmentMode.None || adjustmentMode == AdjustmentMode.Network)
            {
                return TraverseClosureMode.Open;
            }

            if (!isClosed)
            {
                return distinctAnchorCount >= 2 ? TraverseClosureMode.Local : TraverseClosureMode.Open;
            }

            return distinctAnchorCount > 1 ? TraverseClosureMode.Local : TraverseClosureMode.Simple;
        }

        private static void CalculateCorrectionsWithSections(
            List<StationDto> stations,
            List<(int Index, string? Code)> knownPoints,
            Func<string?, double?> getKnownHeightMeters,
            double methodSign,
            List<double> closures,
            Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0) return;
            if (knownPoints.Count < 2)
            {
                var closure = CalculateCorrectionsForSection(stations, methodSign, requiredSum: 0, applyCorrection);
                if (closure.HasValue) closures.Add(closure.Value);
                return;
            }
            for (int i = 0; i < knownPoints.Count - 1; i++)
            {
                var sectionStations = stations.Skip(knownPoints[i].Index).Take(knownPoints[i + 1].Index - knownPoints[i].Index).ToList();
                if (sectionStations.Count > 0)
                {
                    var startHeight = getKnownHeightMeters(knownPoints[i].Code);
                    var endHeight = getKnownHeightMeters(knownPoints[i + 1].Code);
                    if (!startHeight.HasValue || !endHeight.HasValue)
                        continue;

                    var requiredSum = endHeight.Value - startHeight.Value;
                    var closure = CalculateCorrectionsForSection(sectionStations, methodSign, requiredSum, applyCorrection);
                    if (closure.HasValue) closures.Add(closure.Value);
                }
            }
        }

        private static double? CalculateCorrectionsForSection(
            List<StationDto> stations,
            double methodSign,
            double requiredSum,
            Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0) return null;
            var measuredStations = stations.Where(s => s.DeltaH.HasValue).ToList();
            if (measuredStations.Count == 0)
                return null;

            var measuredSum = measuredStations.Sum(s => s.DeltaH!.Value);
            var sectionClosure = methodSign * (measuredSum - requiredSum);
            var adjustableStations = stations.Where(s => s.DeltaH.HasValue).ToList();
            if (adjustableStations.Count == 0) return sectionClosure;

            double totalDistance = stations.Sum(s => ((s.BackDistance ?? 0) + (s.ForeDistance ?? 0)) / 2.0);
            var allocations = new List<(int Index, double Raw, double Length)>();
            var requiredCorrectionSum = requiredSum - measuredSum;

            if (totalDistance <= 0)
            {
                var correctionPerStation = requiredCorrectionSum / adjustableStations.Count;
                foreach (var station in adjustableStations)
                    allocations.Add((station.Index, correctionPerStation, 1));
            }
            else
            {
                var correctionFactor = requiredCorrectionSum / totalDistance;
                foreach (var station in adjustableStations)
                {
                    var avgDistance = ((station.BackDistance ?? 0) + (station.ForeDistance ?? 0)) / 2.0;
                    allocations.Add((station.Index, correctionFactor * avgDistance, avgDistance));
                }
            }

            if (applyCorrection != null)
            {
                var rounded = RoundCorrections(allocations, requiredCorrectionSum);
                foreach (var (index, value) in rounded)
                    applyCorrection(index, value);
            }

            return sectionClosure;
        }

        private static List<(int Index, double Value)> RoundCorrections(
            List<(int Index, double Raw, double Length)> allocations,
            double requiredSum)
        {
            var rounded = allocations
                .Select(a => (a.Index, Value: Math.Round(a.Raw, 4), a.Length))
                .ToList();

            var roundedSum = rounded.Sum(a => a.Value);
            var residual = requiredSum - roundedSum;
            var ticks = (int)Math.Round(residual / CorrectionRoundingStep);

            if (ticks == 0)
                return rounded.Select(a => (a.Index, a.Value)).ToList();

            var order = rounded
                .OrderByDescending(a => a.Length)
                .Select(a => a.Index)
                .ToArray();

            int k = 0;
            while (ticks != 0 && order.Length > 0)
            {
                var index = order[k % order.Length];
                var entryIndex = rounded.FindIndex(a => a.Index == index);
                if (entryIndex >= 0)
                {
                    var entry = rounded[entryIndex];
                    entry.Value += ticks > 0 ? CorrectionRoundingStep : -CorrectionRoundingStep;
                    rounded[entryIndex] = entry;
                    ticks += ticks > 0 ? -1 : 1;
                }
                k++;
            }

            return rounded.Select(a => (a.Index, a.Value)).ToList();
        }
    }
}
