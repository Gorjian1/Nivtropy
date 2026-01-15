namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Высота точки в метрах. Может быть известной или неизвестной.
/// </summary>
public readonly record struct Height : IComparable<Height>
{
    public double Value { get; }
    public bool IsKnown { get; }

    private Height(double value, bool isKnown)
    {
        Value = isKnown ? value : 0;
        IsKnown = isKnown;
    }

    /// <summary>Создать известную высоту</summary>
    public static Height Known(double meters) => new(meters, true);

    /// <summary>Неизвестная высота</summary>
    public static Height Unknown => new(0, false);

    /// <summary>Создать высоту из nullable</summary>
    public static Height FromNullable(double? meters) =>
        meters.HasValue ? Known(meters.Value) : Unknown;

    // Арифметика (только для известных высот)
    public static Height operator +(Height a, double delta) =>
        a.IsKnown ? Known(a.Value + delta) : Unknown;

    public static Height operator -(Height a, double delta) =>
        a.IsKnown ? Known(a.Value - delta) : Unknown;

    public static double operator -(Height a, Height b)
    {
        if (!a.IsKnown || !b.IsKnown)
            throw new InvalidOperationException("Cannot subtract unknown heights");
        return a.Value - b.Value;
    }

    // Форматирование
    public override string ToString() =>
        IsKnown ? $"{Value:F4} м" : "—";

    public string ToString(string format) =>
        IsKnown ? Value.ToString(format) : "—";

    public int CompareTo(Height other)
    {
        if (!IsKnown && !other.IsKnown) return 0;
        if (!IsKnown) return -1;
        if (!other.IsKnown) return 1;
        return Value.CompareTo(other.Value);
    }
}
