namespace Nivtropy.Domain.ValueObjects;

/// <summary>
/// Невязка хода с допуском.
/// </summary>
public readonly record struct Closure
{
    /// <summary>Невязка в миллиметрах</summary>
    public double ValueMm { get; }

    /// <summary>Допустимая невязка в миллиметрах</summary>
    public double ToleranceMm { get; }

    public Closure(double valueMm, double toleranceMm)
    {
        ValueMm = valueMm;
        ToleranceMm = Math.Abs(toleranceMm);
    }

    /// <summary>Невязка в пределах допуска</summary>
    public bool IsWithinTolerance => Math.Abs(ValueMm) <= ToleranceMm;

    /// <summary>Отношение невязки к допуску (для визуализации)</summary>
    public double Ratio => ToleranceMm > 0 ? ValueMm / ToleranceMm : 0;

    /// <summary>Абсолютное значение невязки</summary>
    public double AbsoluteValueMm => Math.Abs(ValueMm);

    /// <summary>Превышение допуска в мм (отрицательное если в допуске)</summary>
    public double ExcessMm => AbsoluteValueMm - ToleranceMm;

    public override string ToString() =>
        $"{ValueMm:+0.0;-0.0} мм (доп. ±{ToleranceMm:F1} мм) {(IsWithinTolerance ? "✓" : "✗")}";
}
