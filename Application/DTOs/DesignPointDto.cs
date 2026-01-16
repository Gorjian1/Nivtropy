namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO строки проектирования (замена DesignRow).
/// </summary>
public class DesignPointDto
{
    public int Index { get; set; }
    public string Station { get; set; } = string.Empty;
    public double? Distance { get; set; }
    public double? OriginalDeltaH { get; set; }
    public double Correction { get; set; }
    public double? AdjustedDeltaH { get; set; }
    public double DesignedHeight { get; set; }
    public bool IsEdited { get; set; }
    public double? OriginalHeight { get; set; }
    public double? OriginalDistance { get; set; }
}
