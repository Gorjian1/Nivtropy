using System.Collections.Generic;
using System.Linq;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModels.Managers;

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
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds);
    }

    public class SystemConnectivityService : ISystemConnectivityService
    {
        public ConnectivityResult AnalyzeConnectivity(
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds)
        {
            var result = new ConnectivityResult();
            if (runs.Count == 0) return result;

            var adjacency = BuildAdjacencyGraph(runs, sharedPoints);
            var components = FindConnectedComponents(runs, adjacency);

            if (components.Count <= 1)
            {
                foreach (var run in runs)
                    result.RunToSystemId[run.Index] = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
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
                    systemId = ITraverseSystemsManager.DEFAULT_SYSTEM_ID;
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
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints)
        {
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var run in runs)
                adjacency[run.Index] = new HashSet<int>();

            foreach (var sp in sharedPoints.Where(p => p.IsEnabled))
            {
                var runsWithPoint = runs.Where(r => sp.IsUsedInRun(r.Index)).Select(r => r.Index).ToList();
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
            IReadOnlyList<LineSummary> runs,
            Dictionary<int, HashSet<int>> adjacency)
        {
            var visited = new HashSet<int>();
            var components = new List<List<int>>();

            foreach (var run in runs)
            {
                if (visited.Contains(run.Index)) continue;
                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(run.Index);
                visited.Add(run.Index);

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
