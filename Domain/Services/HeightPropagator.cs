namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Реализация распространения высот через BFS обход графа.
/// </summary>
public class HeightPropagator : IHeightPropagator
{
    public int PropagateHeights(LevelingNetwork network)
    {
        // Сбрасываем ранее вычисленные высоты
        network.ResetCalculatedHeights();

        var calculatedCount = 0;
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        // Начинаем с реперов
        foreach (var benchmark in network.Benchmarks)
        {
            queue.Enqueue(benchmark);
            visited.Add(benchmark);
        }

        // BFS обход
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Распространяем по исходящим наблюдениям (вперёд)
            foreach (var obs in current.OutgoingObservations)
            {
                if (!visited.Contains(obs.To) && !obs.To.Height.IsKnown)
                {
                    var newHeight = obs.CalculateForeHeight();
                    if (newHeight.IsKnown)
                    {
                        obs.To.SetCalculatedHeight(newHeight);
                        calculatedCount++;
                        visited.Add(obs.To);
                        queue.Enqueue(obs.To);
                    }
                }
            }

            // Распространяем по входящим наблюдениям (назад)
            foreach (var obs in current.IncomingObservations)
            {
                if (!visited.Contains(obs.From) && !obs.From.Height.IsKnown)
                {
                    var newHeight = obs.CalculateBackHeight();
                    if (newHeight.IsKnown)
                    {
                        obs.From.SetCalculatedHeight(newHeight);
                        calculatedCount++;
                        visited.Add(obs.From);
                        queue.Enqueue(obs.From);
                    }
                }
            }
        }

        return calculatedCount;
    }

    public int PropagateHeightsInRun(Run run)
    {
        var calculatedCount = 0;

        // Пытаемся распространить от начала к концу
        if (run.StartPoint?.Height.IsKnown == true)
        {
            Height currentHeight = run.StartPoint.Height;

            foreach (var obs in run.Observations)
            {
                if (!obs.To.Height.IsKnown)
                {
                    var newHeight = Height.Known(currentHeight.Value - obs.AdjustedDeltaH);
                    obs.To.SetCalculatedHeight(newHeight);
                    calculatedCount++;
                }
                currentHeight = obs.To.Height;
            }
        }
        // Или от конца к началу
        else if (run.EndPoint?.Height.IsKnown == true)
        {
            Height currentHeight = run.EndPoint.Height;

            for (int i = run.Observations.Count - 1; i >= 0; i--)
            {
                var obs = run.Observations[i];
                if (!obs.From.Height.IsKnown)
                {
                    var newHeight = Height.Known(currentHeight.Value + obs.AdjustedDeltaH);
                    obs.From.SetCalculatedHeight(newHeight);
                    calculatedCount++;
                }
                currentHeight = obs.From.Height;
            }
        }

        return calculatedCount;
    }
}
