using System.Collections.Generic;
using Nivtropy.Application.Services;
using Nivtropy.Domain.Services;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Результат обработки данных ходов.
/// </summary>
public class TraverseProcessingResult
{
    public List<StationDto> Stations { get; set; } = new();
    public List<RunSummaryDto> Runs { get; set; } = new();
    public TraverseStatisticsDto Statistics { get; set; } = new();
    public Dictionary<int, List<string>> SharedPointsByRun { get; set; } = new();
    public Dictionary<string, List<int>> SharedPointRunIndexes { get; set; } = new();
    public ConnectivityResult Connectivity { get; set; } = new();
    public ClosureCalculationResult ClosureResult { get; set; } = new();
}
