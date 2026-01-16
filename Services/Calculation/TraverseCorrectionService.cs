using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.Enums;
using Nivtropy.Presentation.Models;

namespace Nivtropy.Services.Calculation
{
    public class StationCorrectionInput
    {
        public int Index { get; init; }
        public string? BackCode { get; init; }
        public string? ForeCode { get; init; }
        public double? DeltaH { get; init; }
        public double? HdBack { get; init; }
        public double? HdFore { get; init; }
    }

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
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor,
            double methodOrientationSign,
            AdjustmentMode adjustmentMode);
    }

    public class TraverseCorrectionService : ITraverseCorrectionService
    {
        private const double CorrectionRoundingStep = 0.0001;

        public CorrectionCalculationResult CalculateCorrections(
            IReadOnlyList<StationCorrectionInput> stations,
            Func<string?, bool> isAnchor,
            double methodOrientationSign,
            AdjustmentMode adjustmentMode)
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

            var closureMode = DetermineClosureMode(stations, isAnchor, anchorPoints, adjustmentMode);
            var corrections = new Dictionary<int, (double? correction, double? baseline, CorrectionDisplayMode mode)>();

            foreach (var station in stations)
                corrections[station.Index] = (null, null, CorrectionDisplayMode.None);

            switch (closureMode)
            {
                case TraverseClosureMode.Simple:
                    {
                        var closure = CalculateCorrectionsForSection(
                            stations.ToList(), methodOrientationSign,
                            (idx, value) => corrections[idx] = (value, value, CorrectionDisplayMode.Single));
                        if (closure.HasValue) result.Closures.Add(closure.Value);
                        break;
                    }
                case TraverseClosureMode.Local:
                    {
                        CalculateCorrectionsForSection(
                            stations.ToList(), methodOrientationSign,
                            (idx, value) => { var c = corrections[idx]; corrections[idx] = (c.correction, value, c.mode); });
                        CalculateCorrectionsWithSections(
                            stations.ToList(), anchorPoints, methodOrientationSign, result.Closures,
                            (idx, value) => { var c = corrections[idx]; corrections[idx] = (value, c.baseline, CorrectionDisplayMode.Local); });
                        break;
                    }
                case TraverseClosureMode.Open:
                    {
                        var closure = stations.Where(s => s.DeltaH.HasValue)
                            .Sum(s => s.DeltaH!.Value * methodOrientationSign);
                        result.Closures.Add(closure);
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

        private static List<(int Index, string? Code)> CollectAnchorPoints(IReadOnlyList<StationCorrectionInput> stations, Func<string?, bool> isAnchor)
        {
            var knownPoints = new List<(int Index, string? Code)>();
            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i];
                if (!string.IsNullOrWhiteSpace(station.BackCode) && isAnchor(station.BackCode))
                    if (knownPoints.All(p => p.Index != i))
                        knownPoints.Add((i, station.BackCode));
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
            AdjustmentMode adjustmentMode)
        {
            var startCode = stations.FirstOrDefault()?.BackCode ?? stations.FirstOrDefault()?.ForeCode;
            var endCode = stations.LastOrDefault()?.ForeCode ?? stations.LastOrDefault()?.BackCode;
            bool startKnown = !string.IsNullOrWhiteSpace(startCode) && isAnchor(startCode);
            bool endKnown = !string.IsNullOrWhiteSpace(endCode) && isAnchor(endCode);
            bool closesByLoop = !string.IsNullOrWhiteSpace(startCode) &&
                !string.IsNullOrWhiteSpace(endCode) &&
                string.Equals(startCode, endCode, StringComparison.OrdinalIgnoreCase);
            bool isClosed = closesByLoop || (startKnown && endKnown);
            var distinctAnchorCount = anchorPoints
                .Select(a => a.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

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

        private static void CalculateCorrectionsWithSections(List<StationCorrectionInput> stations, List<(int Index, string? Code)> knownPoints, double methodSign, List<double> closures, Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0) return;
            if (knownPoints.Count < 2)
            {
                var closure = CalculateCorrectionsForSection(stations, methodSign, applyCorrection);
                if (closure.HasValue) closures.Add(closure.Value);
                return;
            }
            for (int i = 0; i < knownPoints.Count - 1; i++)
            {
                var sectionStations = stations.Skip(knownPoints[i].Index).Take(knownPoints[i + 1].Index - knownPoints[i].Index).ToList();
                if (sectionStations.Count > 0)
                {
                    var closure = CalculateCorrectionsForSection(sectionStations, methodSign, applyCorrection);
                    if (closure.HasValue) closures.Add(closure.Value);
                }
            }
        }

        private static double? CalculateCorrectionsForSection(List<StationCorrectionInput> stations, double methodSign, Action<int, double>? applyCorrection)
        {
            if (stations.Count == 0) return null;
            var sectionClosure = stations.Where(s => s.DeltaH.HasValue).Sum(s => s.DeltaH!.Value * methodSign);
            var adjustableStations = stations.Where(s => s.DeltaH.HasValue).ToList();
            if (adjustableStations.Count == 0) return sectionClosure;

            double totalDistance = stations.Sum(s => ((s.HdBack ?? 0) + (s.HdFore ?? 0)) / 2.0);
            var allocations = new List<(int Index, double Raw, double Rounded)>();

            if (totalDistance <= 0)
            {
                var correctionPerStation = -sectionClosure / adjustableStations.Count;
                foreach (var station in adjustableStations)
                    allocations.Add((station.Index, correctionPerStation, correctionPerStation));
            }
            else
            {
                var correctionFactor = -sectionClosure / totalDistance;
                foreach (var station in adjustableStations)
                {
                    var avgDistance = ((station.HdBack ?? 0) + (station.HdFore ?? 0)) / 2.0;
                    allocations.Add((station.Index, correctionFactor * avgDistance, correctionFactor * avgDistance));
                }
            }

            if (applyCorrection != null)
                foreach (var (index, _, rounded) in allocations)
                    applyCorrection(index, Math.Round(rounded, 4));
            return sectionClosure;
        }
    }
}
