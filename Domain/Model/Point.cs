namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Точка нивелирования - узел графа.
/// Содержит ссылки на входящие и исходящие наблюдения (рёбра).
/// </summary>
public class Point : IEquatable<Point>
{
    private readonly List<Observation> _outgoingObservations = new();
    private readonly List<Observation> _incomingObservations = new();

    /// <summary>Уникальный код точки</summary>
    public PointCode Code { get; }

    /// <summary>Высота точки</summary>
    public Height Height { get; private set; }

    /// <summary>Тип точки</summary>
    public PointType Type { get; private set; }

    /// <summary>Исходящие наблюдения (эта точка - задняя)</summary>
    public IReadOnlyList<Observation> OutgoingObservations => _outgoingObservations;

    /// <summary>Входящие наблюдения (эта точка - передняя)</summary>
    public IReadOnlyList<Observation> IncomingObservations => _incomingObservations;

    /// <summary>Все наблюдения, связанные с точкой</summary>
    public IEnumerable<Observation> AllObservations =>
        _outgoingObservations.Concat(_incomingObservations);

    /// <summary>Степень вершины (количество связей)</summary>
    public int Degree => _outgoingObservations.Count + _incomingObservations.Count;

    /// <summary>Является ли точка общей (связывает несколько ходов)</summary>
    public bool IsSharedPoint => AllObservations
        .Select(o => o.Run)
        .Distinct()
        .Count() > 1;

    /// <summary>Соседние точки в графе</summary>
    public IEnumerable<Point> AdjacentPoints =>
        _outgoingObservations.Select(o => o.To)
        .Concat(_incomingObservations.Select(o => o.From))
        .Distinct();

    /// <summary>Ходы, проходящие через эту точку</summary>
    public IEnumerable<Run> ConnectedRuns =>
        AllObservations.Select(o => o.Run).Distinct();

    public Point(PointCode code, PointType type = PointType.TurningPoint)
    {
        Code = code;
        Type = type;
        Height = Height.Unknown;
    }

    /// <summary>Установить известную высоту (для реперов)</summary>
    public void SetKnownHeight(Height height)
    {
        if (!height.IsKnown)
            throw new ArgumentException("Height must be known", nameof(height));

        Height = height;
        Type = PointType.Benchmark;
    }

    /// <summary>Установить вычисленную высоту</summary>
    public void SetCalculatedHeight(Height height)
    {
        if (Type == PointType.Benchmark)
            return; // Не перезаписываем известную высоту

        Height = height;
    }

    /// <summary>Сбросить вычисленную высоту</summary>
    public void ResetCalculatedHeight()
    {
        if (Type != PointType.Benchmark)
        {
            Height = Height.Unknown;
        }
    }

    /// <summary>Пометить как репер</summary>
    public void MarkAsBenchmark()
    {
        Type = PointType.Benchmark;
    }

    // Внутренние методы для добавления связей (вызываются из LevelingNetwork)
    internal void AddOutgoingObservation(Observation observation)
    {
        if (observation.From != this)
            throw new ArgumentException("Observation.From must be this point");
        _outgoingObservations.Add(observation);
    }

    internal void AddIncomingObservation(Observation observation)
    {
        if (observation.To != this)
            throw new ArgumentException("Observation.To must be this point");
        _incomingObservations.Add(observation);
    }

    internal void RemoveObservation(Observation observation)
    {
        _outgoingObservations.Remove(observation);
        _incomingObservations.Remove(observation);
    }

    // Equality по коду точки
    public bool Equals(Point? other) => other != null && Code.Equals(other.Code);
    public override bool Equals(object? obj) => Equals(obj as Point);
    public override int GetHashCode() => Code.GetHashCode();
    public override string ToString() => $"Point({Code}, {Height}, {Type})";
}
