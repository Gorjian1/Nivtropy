using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Domain.Services;

namespace Nivtropy.Application.Services;

public interface ITraverseCalculationService
{
    List<StationDto> BuildTraverseRows(IReadOnlyList<MeasurementDto> records, IReadOnlyList<RunSummaryDto> runs);

    void RecalculateHeights(IList<StationDto> rows, Func<string, double?> getKnownHeight);

    List<RunSummaryDto> CalculateLineSummaries(IReadOnlyList<StationDto> rows);

    void ApplyCorrections(IList<StationDto> rows, RunSummaryDto run, double closureValue);

    void ApplyCorrections(
        IList<StationDto> rows,
        Func<string, bool> isAnchor,
        double methodOrientationSign,
        AdjustmentMode adjustmentMode);
}

public class TraverseCalculationService : ITraverseCalculationService
{
    private readonly ITraverseCorrectionService _correctionService;

    public TraverseCalculationService(ITraverseCorrectionService correctionService)
    {
        _correctionService = correctionService ?? throw new ArgumentNullException(nameof(correctionService));
    }

    public List<StationDto> BuildTraverseRows(IReadOnlyList<MeasurementDto> records, IReadOnlyList<RunSummaryDto> runs)
    {
        if (records.Count == 0)
            return new List<StationDto>();

        var runGroups = GroupMeasurements(records);
        var runLookup = runs.ToDictionary(r => r.Index);
        var result = new List<StationDto>();

        for (int g = 0; g < runGroups.Count; g++)
        {
            var group = runGroups[g];
            if (group.Count == 0)
                continue;

            var runIndex = g + 1;
            if (!runLookup.TryGetValue(runIndex, out var runSummary))
            {
                runSummary = BuildSummary(runIndex, group);
            }

            var lineName = GetLineName(runSummary, runIndex);
            result.AddRange(BuildStationsForRun(group, runSummary, lineName));
        }

        return result;
    }

    public void RecalculateHeights(IList<StationDto> rows, Func<string, double?> getKnownHeight)
    {
        if (rows.Count == 0)
            return;

        double? runningHeight = null;
        double? runningHeightZ0 = null;

        foreach (var row in rows)
        {
            if (!string.IsNullOrEmpty(row.BackCode))
            {
                var backKnownHeight = getKnownHeight(row.BackCode);
                if (backKnownHeight.HasValue)
                {
                    runningHeight = backKnownHeight;
                    runningHeightZ0 = backKnownHeight;
                }
            }

            if (runningHeight.HasValue)
            {
                row.BackHeight = runningHeight;
                row.BackHeightRaw = runningHeightZ0;
            }

            if (runningHeight.HasValue && row.AdjustedDeltaH.HasValue)
            {
                row.ForeHeight = runningHeight.Value + row.AdjustedDeltaH.Value;
                runningHeight = row.ForeHeight;
            }

            if (runningHeightZ0.HasValue && row.DeltaH.HasValue)
            {
                row.ForeHeightRaw = runningHeightZ0.Value + row.DeltaH.Value;
                runningHeightZ0 = row.ForeHeightRaw;
            }

            if (!string.IsNullOrEmpty(row.ForeCode))
            {
                var foreKnownHeight = getKnownHeight(row.ForeCode);
                if (foreKnownHeight.HasValue)
                {
                    row.ForeHeight = foreKnownHeight;
                    runningHeight = foreKnownHeight;
                }
            }
        }
    }

    public List<RunSummaryDto> CalculateLineSummaries(IReadOnlyList<StationDto> rows)
    {
        return rows
            .Select(r => r.RunSummary)
            .Where(summary => summary != null)
            .Distinct()
            .Cast<RunSummaryDto>()
            .ToList();
    }

    public void ApplyCorrections(IList<StationDto> rows, RunSummaryDto run, double closureValue)
    {
        if (rows.Count == 0)
            return;

        ResetCorrections(rows);
        run.Closures = new List<double> { closureValue };
    }

    public void ApplyCorrections(
        IList<StationDto> rows,
        Func<string, bool> isAnchor,
        double methodOrientationSign,
        AdjustmentMode adjustmentMode)
    {
        if (rows.Count == 0)
            return;

        ResetCorrections(rows);

        var result = _correctionService.CalculateCorrections(
            rows.Select((row, idx) => new StationDto
            {
                Index = idx,
                LineName = row.LineName,
                BackCode = row.BackCode,
                ForeCode = row.ForeCode,
                BackReading = row.BackReading,
                ForeReading = row.ForeReading,
                BackDistance = row.BackDistance,
                ForeDistance = row.ForeDistance,
                BackHeight = row.BackHeight,
                ForeHeight = row.ForeHeight,
                BackHeightRaw = row.BackHeightRaw,
                ForeHeightRaw = row.ForeHeightRaw,
                IsBackHeightKnown = row.IsBackHeightKnown,
                IsForeHeightKnown = row.IsForeHeightKnown,
                IsArmDifferenceExceeded = row.IsArmDifferenceExceeded,
                Correction = row.Correction,
                BaselineCorrection = row.BaselineCorrection,
                CorrectionMode = row.CorrectionMode
            }).ToList(),
            code => !string.IsNullOrWhiteSpace(code) && isAnchor(code!),
            methodOrientationSign,
            adjustmentMode);

        foreach (var correction in result.Corrections)
        {
            if (correction.Index >= 0 && correction.Index < rows.Count)
            {
                var row = rows[correction.Index];
                row.Correction = correction.Correction;
                row.BaselineCorrection = correction.BaselineCorrection;
                row.CorrectionMode = correction.Mode;
            }
        }

        var lineSummary = rows.FirstOrDefault()?.RunSummary;
        if (lineSummary != null)
        {
            lineSummary.KnownPointsCount = result.DistinctAnchorCount;
            lineSummary.Closures = result.Closures.ToList();
        }
    }

    private static void ResetCorrections(IList<StationDto> rows)
    {
        foreach (var row in rows)
        {
            row.Correction = null;
            row.BaselineCorrection = null;
            row.CorrectionMode = CorrectionDisplayMode.None;
        }
    }

    private static string GetLineName(RunSummaryDto runSummary, int index)
    {
        return !string.IsNullOrWhiteSpace(runSummary.OriginalLineNumber)
            ? $"Ход {runSummary.OriginalLineNumber}"
            : $"Ход {index:D2}";
    }

    private static List<List<MeasurementDto>> GroupMeasurements(IReadOnlyList<MeasurementDto> records)
    {
        var groups = new List<List<MeasurementDto>>();
        var current = new List<MeasurementDto>();
        MeasurementDto? previous = null;

        foreach (var record in records)
        {
            if (previous != null && ShouldStartNewLine(previous, record))
            {
                if (current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<MeasurementDto>();
                }
            }

            current.Add(record);
            previous = record;
        }

        if (current.Count > 0)
            groups.Add(current);

        return groups;
    }

    private static bool ShouldStartNewLine(MeasurementDto previous, MeasurementDto current)
    {
        if (current.LineMarker == "Start-Line")
            return true;

        if (current.LineMarker == "Cont-Line")
            return false;

        if (current.LineMarker == "End-Line")
            return false;

        if (current.LineMarker == null && previous.LineMarker == null)
        {
            if (current.Seq.HasValue && previous.Seq.HasValue)
            {
                if (current.Seq.Value - previous.Seq.Value > 50)
                    return true;
            }

            if (current.Mode != null && current.Mode.IndexOf("line", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!string.Equals(previous.Mode, current.Mode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static RunSummaryDto BuildSummary(int index, IReadOnlyList<MeasurementDto> group)
    {
        var start = group.FirstOrDefault(r => r.Rb_m.HasValue) ?? group.First();
        var end = group.LastOrDefault(r => r.Rf_m.HasValue) ?? group.Last();

        var startLineRecord = group.FirstOrDefault(r => r.LineMarker == "Start-Line");
        var originalLineNumber = startLineRecord?.OriginalLineNumber;

        double? deltaSum = null;
        double? totalDistanceBack = null;
        double? totalDistanceFore = null;
        double? armDiffAccumulation = null;

        foreach (var rec in group)
        {
            if (rec.DeltaH.HasValue)
            {
                deltaSum = (deltaSum ?? 0d) + rec.DeltaH.Value;
            }

            if (rec.HdBack_m.HasValue)
            {
                totalDistanceBack = (totalDistanceBack ?? 0d) + rec.HdBack_m.Value;
            }

            if (rec.HdFore_m.HasValue)
            {
                totalDistanceFore = (totalDistanceFore ?? 0d) + rec.HdFore_m.Value;
            }

            if (rec.HdBack_m.HasValue && rec.HdFore_m.HasValue)
            {
                var armDiff = rec.HdBack_m.Value - rec.HdFore_m.Value;
                armDiffAccumulation = (armDiffAccumulation ?? 0d) + armDiff;
            }
        }

        return new RunSummaryDto
        {
            Index = index,
            OriginalLineNumber = originalLineNumber,
            StartPointCode = start.Target ?? start.StationCode,
            EndPointCode = end.Target ?? end.StationCode,
            StationCount = group.Count,
            DeltaHSum = deltaSum,
            TotalDistanceBack = totalDistanceBack,
            TotalDistanceFore = totalDistanceFore,
            ArmDifferenceAccumulation = armDiffAccumulation
        };
    }

    private static List<StationDto> BuildStationsForRun(
        IReadOnlyList<MeasurementDto> records,
        RunSummaryDto currentLineSummary,
        string lineName)
    {
        var list = new List<StationDto>();
        string mode = "BF";
        StationDto? pending = null;
        int idx = 1;
        bool isFirstPointOfLine = true;
        string? firstPointCode = null;

        foreach (var r in records)
        {
            if (r.LineMarker == "Start-Line" && !string.IsNullOrWhiteSpace(r.Mode))
            {
                var modeUpper = r.Mode.Trim().ToUpperInvariant();
                if (modeUpper == "BF" || modeUpper == "FB")
                    mode = modeUpper;
                isFirstPointOfLine = true;
            }

            bool isBF = mode == "BF";

            if (r.Rb_m.HasValue)
            {
                if (isFirstPointOfLine && firstPointCode == null)
                {
                    firstPointCode = r.StationCode;
                    var virtualStation = new StationDto
                    {
                        LineName = lineName,
                        Index = idx++,
                        BackCode = r.StationCode,
                        RunSummary = currentLineSummary
                    };
                    list.Add(virtualStation);
                    isFirstPointOfLine = false;
                }

                if (pending == null)
                {
                    if (isBF)
                        pending = new StationDto { LineName = lineName, Index = idx++, BackCode = r.StationCode, BackReading = r.Rb_m, BackDistance = r.HD_m, RunSummary = currentLineSummary };
                    else
                        pending = new StationDto { LineName = lineName, Index = idx++, ForeCode = r.StationCode, BackReading = r.Rb_m, ForeDistance = r.HD_m, RunSummary = currentLineSummary };
                }
                else
                {
                    if (isBF)
                    {
                        pending.BackReading ??= r.Rb_m;
                        pending.BackDistance ??= r.HD_m;
                        pending.BackCode ??= r.StationCode;
                    }
                    else
                    {
                        pending.BackReading ??= r.Rb_m;
                        pending.ForeDistance ??= r.HD_m;
                        pending.ForeCode ??= r.StationCode;
                    }
                    list.Add(pending);
                    pending = null;
                }
                continue;
            }

            if (r.Rf_m.HasValue)
            {
                if (pending == null)
                {
                    if (isBF)
                        pending = new StationDto { LineName = lineName, Index = idx++, ForeCode = r.StationCode, ForeReading = r.Rf_m, ForeDistance = r.HD_m, RunSummary = currentLineSummary };
                    else
                        pending = new StationDto { LineName = lineName, Index = idx++, BackCode = r.StationCode, ForeReading = r.Rf_m, BackDistance = r.HD_m, RunSummary = currentLineSummary };
                }
                else
                {
                    if (isBF)
                    {
                        pending.ForeReading ??= r.Rf_m;
                        pending.ForeDistance ??= r.HD_m;
                        pending.ForeCode ??= r.StationCode;
                    }
                    else
                    {
                        pending.ForeReading ??= r.Rf_m;
                        pending.BackDistance ??= r.HD_m;
                        pending.BackCode ??= r.StationCode;
                    }
                    list.Add(pending);
                    pending = null;
                }
            }
        }
        if (pending != null) list.Add(pending);
        return list;
    }
}
