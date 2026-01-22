namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Распределение невязки пропорционально длине станций.
/// </summary>
public class ProportionalClosureDistributor : IClosureDistributor
{
    public void DistributeClosure(Run run)
    {
        if (run.Closure == null || !run.Closure.Value.IsWithinTolerance)
            return;

        var totalLength = run.TotalLength.Meters;
        if (totalLength <= 0)
            return;

        var closureMeters = run.Closure.Value.ValueMm / 1000.0;

        foreach (var obs in run.Observations)
        {
            var weight = obs.StationLength.Meters / totalLength;
            var target = -closureMeters * weight;
            obs.ApplyCorrection(target - obs.Correction);
        }
    }

    public void DistributeClosureWithSections(Run run)
    {
        // Находим известные точки внутри хода
        var knownPoints = run.Points
            .Where(p => p.Height.IsKnown)
            .ToList();

        if (knownPoints.Count < 2)
        {
            // Нет секций - обычное распределение
            DistributeClosure(run);
            return;
        }

        // Разбиваем на секции
        var sections = new List<(int startIdx, int endIdx, double closure)>();

        for (int i = 0; i < knownPoints.Count - 1; i++)
        {
            var startPoint = knownPoints[i];
            var endPoint = knownPoints[i + 1];

            // Находим индексы наблюдений для этой секции
            var startIdx = run.Observations
                .ToList()
                .FindIndex(o => o.From.Code.Equals(startPoint.Code));

            var endIdx = run.Observations
                .ToList()
                .FindIndex(o => o.To.Code.Equals(endPoint.Code));

            if (startIdx >= 0 && endIdx >= 0 && endIdx >= startIdx)
            {
                // Вычисляем невязку секции
                var theoretical = endPoint.Height - startPoint.Height;
                var measured = run.Observations
                    .Skip(startIdx)
                    .Take(endIdx - startIdx + 1)
                    .Sum(o => o.DeltaH);

                sections.Add((startIdx, endIdx, measured - theoretical));
            }
        }

        // Распределяем невязку по каждой секции
        foreach (var (startIdx, endIdx, closure) in sections)
        {
            var sectionObs = run.Observations
                .Skip(startIdx)
                .Take(endIdx - startIdx + 1)
                .ToList();

            var sectionLength = sectionObs.Sum(o => o.StationLength.Meters);
            if (sectionLength <= 0)
                continue;

            foreach (var obs in sectionObs)
            {
                var weight = obs.StationLength.Meters / sectionLength;
                var target = -closure * weight;
                obs.ApplyCorrection(target - obs.Correction);
            }
        }
    }
}
