namespace Nivtropy.Domain.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

public class LeastSquaresNetworkAdjuster : INetworkAdjuster
{
    public NetworkAdjustmentResult Adjust(LevelingNetwork network)
    {
        if (network.AllObservations == null || !network.AllObservations.Any())
            return new NetworkAdjustmentResult(false, "Сетевое уравнивание не выполнено: нет наблюдений.");

        var points = network.Points.Values.ToList();
        var knownHeights = new Dictionary<Point, double>();

        foreach (var point in points)
        {
            if (point.Height.IsKnown)
                knownHeights[point] = point.Height.Value;
        }

        if (knownHeights.Count == 0 && points.Count > 0)
        {
            knownHeights[points[0]] = 0.0;
        }

        var unknownPoints = points.Where(p => !knownHeights.ContainsKey(p)).ToList();
        var indexByPoint = new Dictionary<Point, int>();
        for (int i = 0; i < unknownPoints.Count; i++)
        {
            indexByPoint[unknownPoints[i]] = i;
        }

        var normalMatrix = new double[unknownPoints.Count, unknownPoints.Count];
        var rightHandSide = new double[unknownPoints.Count];

        foreach (var observation in network.AllObservations)
        {
            var deltaH = observation.DeltaH;
            var b = deltaH;

            if (knownHeights.TryGetValue(observation.From, out var fromKnown))
                b += fromKnown;
            if (knownHeights.TryGetValue(observation.To, out var toKnown))
                b -= toKnown;

            var weight = CalculateWeight(observation.StationLength);

            var hasFromUnknown = indexByPoint.TryGetValue(observation.From, out var fromIndex);
            var hasToUnknown = indexByPoint.TryGetValue(observation.To, out var toIndex);

            if (!hasFromUnknown && !hasToUnknown)
                continue;

            if (hasFromUnknown)
            {
                normalMatrix[fromIndex, fromIndex] += weight;
                rightHandSide[fromIndex] += weight * b * -1.0;
            }

            if (hasToUnknown)
            {
                normalMatrix[toIndex, toIndex] += weight;
                rightHandSide[toIndex] += weight * b;
            }

            if (hasFromUnknown && hasToUnknown)
            {
                normalMatrix[fromIndex, toIndex] += -weight;
                normalMatrix[toIndex, fromIndex] += -weight;
            }
        }

        if (!SolveLinearSystem(normalMatrix, rightHandSide, out var solution))
        {
            return new NetworkAdjustmentResult(false, "Сетевое уравнивание не выполнено: система вырождена.");
        }

        var adjustedHeights = new Dictionary<Point, double>(knownHeights);
        for (int i = 0; i < unknownPoints.Count; i++)
        {
            adjustedHeights[unknownPoints[i]] = solution[i];
        }

        foreach (var point in points)
        {
            if (!adjustedHeights.TryGetValue(point, out var height))
                continue;

            if (point.Type == PointType.Benchmark)
                continue;

            point.SetCalculatedHeight(Height.Known(height));
        }

        foreach (var observation in network.AllObservations)
        {
            if (!adjustedHeights.TryGetValue(observation.From, out var fromHeight) ||
                !adjustedHeights.TryGetValue(observation.To, out var toHeight))
                continue;

            var correction = (toHeight - fromHeight) - observation.DeltaH;
            observation.ApplyCorrection(correction);
        }

        return new NetworkAdjustmentResult(true, "Сетевое уравнивание выполнено.");
    }

    private static double CalculateWeight(Distance stationLength)
    {
        var length = stationLength.Meters;
        if (length <= 0)
            return 1.0;

        return 1.0 / length;
    }

    private static bool SolveLinearSystem(double[,] matrix, double[] rhs, out double[] solution)
    {
        int n = rhs.Length;
        solution = new double[n];

        for (int k = 0; k < n; k++)
        {
            int pivotRow = k;
            double pivotValue = Math.Abs(matrix[k, k]);

            for (int i = k + 1; i < n; i++)
            {
                var candidate = Math.Abs(matrix[i, k]);
                if (candidate > pivotValue)
                {
                    pivotValue = candidate;
                    pivotRow = i;
                }
            }

            if (pivotValue < 1e-12)
                return false;

            if (pivotRow != k)
            {
                for (int j = k; j < n; j++)
                {
                    (matrix[k, j], matrix[pivotRow, j]) = (matrix[pivotRow, j], matrix[k, j]);
                }
                (rhs[k], rhs[pivotRow]) = (rhs[pivotRow], rhs[k]);
            }

            for (int i = k + 1; i < n; i++)
            {
                var factor = matrix[i, k] / matrix[k, k];
                if (Math.Abs(factor) < 1e-12)
                    continue;

                for (int j = k; j < n; j++)
                {
                    matrix[i, j] -= factor * matrix[k, j];
                }
                rhs[i] -= factor * rhs[k];
            }
        }

        for (int i = n - 1; i >= 0; i--)
        {
            double sum = rhs[i];
            for (int j = i + 1; j < n; j++)
            {
                sum -= matrix[i, j] * solution[j];
            }

            solution[i] = sum / matrix[i, i];
        }

        return true;
    }
}
