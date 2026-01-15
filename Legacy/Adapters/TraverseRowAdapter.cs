namespace Nivtropy.Legacy.Adapters;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;
using Nivtropy.Models;

/// <summary>
/// Адаптер для конвертации между TraverseRow (старый) и Observation (новый).
/// Используется для обратной совместимости с UI.
/// </summary>
public static class TraverseRowAdapter
{
    /// <summary>Конвертировать Observation в TraverseRow для UI</summary>
    public static TraverseRow ToTraverseRow(Observation obs)
    {
        return new TraverseRow
        {
            LineName = obs.Run.Name,
            Index = obs.StationIndex,
            BackCode = obs.From.Code.Value,
            ForeCode = obs.To.Code.Value,
            Rb_m = obs.BackReading.Meters,
            Rf_m = obs.ForeReading.Meters,
            HdBack_m = obs.BackDistance.Meters,
            HdFore_m = obs.ForeDistance.Meters,
            Correction = obs.Correction,
            BackHeight = obs.From.Height.IsKnown ? obs.From.Height.Value : null,
            ForeHeight = obs.To.Height.IsKnown ? obs.To.Height.Value : null,
            IsBackHeightKnown = obs.From.Height.IsKnown && obs.From.Type == PointType.Benchmark,
            IsForeHeightKnown = obs.To.Height.IsKnown && obs.To.Type == PointType.Benchmark
            // IsVirtualStation вычисляется автоматически
        };
    }

    /// <summary>Конвертировать список Observations в TraverseRows</summary>
    public static List<TraverseRow> ToTraverseRows(Run run)
    {
        var rows = new List<TraverseRow>();

        // Добавляем виртуальную станцию для первой точки
        if (run.StartPoint != null)
        {
            rows.Add(new TraverseRow
            {
                LineName = run.Name,
                Index = 0,
                BackCode = run.StartPoint.Code.Value,
                ForeCode = "", // Пустой ForeCode делает IsVirtualStation = true автоматически
                BackHeight = run.StartPoint.Height.IsKnown ? run.StartPoint.Height.Value : null,
                IsBackHeightKnown = run.StartPoint.Type == PointType.Benchmark
            });
        }

        // Добавляем обычные станции
        foreach (var obs in run.Observations)
        {
            rows.Add(ToTraverseRow(obs));
        }

        return rows;
    }

    /// <summary>Создать кортеж данных из TraverseRow для построения Observation</summary>
    public static (PointCode from, PointCode to, Reading back, Reading fore, Distance backDist, Distance foreDist)
        FromTraverseRow(TraverseRow row)
    {
        return (
            new PointCode(row.BackCode ?? ""),
            new PointCode(row.ForeCode ?? ""),
            new Reading(row.Rb_m ?? 0),
            new Reading(row.Rf_m ?? 0),
            new Distance(row.HdBack_m ?? 0),
            new Distance(row.HdFore_m ?? 0)
        );
    }
}
