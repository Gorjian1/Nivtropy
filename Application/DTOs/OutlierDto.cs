using System;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Представляет точку-выброс (аномалию) в профиле хода
/// </summary>
public class OutlierDto
{
    /// <summary>
    /// Номер станции (индекс)
    /// </summary>
    public int StationIndex { get; set; }

    /// <summary>
    /// Код точки
    /// </summary>
    public string PointCode { get; set; } = string.Empty;

    /// <summary>
    /// Фактическое значение
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Ожидаемое значение (среднее, тренд и т.д.)
    /// </summary>
    public double ExpectedValue { get; set; }

    /// <summary>
    /// Отклонение от ожидаемого (в единицах σ)
    /// </summary>
    public double DeviationInSigma { get; set; }

    /// <summary>
    /// Абсолютное отклонение
    /// </summary>
    public double AbsoluteDeviation => Math.Abs(Value - ExpectedValue);

    /// <summary>
    /// Тип аномалии
    /// </summary>
    public OutlierType Type { get; set; }

    /// <summary>
    /// Описание проблемы
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Уровень серьезности (1-3: 1=предупреждение, 2=важно, 3=критично)
    /// </summary>
    public int Severity { get; set; } = 1;

    public override string ToString()
    {
        return $"Ст. {StationIndex} ({PointCode}): {Description}";
    }
}

/// <summary>
/// Типы аномалий
/// </summary>
public enum OutlierType
{
    /// <summary>
    /// Резкий перепад высот
    /// </summary>
    HeightJump,

    /// <summary>
    /// Аномальная длина станции
    /// </summary>
    StationLength,

    /// <summary>
    /// Превышение разности плеч
    /// </summary>
    ArmDifference,

    /// <summary>
    /// Аномальное превышение
    /// </summary>
    DeltaH,

    /// <summary>
    /// Общее отклонение от тренда
    /// </summary>
    TrendDeviation
}
