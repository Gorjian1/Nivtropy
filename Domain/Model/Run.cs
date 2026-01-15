namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Нивелирный ход - Aggregate Root.
/// Упорядоченная последовательность наблюдений (станций).
/// </summary>
public class Run
{
    private readonly List<Observation> _observations = new();

    /// <summary>Уникальный идентификатор</summary>
    public Guid Id { get; }

    /// <summary>Название хода (например "Ход 01")</summary>
    public string Name { get; private set; }

    /// <summary>Оригинальный номер из файла</summary>
    public string? OriginalNumber { get; set; }

    /// <summary>Активен ли ход</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Система, к которой принадлежит ход</summary>
    public TraverseSystem? System { get; internal set; }

    /// <summary>Наблюдения (станции) хода</summary>
    public IReadOnlyList<Observation> Observations => _observations;

    /// <summary>Количество станций</summary>
    public int StationCount => _observations.Count;

    // Граничные точки
    /// <summary>Начальная точка хода</summary>
    public Point? StartPoint => _observations.FirstOrDefault()?.From;

    /// <summary>Конечная точка хода</summary>
    public Point? EndPoint => _observations.LastOrDefault()?.To;

    /// <summary>Все точки хода в порядке прохождения</summary>
    public IEnumerable<Point> Points
    {
        get
        {
            if (_observations.Count == 0)
                yield break;

            yield return _observations[0].From;
            foreach (var obs in _observations)
                yield return obs.To;
        }
    }

    // Агрегированные характеристики
    /// <summary>Общая длина хода</summary>
    public Distance TotalLength => _observations.Aggregate(
        Distance.Zero,
        (sum, obs) => sum + obs.StationLength);

    /// <summary>Сумма превышений</summary>
    public double DeltaHSum => _observations.Sum(o => o.DeltaH);

    /// <summary>Сумма исправленных превышений</summary>
    public double AdjustedDeltaHSum => _observations.Sum(o => o.AdjustedDeltaH);

    /// <summary>Накопленная разность плеч</summary>
    public double AccumulatedArmDifference => _observations.Sum(o => o.ArmDifference);

    // Невязка
    /// <summary>Невязка хода (если замкнут или между реперами)</summary>
    public Closure? Closure { get; private set; }

    /// <summary>Является ли ход замкнутым (начало = конец)</summary>
    public bool IsClosed => StartPoint != null &&
                            EndPoint != null &&
                            StartPoint.Code.Equals(EndPoint.Code);

    /// <summary>Есть ли известные точки на концах</summary>
    public bool HasKnownEndPoints =>
        (StartPoint?.Height.IsKnown ?? false) &&
        (EndPoint?.Height.IsKnown ?? false);

    public Run(string name)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>Переименовать ход</summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        Name = newName;
    }

    /// <summary>Добавить наблюдение в ход</summary>
    internal void AddObservation(Observation observation)
    {
        if (observation.Run != this)
            throw new ArgumentException("Observation belongs to different run");

        // Проверяем связность: новое наблюдение должно начинаться там, где закончилось предыдущее
        if (_observations.Count > 0)
        {
            var lastTo = _observations[^1].To;
            if (!observation.From.Code.Equals(lastTo.Code))
                throw new InvalidOperationException(
                    $"Observation must start at {lastTo.Code}, but starts at {observation.From.Code}");
        }

        _observations.Add(observation);
    }

    /// <summary>Вычислить невязку</summary>
    public void CalculateClosure(double toleranceMm)
    {
        if (!HasKnownEndPoints)
        {
            Closure = null;
            return;
        }

        // Теоретическое превышение
        var theoretical = StartPoint!.Height - EndPoint!.Height;

        // Измеренное превышение
        var measured = DeltaHSum;

        // Невязка в мм
        var closureMm = (measured - theoretical) * 1000;

        Closure = new Closure(closureMm, toleranceMm);
    }

    /// <summary>Проверить, содержит ли ход точку</summary>
    public bool ContainsPoint(PointCode code) =>
        Points.Any(p => p.Code.Equals(code));

    /// <summary>Получить точку по коду</summary>
    public Point? GetPoint(PointCode code) =>
        Points.FirstOrDefault(p => p.Code.Equals(code));

    /// <summary>Активировать ход</summary>
    public void Activate() => IsActive = true;

    /// <summary>Деактивировать ход</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Сбросить все поправки</summary>
    public void ResetCorrections()
    {
        foreach (var obs in _observations)
            obs.ResetCorrection();
    }

    public override string ToString() =>
        $"Run({Name}, {StationCount} stations, {TotalLength})";
}
