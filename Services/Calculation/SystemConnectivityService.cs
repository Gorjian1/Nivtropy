using System.Collections.Generic;
using System.Linq;
using Nivtropy.Models;
using Nivtropy.ViewModels.Managers;

namespace Nivtropy.Services.Calculation
{
    /// <summary>
    /// Результат анализа связности ходов
    /// </summary>
    public class ConnectivityResult
    {
        /// <summary>
        /// Словарь: индекс хода → ID системы
        /// </summary>
        public Dictionary<int, string> RunToSystemId { get; } = new();

        /// <summary>
        /// Список новых систем для создания (ID, Name, Order)
        /// </summary>
        public List<(string Id, string Name, int Order)> NewSystems { get; } = new();

        /// <summary>
        /// ID автосистем, которые нужно удалить
        /// </summary>
        public List<string> SystemsToRemove { get; } = new();
    }

    /// <summary>
    /// Интерфейс сервиса анализа связности ходов
    /// </summary>
    public interface ISystemConnectivityService
    {
        /// <summary>
        /// Анализирует связность ходов через общие точки и определяет системы
        /// </summary>
        /// <param name="runs">Список ходов</param>
        /// <param name="sharedPoints">Список общих точек (только включённые учитываются)</param>
        /// <param name="existingAutoSystemIds">ID существующих автосистем</param>
        /// <returns>Результат анализа связности</returns>
        ConnectivityResult AnalyzeConnectivity(
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds);
    }

    /// <summary>
    /// Сервис анализа связности ходов через общие точки
    /// </summary>
    public class SystemConnectivityService : ISystemConnectivityService
    {
        public ConnectivityResult AnalyzeConnectivity(
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints,
            IReadOnlyList<string> existingAutoSystemIds)
        {
            var result = new ConnectivityResult();

            if (runs.Count == 0)
                return result;

            // Строим граф связности: ход -> список связанных ходов через включённые общие точки
            var adjacency = BuildAdjacencyGraph(runs, sharedPoints);

            // Находим компоненты связности через BFS
            var components = FindConnectedComponents(runs, adjacency);

            // Если все ходы в одной компоненте - все в основной системе
            if (components.Count <= 1)
            {
                foreach (var run in runs)
                {
                    result.RunToSystemId[run.Index] = TraverseSystemsManager.DEFAULT_SYSTEM_ID;
                }

                // Помечаем все автосистемы на удаление
                result.SystemsToRemove.AddRange(existingAutoSystemIds);
                return result;
            }

            // Назначаем системы для каждой компоненты
            // Первая (самая большая) компонента остаётся в основной системе
            var sortedComponents = components.OrderByDescending(c => c.Count).ToList();
            var usedAutoSystemIds = new HashSet<string>();

            for (int i = 0; i < sortedComponents.Count; i++)
            {
                var component = sortedComponents[i];
                string systemId;

                if (i == 0)
                {
                    // Самая большая компонента - в основную систему
                    systemId = TraverseSystemsManager.DEFAULT_SYSTEM_ID;
                }
                else
                {
                    // Создаём или используем дополнительную систему
                    systemId = $"system-auto-{i}";
                    usedAutoSystemIds.Add(systemId);

                    if (!existingAutoSystemIds.Contains(systemId))
                    {
                        result.NewSystems.Add((systemId, $"Система {i + 1}", i + 1));
                    }
                }

                // Назначаем ходам систему
                foreach (var runIndex in component)
                {
                    result.RunToSystemId[runIndex] = systemId;
                }
            }

            // Определяем автосистемы для удаления (те, что не используются)
            foreach (var existingId in existingAutoSystemIds)
            {
                if (!usedAutoSystemIds.Contains(existingId))
                {
                    result.SystemsToRemove.Add(existingId);
                }
            }

            return result;
        }

        private static Dictionary<int, HashSet<int>> BuildAdjacencyGraph(
            IReadOnlyList<LineSummary> runs,
            IReadOnlyList<SharedPointLinkItem> sharedPoints)
        {
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var run in runs)
            {
                adjacency[run.Index] = new HashSet<int>();
            }

            // Для каждой включённой общей точки добавляем рёбра между ходами
            foreach (var sp in sharedPoints.Where(p => p.IsEnabled))
            {
                var runsWithPoint = runs
                    .Where(r => sp.IsUsedInRun(r.Index))
                    .Select(r => r.Index)
                    .ToList();

                // Связываем все ходы, использующие эту точку
                for (int i = 0; i < runsWithPoint.Count; i++)
                {
                    for (int j = i + 1; j < runsWithPoint.Count; j++)
                    {
                        adjacency[runsWithPoint[i]].Add(runsWithPoint[j]);
                        adjacency[runsWithPoint[j]].Add(runsWithPoint[i]);
                    }
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
                if (visited.Contains(run.Index))
                    continue;

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
