namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

/// <summary>
/// Вычисляет снимок высот для сети без изменения состояния точек.
/// </summary>
public sealed class HeightSnapshotCalculator
{
    public Dictionary<Point, double> ComputeHeightsSnapshot(LevelingNetwork network, bool useCorrections)
    {
        var heights = new Dictionary<Point, double>();
        var queue = new Queue<Point>();

        foreach (var benchmark in network.Benchmarks)
        {
            if (!benchmark.Height.IsKnown)
                continue;

            heights[benchmark] = benchmark.Height.Value;
            queue.Enqueue(benchmark);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!heights.TryGetValue(current, out var currentHeight))
                continue;

            foreach (var obs in current.OutgoingObservations)
            {
                var next = obs.To;
                if (heights.ContainsKey(next))
                    continue;

                var delta = useCorrections ? obs.AdjustedDeltaH : obs.DeltaH;
                heights[next] = currentHeight + delta;
                queue.Enqueue(next);
            }

            foreach (var obs in current.IncomingObservations)
            {
                var next = obs.From;
                if (heights.ContainsKey(next))
                    continue;

                var delta = useCorrections ? obs.AdjustedDeltaH : obs.DeltaH;
                heights[next] = currentHeight - delta;
                queue.Enqueue(next);
            }
        }

        return heights;
    }
}
