namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO для сгенерированных измерений (экспорт Nivelir).
/// </summary>
public class GeneratedMeasurementDto
{
    public int Index { get; set; }
    public string LineName { get; set; } = "?";
    public string PointCode { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;
    public string? BackPointCode { get; set; }
    public string? ForePointCode { get; set; }
    public double? Rb_m { get; set; }
    public double? Rf_m { get; set; }
    public double? HD_Back_m { get; set; }
    public double? HD_Fore_m { get; set; }
    public double? Height_m { get; set; }
    public bool IsBackSight { get; set; }
    public double? OriginalHeight { get; set; }
    public double? OriginalHD_Back { get; set; }
    public double? OriginalHD_Fore { get; set; }
}
