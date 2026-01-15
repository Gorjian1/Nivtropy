namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Расстояние (горизонтальное проложение) в метрах.
/// Всегда неотрицательное.
/// </summary>
public readonly record struct Distance : IComparable<Distance>
{
    public double Meters { get; }

    public Distance(double meters)
    {
        if (meters < 0)
            throw new ArgumentOutOfRangeException(nameof(meters), "Distance cannot be negative");
        Meters = meters;
    }

    // Конверсии
    public double Kilometers => Meters / 1000.0;
    public double Centimeters => Meters * 100.0;

    // Фабричные методы
    public static Distance FromMeters(double m) => new(m);
    public static Distance FromKilometers(double km) => new(km * 1000);
    public static Distance Zero => new(0);

    // Арифметика
    public static Distance operator +(Distance a, Distance b) =>
        new(a.Meters + b.Meters);

    public static Distance operator -(Distance a, Distance b) =>
        new(Math.Max(0, a.Meters - b.Meters));

    public static Distance operator *(Distance d, double factor) =>
        new(d.Meters * factor);

    public static Distance operator /(Distance d, double divisor) =>
        new(d.Meters / divisor);

    // Сравнение
    public static bool operator >(Distance a, Distance b) => a.Meters > b.Meters;
    public static bool operator <(Distance a, Distance b) => a.Meters < b.Meters;
    public static bool operator >=(Distance a, Distance b) => a.Meters >= b.Meters;
    public static bool operator <=(Distance a, Distance b) => a.Meters <= b.Meters;

    public int CompareTo(Distance other) => Meters.CompareTo(other.Meters);

    public override string ToString() => $"{Meters:F3} м";
}
