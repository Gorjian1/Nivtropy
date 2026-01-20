using System.Collections.Generic;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Services;

namespace Nivtropy.Application.DTOs;

public record TraverseSystemDescriptorDto(string Id, int Order);

public class TraverseCalculationRequest
{
    public IReadOnlyList<StationDto> Stations { get; init; } = new List<StationDto>();
    public IReadOnlyList<RunSummaryDto> Runs { get; init; } = new List<RunSummaryDto>();
    public IReadOnlyDictionary<string, double> KnownHeights { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, bool> SharedPointStates { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, string> BenchmarkSystems { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<TraverseSystemDescriptorDto> Systems { get; init; } = new List<TraverseSystemDescriptorDto>();
    public IReadOnlyDictionary<int, List<string>> SharedPointsByRun { get; init; } = new Dictionary<int, List<string>>();
    public double MethodOrientationSign { get; init; }
    public AdjustmentMode AdjustmentMode { get; init; }
    public IToleranceOption? MethodOption { get; init; }
    public LevelingClassOption? ClassOption { get; init; }
}

public class TraverseCalculationResult
{
    public List<StationDto> Stations { get; init; } = new();
    public List<RunSummaryDto> Runs { get; init; } = new();
    public ClosureCalculationResult Closure { get; init; } = new();
    public int StationsCount { get; init; }
    public double TotalBackDistance { get; init; }
    public double TotalForeDistance { get; init; }
    public double TotalAverageDistance { get; init; }
}
