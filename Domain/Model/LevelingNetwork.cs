namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Нивелирная сеть - главный Aggregate Root.
/// Представляет граф точек (узлов) и наблюдений (рёбер).
/// </summary>
public class LevelingNetwork
{
    private readonly Dictionary<PointCode, Point> _points = new();
    private readonly List<Run> _runs = new();
    private readonly List<TraverseSystem> _systems = new();

    /// <summary>Уникальный идентификатор сети</summary>
    public Guid Id { get; }

    /// <summary>Название проекта</summary>
    public string Name { get; set; }

    /// <summary>Дата создания</summary>
    public DateTime CreatedAt { get; }

    // Коллекции
    /// <summary>Все точки сети (узлы графа)</summary>
    public IReadOnlyDictionary<PointCode, Point> Points => _points;

    /// <summary>Все ходы сети</summary>
    public IReadOnlyList<Run> Runs => _runs;

    /// <summary>Системы ходов</summary>
    public IReadOnlyList<TraverseSystem> Systems => _systems;

    // Агрегированные данные
    /// <summary>Реперы (точки с известной высотой)</summary>
    public IEnumerable<Point> Benchmarks =>
        _points.Values.Where(p => p.Type == PointType.Benchmark);

    /// <summary>Общие точки (связывают несколько ходов)</summary>
    public IEnumerable<Point> SharedPoints =>
        _points.Values.Where(p => p.IsSharedPoint);

    /// <summary>Связующие точки</summary>
    public IEnumerable<Point> TurningPoints =>
        _points.Values.Where(p => p.Type == PointType.TurningPoint);

    /// <summary>Все наблюдения сети</summary>
    public IEnumerable<Observation> AllObservations =>
        _runs.SelectMany(r => r.Observations);

    /// <summary>Общее количество станций</summary>
    public int TotalStationCount => _runs.Sum(r => r.StationCount);

    /// <summary>Общая длина всех ходов</summary>
    public Distance TotalLength => _runs.Aggregate(
        Distance.Zero,
        (sum, run) => sum + run.TotalLength);

    public LevelingNetwork(string name = "Новый проект")
    {
        Id = Guid.NewGuid();
        Name = name;
        CreatedAt = DateTime.UtcNow;
    }

    #region Управление точками

    /// <summary>Получить или создать точку</summary>
    public Point GetOrCreatePoint(PointCode code)
    {
        if (!_points.TryGetValue(code, out var point))
        {
            point = new Point(code);
            _points[code] = point;
        }
        return point;
    }

    /// <summary>Получить точку (если существует)</summary>
    public Point? GetPoint(PointCode code) =>
        _points.GetValueOrDefault(code);

    /// <summary>Установить высоту репера</summary>
    public void SetBenchmarkHeight(PointCode code, Height height)
    {
        var point = GetOrCreatePoint(code);
        point.SetKnownHeight(height);
    }

    /// <summary>Сбросить все вычисленные высоты</summary>
    public void ResetCalculatedHeights()
    {
        foreach (var point in _points.Values)
            point.ResetCalculatedHeight();
    }

    /// <summary>Сбросить все поправки наблюдений</summary>
    public void ResetCorrections()
    {
        foreach (var run in _runs)
            run.ResetCorrections();
    }

    #endregion

    #region Управление ходами

    /// <summary>Создать новый ход</summary>
    public Run CreateRun(string name)
    {
        var run = new Run(name);
        _runs.Add(run);
        return run;
    }

    /// <summary>Добавить наблюдение в ход</summary>
    public Observation AddObservation(
        Run run,
        PointCode fromCode,
        PointCode toCode,
        Reading backReading,
        Reading foreReading,
        Distance backDistance,
        Distance foreDistance)
    {
        if (!_runs.Contains(run))
            throw new ArgumentException("Run does not belong to this network");

        var from = GetOrCreatePoint(fromCode);
        var to = GetOrCreatePoint(toCode);

        var observation = new Observation(
            from: from,
            to: to,
            run: run,
            stationIndex: run.StationCount + 1,
            backReading: backReading,
            foreReading: foreReading,
            backDistance: backDistance,
            foreDistance: foreDistance);

        // Добавляем связи в граф
        from.AddOutgoingObservation(observation);
        to.AddIncomingObservation(observation);

        // Добавляем в ход
        run.AddObservation(observation);

        return observation;
    }

    /// <summary>Удалить ход</summary>
    public void RemoveRun(Run run)
    {
        if (!_runs.Remove(run))
            return;

        // Удаляем связи из точек
        foreach (var obs in run.Observations)
        {
            obs.From.RemoveObservation(obs);
            obs.To.RemoveObservation(obs);
        }

        // Удаляем осиротевшие точки (без связей)
        var orphanedPoints = _points.Values
            .Where(p => p.Degree == 0 && p.Type != PointType.Benchmark)
            .Select(p => p.Code)
            .ToList();

        foreach (var code in orphanedPoints)
            _points.Remove(code);
    }

    #endregion

    #region Управление системами

    /// <summary>Создать систему ходов</summary>
    public TraverseSystem CreateSystem(string name)
    {
        var system = new TraverseSystem(name, _systems.Count);
        _systems.Add(system);
        return system;
    }

    /// <summary>Добавить ход в систему</summary>
    public void AddRunToSystem(Run run, TraverseSystem system)
    {
        if (!_systems.Contains(system))
            throw new ArgumentException("System does not belong to this network");

        run.System?.RemoveRun(run);
        system.AddRun(run);
    }

    #endregion

    #region Граф-операции

    /// <summary>Найти все ходы, проходящие через точку</summary>
    public IEnumerable<Run> GetRunsContainingPoint(PointCode code)
    {
        var point = GetPoint(code);
        return point?.ConnectedRuns ?? Enumerable.Empty<Run>();
    }

    /// <summary>Проверить связность сети</summary>
    public bool IsConnected()
    {
        if (_points.Count == 0)
            return true;

        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        queue.Enqueue(_points.Values.First());

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current))
                continue;

            visited.Add(current);

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return visited.Count == _points.Count;
    }

    /// <summary>Найти компоненты связности</summary>
    public IEnumerable<IReadOnlyList<Point>> FindConnectedComponents()
    {
        var visited = new HashSet<Point>();
        var components = new List<List<Point>>();

        foreach (var startPoint in _points.Values)
        {
            if (visited.Contains(startPoint))
                continue;

            var component = new List<Point>();
            var queue = new Queue<Point>();
            queue.Enqueue(startPoint);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (visited.Contains(current))
                    continue;

                visited.Add(current);
                component.Add(current);

                foreach (var neighbor in current.AdjacentPoints)
                {
                    if (!visited.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            components.Add(component);
        }

        return components;
    }

    /// <summary>Найти путь между точками (BFS)</summary>
    public IEnumerable<Observation>? FindPath(PointCode from, PointCode to)
    {
        var startPoint = GetPoint(from);
        var endPoint = GetPoint(to);

        if (startPoint == null || endPoint == null)
            return null;

        if (from.Equals(to))
            return Enumerable.Empty<Observation>();

        var visited = new HashSet<Point>();
        var queue = new Queue<(Point point, List<Observation> path)>();

        queue.Enqueue((startPoint, new List<Observation>()));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            if (visited.Contains(current))
                continue;

            visited.Add(current);

            // Проверяем исходящие наблюдения
            foreach (var obs in current.OutgoingObservations)
            {
                var newPath = new List<Observation>(path) { obs };

                if (obs.To.Code.Equals(to))
                    return newPath;

                if (!visited.Contains(obs.To))
                    queue.Enqueue((obs.To, newPath));
            }

            // Проверяем входящие наблюдения (обратное направление)
            foreach (var obs in current.IncomingObservations)
            {
                var newPath = new List<Observation>(path) { obs };

                if (obs.From.Code.Equals(to))
                    return newPath;

                if (!visited.Contains(obs.From))
                    queue.Enqueue((obs.From, newPath));
            }
        }

        return null; // Путь не найден
    }

    /// <summary>Найти циклы в сети (замкнутые полигоны)</summary>
    public IEnumerable<IReadOnlyList<Point>> FindCycles()
    {
        // DFS cycle detection с оптимизированными структурами данных
        var cycles = new List<List<Point>>();
        var visited = new HashSet<Point>();
        var inStack = new HashSet<Point>();  // O(1) для Contains
        var stack = new List<Point>();       // Для восстановления пути цикла

        void DFS(Point current, Point? parent)
        {
            visited.Add(current);
            inStack.Add(current);
            stack.Add(current);

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                {
                    DFS(neighbor, current);
                }
                else if (neighbor != parent && inStack.Contains(neighbor))
                {
                    // Найден цикл - извлекаем его из стека
                    var cycleStart = stack.IndexOf(neighbor);
                    var cycle = stack.Skip(cycleStart).ToList();
                    cycles.Add(cycle);
                }
            }

            stack.RemoveAt(stack.Count - 1);  // O(1) удаление с конца
            inStack.Remove(current);
        }

        foreach (var point in _points.Values)
        {
            if (!visited.Contains(point))
                DFS(point, null);
        }

        return cycles;
    }

    /// <summary>Топологическая сортировка от реперов (для распространения высот)</summary>
    public IEnumerable<Point> TopologicalSortFromBenchmarks()
    {
        var result = new List<Point>();
        var visited = new HashSet<Point>();
        var queue = new Queue<Point>();

        // Начинаем с реперов
        foreach (var benchmark in Benchmarks)
        {
            queue.Enqueue(benchmark);
            visited.Add(benchmark);
            result.Add(benchmark);
        }

        // BFS обход
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in current.AdjacentPoints)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    result.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return result;
    }

    #endregion

    public override string ToString() =>
        $"LevelingNetwork({Name}, {_points.Count} points, {_runs.Count} runs)";
}
