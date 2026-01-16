namespace Nivtropy.Application.DTOs;

/// <summary>
/// Входные данные для расчёта поправок (замена StationCorrectionInput).
/// </summary>
public class CorrectionInputDto
{
    public string? PointCode { get; set; }
    public double? DeltaH { get; set; }
    public double? BackDistance { get; set; }
    public double? ForeDistance { get; set; }

    public double? AverageDistance => (BackDistance.HasValue && ForeDistance.HasValue)
        ? (BackDistance.Value + ForeDistance.Value) / 2.0
        : null;
}
