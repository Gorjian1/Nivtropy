namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Models;

/// <summary>
/// Адаптер для конвертации между LineSummary (старый) и Run (новый).
/// Используется для обратной совместимости с UI.
/// </summary>
public static class LineSummaryAdapter
{
    /// <summary>Конвертировать Run в LineSummary для UI</summary>
    public static LineSummary ToLineSummary(Run run, int index)
    {
        var summary = new LineSummary(
            index: index,
            startTarget: run.StartPoint?.Code.Value,
            startStation: run.StartPoint?.Code.Value,
            endTarget: run.EndPoint?.Code.Value,
            endStation: run.EndPoint?.Code.Value,
            recordCount: run.StationCount,
            deltaHSum: run.DeltaHSum,
            totalDistanceBack: run.Observations.Sum(o => o.BackDistance.Meters),
            totalDistanceFore: run.Observations.Sum(o => o.ForeDistance.Meters),
            armDifferenceAccumulation: run.AccumulatedArmDifference,
            knownPointsCount: run.Points.Count(p => p.Height.IsKnown),
            systemId: run.System?.Id.ToString(),
            isActive: run.IsActive,
            originalLineNumber: run.OriginalNumber ?? index.ToString()
        );

        // Устанавливаем невязки через метод
        if (run.Closure != null)
        {
            summary.SetClosures(new[] { run.Closure.Value.ValueMm / 1000.0 });
        }

        return summary;
    }

    /// <summary>Конвертировать все ходы сети в LineSummaries</summary>
    public static List<LineSummary> ToLineSummaries(LevelingNetwork network)
    {
        return network.Runs
            .Select((run, idx) => ToLineSummary(run, idx))
            .ToList();
    }
}
