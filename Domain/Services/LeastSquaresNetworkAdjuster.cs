namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

public class LeastSquaresNetworkAdjuster : INetworkAdjuster
{
    public NetworkAdjustmentResult Adjust(LevelingNetwork network)
    {
        var activeObservations = network.Runs
            .Where(r => r.IsActive)
            .SelectMany(r => r.Observations)
            .ToList();

        if (activeObservations.Count == 0)
            return new NetworkAdjustmentResult(false, "Нет активных наблюдений для уравнивания сети.");

        var componentMap = BuildComponents(activeObservations);
        if (componentMap.Count == 0)
            return new NetworkAdjustmentResult(false, "Недостаточно данных для уравнивания сети.");

        var componentHasBenchmark = componentMap
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(kvp => kvp.Key).Any(p => p.Height.IsKnown));

        if (!componentHasBenchmark.Values.Any(v => v))
            return new NetworkAdjustmentResult(false, "Для уравнивания сети нужен хотя бы один репер.");

        var unknownIndex = new Dictionary<Point, int>();
        foreach (var (point, componentId) in componentMap)
        {
            if (!componentHasBenchmark.GetValueOrDefault(componentId))
                continue;

            if (!point.Height.IsKnown)
                unknownIndex[point] = unknownIndex.Count;
        }

        var eligibleObservations = activeObservations
            .Where(obs =>
                componentMap.TryGetValue(obs.From, out var fromComponent) &&
                componentMap.TryGetValue(obs.To, out var toComponent) &&
                fromComponent == toComponent &&
                componentHasBenchmark.GetValueOrDefault(fromComponent))
            .ToList();

        if (eligibleObservations.Count == 0)
            return new NetworkAdjustmentResult(false, "Нет наблюдений, связанных с реперами.");

        double[]? solvedHeights = null;
        if (unknownIndex.Count > 0)
        {
            solvedHeights = SolveLeastSquares(eligibleObservations, unknownIndex);
            if (solvedHeights == null)
                return new NetworkAdjustmentResult(false, "Матричная система уравнивания вырождена.");
        }

        var appliedCorrections = 0;
        foreach (var obs in eligibleObservations)
        {
            var fromHeight = obs.From.Height.IsKnown
                ? obs.From.Height.Value
                : solvedHeights?[unknownIndex[obs.From]] ?? 0.0;

            var toHeight = obs.To.Height.IsKnown
                ? obs.To.Height.Value
                : solvedHeights?[unknownIndex[obs.To]] ?? 0.0;

            var targetDelta = toHeight - fromHeight;
            var correction = targetDelta - obs.DeltaH;
            obs.ApplyCorrection(correction);
            appliedCorrections++;
        }

        if (appliedCorrections == 0)
            return new NetworkAdjustmentResult(false, "Поправки не были применены.");

        var message = solvedHeights == null
            ? "Сетевое уравнивание выполнено на основе известных высот."
            : $"Сетевое уравнивание выполнено: оценено неизвестных высот {unknownIndex.Count}.";

        return new NetworkAdjustmentResult(true, message);
    }

    private static Dictionary<Point, int> BuildComponents(IReadOnlyList<Observation> observations)
    {
        var adjacency = new Dictionary<Point, HashSet<Point>>();
        foreach (var obs in observations)
        {
            if (!adjacency.TryGetValue(obs.From, out var fromNeighbors))
            {
                fromNeighbors = new HashSet<Point>();
                adjacency[obs.From] = fromNeighbors;
            }

            if (!adjacency.TryGetValue(obs.To, out var toNeighbors))
            {
                toNeighbors = new HashSet<Point>();
                adjacency[obs.To] = toNeighbors;
            }

            fromNeighbors.Add(obs.To);
            toNeighbors.Add(obs.From);
        }

        var componentMap = new Dictionary<Point, int>();
        var componentId = 0;

        foreach (var point in adjacency.Keys)
        {
            if (componentMap.ContainsKey(point))
                continue;

            var queue = new Queue<Point>();
            queue.Enqueue(point);
            componentMap[point] = componentId;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out var neighbors))
                    continue;

                foreach (var neighbor in neighbors)
                {
                    if (componentMap.ContainsKey(neighbor))
                        continue;

                    componentMap[neighbor] = componentId;
                    queue.Enqueue(neighbor);
                }
            }

            componentId++;
        }

        return componentMap;
    }

    private static double[]? SolveLeastSquares(
        IReadOnlyList<Observation> observations,
        IReadOnlyDictionary<Point, int> unknownIndex)
    {
        var unknownCount = unknownIndex.Count;
        var normal = new double[unknownCount, unknownCount];
        var rhs = new double[unknownCount];

        foreach (var obs in observations)
        {
            var entries = new List<(int Index, double Coefficient)>();

            var l = obs.DeltaH;

            if (obs.From.Height.IsKnown)
            {
                l += obs.From.Height.Value;
            }
            else if (unknownIndex.TryGetValue(obs.From, out var fromIndex))
            {
                entries.Add((fromIndex, -1));
            }

            if (obs.To.Height.IsKnown)
            {
                l -= obs.To.Height.Value;
            }
            else if (unknownIndex.TryGetValue(obs.To, out var toIndex))
            {
                entries.Add((toIndex, 1));
            }

            if (entries.Count == 0)
                continue;

            var weight = GetWeight(obs);

            foreach (var (indexI, coeffI) in entries)
            {
                rhs[indexI] += weight * coeffI * l;

                foreach (var (indexJ, coeffJ) in entries)
                {
                    normal[indexI, indexJ] += weight * coeffI * coeffJ;
                }
            }
        }

        return SolveLinearSystem(normal, rhs);
    }

    private static double GetWeight(Observation observation)
    {
        var length = observation.StationLength.Meters;
        if (length <= 0)
            return 1.0;

        return 1.0 / length;
    }

    private static double[]? SolveLinearSystem(double[,] matrix, double[] vector)
    {
        var size = vector.Length;
        var augmented = new double[size, size + 1];

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
                augmented[i, j] = matrix[i, j];
            augmented[i, size] = vector[i];
        }

        const double epsilon = 1e-12;

        for (var column = 0; column < size; column++)
        {
            var pivotRow = column;
            var pivotValue = Math.Abs(augmented[pivotRow, column]);

            for (var row = column + 1; row < size; row++)
            {
                var value = Math.Abs(augmented[row, column]);
                if (value > pivotValue)
                {
                    pivotValue = value;
                    pivotRow = row;
                }
            }

            if (pivotValue < epsilon)
                return null;

            if (pivotRow != column)
            {
                for (var col = column; col <= size; col++)
                {
                    var temp = augmented[column, col];
                    augmented[column, col] = augmented[pivotRow, col];
                    augmented[pivotRow, col] = temp;
                }
            }

            var pivot = augmented[column, column];
            for (var col = column; col <= size; col++)
                augmented[column, col] /= pivot;

            for (var row = 0; row < size; row++)
            {
                if (row == column)
                    continue;

                var factor = augmented[row, column];
                if (Math.Abs(factor) < epsilon)
                    continue;

                for (var col = column; col <= size; col++)
                    augmented[row, col] -= factor * augmented[column, col];
            }
        }

        var solution = new double[size];
        for (var i = 0; i < size; i++)
            solution[i] = augmented[i, size];

        return solution;
    }
}
