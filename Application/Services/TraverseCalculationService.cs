using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Domain.Services;
using Nivtropy.Models;
using Nivtropy.Services;

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
    private readonly ITraverseBuilder _traverseBuilder;
    private readonly ITraverseCorrectionService _correctionService;

    public TraverseCalculationService(ITraverseCorrectionService correctionService)
    {
        // Создаём TraverseBuilder как internal dependency (implementation detail)
        _traverseBuilder = new TraverseBuilder();
        _correctionService = correctionService ?? throw new ArgumentNullException(nameof(correctionService));
    }

    public List<StationDto> BuildTraverseRows(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<RunSummaryDto> runs)
    {
        return _traverseBuilder.Build(records);
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
                row.BackHeightZ0 = runningHeightZ0;
            }

            if (runningHeight.HasValue && row.AdjustedDeltaH.HasValue)
            {
                row.ForeHeight = runningHeight.Value + row.AdjustedDeltaH.Value;
                runningHeight = row.ForeHeight;
            }

            if (runningHeightZ0.HasValue && row.DeltaH.HasValue)
            {
                row.ForeHeightZ0 = runningHeightZ0.Value + row.DeltaH.Value;
                runningHeightZ0 = row.ForeHeightZ0;
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
}
