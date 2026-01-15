namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Код точки нивелирования (репер, связующая точка).
/// Immutable value object с нормализацией.
/// </summary>
public readonly record struct PointCode : IComparable<PointCode>
{
    public string Value { get; }

    public PointCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Point code cannot be empty", nameof(value));

        // Нормализация: trim + uppercase
        Value = value.Trim().ToUpperInvariant();
    }

    // Implicit conversion для совместимости со старым кодом
    public static implicit operator string(PointCode code) => code.Value;
    public static explicit operator PointCode(string value) => new(value);

    public override string ToString() => Value;

    public int CompareTo(PointCode other) =>
        string.Compare(Value, other.Value, StringComparison.Ordinal);

    // Для использования в Dictionary
    public override int GetHashCode() => Value.GetHashCode();
}
