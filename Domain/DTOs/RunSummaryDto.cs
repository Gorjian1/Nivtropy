namespace Nivtropy.Domain.DTOs;

/// <summary>
/// DTO сводки по нивелирному ходу для расчётов.
/// </summary>
public class RunSummaryDto
{
    public int Index { get; set; }
    public string? OriginalLineNumber { get; set; }
    public string? StartPointCode { get; set; }
    public string? EndPointCode { get; set; }
    public int StationCount { get; set; }
    public double? DeltaHSum { get; set; }
    public double? TotalDistanceBack { get; set; }
    public double? TotalDistanceFore { get; set; }
    public double? ArmDifferenceAccumulation { get; set; }
    public string? SystemId { get; set; }
    public bool IsActive { get; set; } = true;
    public int KnownPointsCount { get; set; }
    public bool UseLocalAdjustment { get; set; }

    public double? TotalLength => TotalDistanceBack.HasValue && TotalDistanceFore.HasValue
        ? TotalDistanceBack.Value + TotalDistanceFore.Value
        : null;

    public List<double> Closures { get; set; } = new();
    public List<string> SharedPointCodes { get; set; } = new();
}
