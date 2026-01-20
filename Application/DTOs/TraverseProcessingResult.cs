using System.Collections.Generic;
using Nivtropy.Application.Services;
using Nivtropy.Domain.Services;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Результат обработки данных ходов.
/// </summary>
public class TraverseProcessingResult
{
    public List<StationDto> Stations { get; init; } = new();
    public List<RunSummaryDto> Runs { get; init; } = new();
    public TraverseStatisticsDto Statistics { get; init; } = new();
    public Dictionary<int, List<string>> SharedPointsByRun { get; init; } = new();
    public Dictionary<string, List<int>> SharedPointRunIndexes { get; init; } = new();
    public ConnectivityResult Connectivity { get; init; } = new();
    public ClosureCalculationResult ClosureResult { get; init; } = new();
}
