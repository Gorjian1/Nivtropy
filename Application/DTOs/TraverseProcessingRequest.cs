using System.Collections.Generic;
using Nivtropy.Application.Enums;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Запрос на обработку данных ходов.
/// </summary>
public class TraverseProcessingRequest
{
    public IReadOnlyList<StationDto> Stations { get; init; } = new List<StationDto>();
    public IReadOnlyList<RunSummaryDto> Runs { get; init; } = new List<RunSummaryDto>();
    public IReadOnlyDictionary<string, double> KnownHeights { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, bool> SharedPointStates { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, string> BenchmarkSystems { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> ExistingAutoSystemIds { get; init; } = new List<string>();
    public double MethodOrientationSign { get; init; }
    public AdjustmentMode AdjustmentMode { get; init; }
    public ToleranceOptionDto? MethodOption { get; init; }
    public ToleranceOptionDto? ClassOption { get; init; }
    public double? ArmDifferenceToleranceStation { get; init; }
    public double? ArmDifferenceToleranceAccumulation { get; init; }
}
