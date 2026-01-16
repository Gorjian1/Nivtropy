using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.Enums;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;
using Nivtropy.Services;
using Nivtropy.Services.Calculation;

namespace Nivtropy.Application.Services;

public interface ITraverseCalculationService
{
    List<TraverseRow> BuildTraverseRows(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<LineSummary> runs);

    void RecalculateHeights(IList<TraverseRow> rows, Func<string, double?> getKnownHeight);

    List<LineSummary> CalculateLineSummaries(IReadOnlyList<TraverseRow> rows);

    void ApplyCorrections(IList<TraverseRow> rows, LineSummary run, double closureValue);

    void ApplyCorrections(
        IList<TraverseRow> rows,
        Func<string, bool> isAnchor,
        double methodOrientationSign,
        AdjustmentMode adjustmentMode);
}

public class TraverseCalculationService : ITraverseCalculationService
{
    private readonly ITraverseBuilder _traverseBuilder;
    private readonly ITraverseCorrectionService _correctionService;

    public TraverseCalculationService(
        ITraverseBuilder traverseBuilder,
        ITraverseCorrectionService correctionService)
    {
        _traverseBuilder = traverseBuilder;
        _correctionService = correctionService;
    }

    public List<TraverseRow> BuildTraverseRows(IReadOnlyList<MeasurementRecord> records, IReadOnlyList<LineSummary> runs)
    {
        return _traverseBuilder.Build(records);
    }

    public void RecalculateHeights(IList<TraverseRow> rows, Func<string, double?> getKnownHeight)
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

    public List<LineSummary> CalculateLineSummaries(IReadOnlyList<TraverseRow> rows)
    {
        return rows
            .Select(r => r.LineSummary)
            .Where(summary => summary != null)
            .Distinct()
            .Cast<LineSummary>()
            .ToList();
    }

    public void ApplyCorrections(IList<TraverseRow> rows, LineSummary run, double closureValue)
    {
        if (rows.Count == 0)
            return;

        ResetCorrections(rows);
        run.SetClosures(new[] { closureValue });
    }

    public void ApplyCorrections(
        IList<TraverseRow> rows,
        Func<string, bool> isAnchor,
        double methodOrientationSign,
        AdjustmentMode adjustmentMode)
    {
        if (rows.Count == 0)
            return;

        ResetCorrections(rows);

        var stations = rows.Select((row, idx) => new StationCorrectionInput
        {
            Index = idx,
            BackCode = row.BackCode,
            ForeCode = row.ForeCode,
            DeltaH = row.DeltaH,
            HdBack = row.HdBack_m,
            HdFore = row.HdFore_m
        }).ToList();

        var result = _correctionService.CalculateCorrections(
            stations,
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

        var lineSummary = rows.FirstOrDefault()?.LineSummary;
        if (lineSummary != null)
        {
            lineSummary.KnownPointsCount = result.DistinctAnchorCount;
            lineSummary.SetClosures(result.Closures);
        }
    }

    private static void ResetCorrections(IList<TraverseRow> rows)
    {
        foreach (var row in rows)
        {
            row.Correction = null;
            row.BaselineCorrection = null;
            row.CorrectionMode = CorrectionDisplayMode.None;
        }
    }
}
