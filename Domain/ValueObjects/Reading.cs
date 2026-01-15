namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Отсчёт по рейке (в метрах).
/// </summary>
public readonly record struct Reading
{
    public double Meters { get; }

    public Reading(double meters)
    {
        // Отсчёт может быть любым (положительным или отрицательным для инвар-реек)
        Meters = meters;
    }

    public double Millimeters => Meters * 1000.0;

    public static Reading FromMeters(double m) => new(m);
    public static Reading FromMillimeters(double mm) => new(mm / 1000.0);

    // Превышение = задний отсчёт - передний отсчёт
    public static double operator -(Reading back, Reading fore) =>
        back.Meters - fore.Meters;

    public override string ToString() => $"{Meters:F5}";
}
