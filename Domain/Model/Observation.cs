namespace Nivtropy.Domain.Model;

using Nivtropy.Domain.ValueObjects;

/// <summary>
/// Наблюдение (станция) - ребро графа между двумя точками.
/// Содержит измерения и вычисляемые характеристики.
/// </summary>
public class Observation
{
    /// <summary>Уникальный идентификатор</summary>
    public Guid Id { get; }

    /// <summary>Задняя точка (откуда)</summary>
    public Point From { get; }

    /// <summary>Передняя точка (куда)</summary>
    public Point To { get; }

    /// <summary>Ход, к которому принадлежит наблюдение</summary>
    public Run Run { get; }

    /// <summary>Индекс станции в ходе (1, 2, 3...)</summary>
    public int StationIndex { get; }

    // Измерения
    /// <summary>Отсчёт по задней рейке</summary>
    public Reading BackReading { get; }

    /// <summary>Отсчёт по передней рейке</summary>
    public Reading ForeReading { get; }

    /// <summary>Расстояние до задней точки</summary>
    public Distance BackDistance { get; }

    /// <summary>Расстояние до передней точки</summary>
    public Distance ForeDistance { get; }

    // Вычисляемые свойства
    /// <summary>Измеренное превышение (Back - Fore)</summary>
    public double DeltaH => BackReading - ForeReading;

    /// <summary>Длина станции (сумма плеч)</summary>
    public Distance StationLength => BackDistance + ForeDistance;

    /// <summary>Разность плеч</summary>
    public double ArmDifference => BackDistance.Meters - ForeDistance.Meters;

    // Уравнивание
    /// <summary>Поправка в превышение</summary>
    public double Correction { get; private set; }

    /// <summary>Исправленное превышение</summary>
    public double AdjustedDeltaH => DeltaH + Correction;

    /// <summary>Создать наблюдение</summary>
    public Observation(
        Point from,
        Point to,
        Run run,
        int stationIndex,
        Reading backReading,
        Reading foreReading,
        Distance backDistance,
        Distance foreDistance)
    {
        Id = Guid.NewGuid();
        From = from ?? throw new ArgumentNullException(nameof(from));
        To = to ?? throw new ArgumentNullException(nameof(to));
        Run = run ?? throw new ArgumentNullException(nameof(run));
        StationIndex = stationIndex;
        BackReading = backReading;
        ForeReading = foreReading;
        BackDistance = backDistance;
        ForeDistance = foreDistance;
        Correction = 0;
    }

    /// <summary>Применить поправку</summary>
    public void ApplyCorrection(double correction)
    {
        Correction = correction;
    }

    /// <summary>Сбросить поправку</summary>
    public void ResetCorrection()
    {
        Correction = 0;
    }

    /// <summary>Вычислить высоту передней точки по задней</summary>
    public Height CalculateForeHeight()
    {
        if (!From.Height.IsKnown)
            return Height.Unknown;

        return Height.Known(From.Height.Value - AdjustedDeltaH);
    }

    /// <summary>Вычислить высоту задней точки по передней</summary>
    public Height CalculateBackHeight()
    {
        if (!To.Height.IsKnown)
            return Height.Unknown;

        return Height.Known(To.Height.Value + AdjustedDeltaH);
    }

    public override string ToString() =>
        $"Observation({From.Code} → {To.Code}, ΔH={DeltaH:F4})";
}
