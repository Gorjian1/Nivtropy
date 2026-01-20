using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Domain.Services;
using Nivtropy.Domain.Model;

namespace Nivtropy.Application.Services;

public interface ITraverseCalculationService
{
    List<StationDto> BuildTraverseRows(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<RunSummaryDto> runs);

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
    private readonly Dictionary<int, List<StationDto>> _cache = new();
    private readonly object _cacheLock = new();
    private readonly ITraverseCorrectionService _correctionService;

    public TraverseCalculationService(ITraverseCorrectionService correctionService)
    {
        _correctionService = correctionService ?? throw new ArgumentNullException(nameof(correctionService));
    }

    public List<StationDto> BuildTraverseRows(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<RunSummaryDto> runs)
    {
        var hash = ComputeHash(records, runs);

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(hash, out var cached))
                return cached;
        }

        var result = BuildTraverseRowsInternal(records, runs);

        lock (_cacheLock)
        {
            _cache[hash] = result;
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

    private static string GetLineName(RunSummaryDto? run)
    {
        if (run == null)
            return "?";

        if (!string.IsNullOrWhiteSpace(run.OriginalLineNumber))
            return $"Ход {run.OriginalLineNumber}";

        return $"Ход {run.Index:D2}";
    }

    private static List<List<MeasurementRecord>> GroupRecords(IReadOnlyList<MeasurementRecord> records)
    {
        var groups = new List<List<MeasurementRecord>>();
        var current = new List<MeasurementRecord>();
        MeasurementRecord? previous = null;

        foreach (var record in records)
        {
            if (previous != null && ShouldStartNewLine(previous, record))
            {
                if (current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<MeasurementRecord>();
                }
            }

            current.Add(record);
            previous = record;
        }

        if (current.Count > 0)
            groups.Add(current);

        return groups;
    }

    private static bool ShouldStartNewLine(MeasurementRecord previous, MeasurementRecord current)
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

    private static List<StationDto> BuildTraverseRowsInternal(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<RunSummaryDto> runs)
    {
        var list = new List<StationDto>();
        if (records.Count == 0)
            return list;

        var groups = GroupRecords(records);
        var runsByIndex = runs.ToDictionary(r => r.Index);

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var runIndex = g + 1;
            runsByIndex.TryGetValue(runIndex, out var runSummary);

            var line = GetLineName(runSummary);
            string mode = "BF";
            StationDto? pending = null;
            int idx = 1;
            foreach (var r in group)
            {
                if (r.LineMarker == "Start-Line" && !string.IsNullOrWhiteSpace(r.Mode))
                {
                    var modeUpper = r.Mode.Trim().ToUpperInvariant();
                    if (modeUpper == "BF" || modeUpper == "FB")
                        mode = modeUpper;
                }

                bool isBF = mode == "BF";

                if (r.Rb_m.HasValue)
                {
                    if (pending == null)
                    {
                        if (isBF)
                            pending = new StationDto { LineName = line, Index = idx++, BackCode = r.StationCode, BackReading = r.Rb_m, BackDistance = r.HD_m, RunSummary = runSummary };
                        else
                            pending = new StationDto { LineName = line, Index = idx++, ForeCode = r.StationCode, BackReading = r.Rb_m, ForeDistance = r.HD_m, RunSummary = runSummary };
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
                            pending = new StationDto { LineName = line, Index = idx++, ForeCode = r.StationCode, ForeReading = r.Rf_m, ForeDistance = r.HD_m, RunSummary = runSummary };
                        else
                            pending = new StationDto { LineName = line, Index = idx++, BackCode = r.StationCode, ForeReading = r.Rf_m, BackDistance = r.HD_m, RunSummary = runSummary };
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
        }

        return list;
    }

    private static int ComputeHash(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<RunSummaryDto> runs)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + records.Count;
            hash = hash * 31 + runs.Count;

            foreach (var run in runs)
            {
                hash = hash * 31 + run.Index;
                hash = hash * 31 + (run.OriginalLineNumber?.GetHashCode() ?? 0);
                hash = hash * 31 + run.StationCount;
                hash = hash * 31 + run.DeltaHSum.GetHashCode();
            }

            int step = records.Count <= 100 ? 1 : Math.Max(1, records.Count / 10);
            for (int i = 0; i < records.Count; i += step)
            {
                var rec = records[i];
                hash = hash * 31 + i;
                hash = hash * 31 + (rec.StationCode?.GetHashCode() ?? 0);
                hash = hash * 31 + (rec.Target?.GetHashCode() ?? 0);
                hash = hash * 31 + rec.Rb_m.GetHashCode();
                hash = hash * 31 + rec.Rf_m.GetHashCode();
            }

            if (records.Count > 1)
            {
                var last = records[records.Count - 1];
                hash = hash * 31 + (last.StationCode?.GetHashCode() ?? 0);
                hash = hash * 31 + last.Rb_m.GetHashCode();
            }

            return hash;
        }
    }
}
