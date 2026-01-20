namespace Nivtropy.Application.DTOs;

/// <summary>
/// Сводная статистика по ходам.
/// </summary>
public class TraverseStatisticsDto
{
    public int StationsCount { get; set; }
    public double TotalBackDistance { get; set; }
    public double TotalForeDistance { get; set; }
    public double TotalAverageDistance { get; set; }
    public double TotalLengthKilometers { get; set; }
}
