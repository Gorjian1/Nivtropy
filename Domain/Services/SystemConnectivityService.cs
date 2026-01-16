using System.Collections.Generic;
using System.Linq;

namespace Nivtropy.Domain.Services
{
    public class ConnectivityResult
    {
        public Dictionary<int, string> RunToSystemId { get; } = new();
        public List<(string Id, string Name, int Order)> NewSystems { get; } = new();
        public List<string> SystemsToRemove { get; } = new();
    }

    public interface ISystemConnectivityService
    {
        ConnectivityResult AnalyzeConnectivity(
            IReadOnlyList<int> runIndexes,
            IReadOnlyList<SharedPointLink> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds,
            string defaultSystemId);
    }

    public class SystemConnectivityService : ISystemConnectivityService
    {
        public ConnectivityResult AnalyzeConnectivity(
            IReadOnlyList<int> runIndexes,
            IReadOnlyList<SharedPointLink> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds,
            string defaultSystemId)
        {
            var result = new ConnectivityResult();
            if (runIndexes.Count == 0) return result;

            var adjacency = BuildAdjacencyGraph(runIndexes, sharedPoints);
            var components = FindConnectedComponents(runIndexes, adjacency);

            if (components.Count <= 1)
            {
                foreach (var runIndex in runIndexes)
                    result.RunToSystemId[runIndex] = defaultSystemId;
                result.SystemsToRemove.AddRange(existingAutoSystemIds);
                return result;
            }

            var sortedComponents = components.OrderByDescending(c => c.Count).ToList();
            var usedAutoSystemIds = new HashSet<string>();

            for (int i = 0; i < sortedComponents.Count; i++)
            {
                var component = sortedComponents[i];
                string systemId;

                if (i == 0)
                {
                    systemId = defaultSystemId;
                }
                else
                {
                    systemId = $"system-auto-{i}";
                    usedAutoSystemIds.Add(systemId);
                    if (!existingAutoSystemIds.Contains(systemId))
                        result.NewSystems.Add((systemId, $"Система {i + 1}", i + 1));
                }

                foreach (var runIndex in component)
                    result.RunToSystemId[runIndex] = systemId;
            }

            foreach (var existingId in existingAutoSystemIds)
                if (!usedAutoSystemIds.Contains(existingId))
                    result.SystemsToRemove.Add(existingId);

            return result;
        }

        private static Dictionary<int, HashSet<int>> BuildAdjacencyGraph(
            IReadOnlyList<int> runIndexes,
            IReadOnlyList<SharedPointLink> sharedPoints)
        {
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var runIndex in runIndexes)
                adjacency[runIndex] = new HashSet<int>();

            foreach (var sp in sharedPoints.Where(p => p.IsEnabled))
            {
                var runsWithPoint = sp.RunIndexes.Where(adjacency.ContainsKey).ToList();
                for (int i = 0; i < runsWithPoint.Count; i++)
                    for (int j = i + 1; j < runsWithPoint.Count; j++)
                    {
                        adjacency[runsWithPoint[i]].Add(runsWithPoint[j]);
                        adjacency[runsWithPoint[j]].Add(runsWithPoint[i]);
                    }
            }
            return adjacency;
        }

        private static List<List<int>> FindConnectedComponents(
            IReadOnlyList<int> runIndexes,
            Dictionary<int, HashSet<int>> adjacency)
        {
            var visited = new HashSet<int>();
            var components = new List<List<int>>();

            foreach (var runIndex in runIndexes)
            {
                if (visited.Contains(runIndex)) continue;
                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(runIndex);
                visited.Add(runIndex);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);
                    foreach (var neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                components.Add(component);
            }
            return components;
        }
    }
}
