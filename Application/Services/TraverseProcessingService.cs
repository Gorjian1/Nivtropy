using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Domain.Services;

namespace Nivtropy.Application.Services;

public interface ITraverseProcessingService
{
    TraverseProcessingResult Process(TraverseProcessingRequest request);
}

public class TraverseProcessingService : ITraverseProcessingService
{
    private readonly ITraverseCalculationService _calculationService;
    private readonly IClosureCalculationService _closureService;
    private readonly ISystemConnectivityService _connectivityService;

    public TraverseProcessingService(
        ITraverseCalculationService calculationService,
        IClosureCalculationService closureService,
        ISystemConnectivityService connectivityService)
    {
        _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        _closureService = closureService ?? throw new ArgumentNullException(nameof(closureService));
        _connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    }

    public TraverseProcessingResult Process(TraverseProcessingRequest request)
    {
        var result = new TraverseProcessingResult();

        var stations = request.Stations.ToList();
        var runs = request.Runs.ToList();

        if (stations.Count == 0)
        {
            result.Stations = new List<StationDto>();
            result.Runs = runs;
            result.Statistics = new TraverseStatisticsDto();
            result.ClosureResult = new ClosureCalculationResult();
            return result;
        }

        var sharedPointUsage = BuildSharedPointUsage(stations);
        result.SharedPointRunIndexes = sharedPointUsage.SharedPointRunIndexes;
        result.SharedPointsByRun = sharedPointUsage.SharedPointsByRun;

        var sharedPoints = sharedPointUsage.SharedPointRunIndexes
            .Select(kvp => new SharedPointDto
            {
                Code = kvp.Key,
                RunIndexes = kvp.Value,
                IsEnabled = IsSharedPointEnabled(kvp.Key, request.SharedPointStates)
            })
            .ToList();

        var connectivity = _connectivityService.AnalyzeConnectivity(
            runs,
            sharedPoints,
            request.ExistingAutoSystemIds);
        result.Connectivity = connectivity;

        ApplyConnectivity(runs, connectivity);

        var activeStations = ProcessStationsBySystem(
            stations,
            request,
            result.SharedPointsByRun);

        ApplyArmDifferenceTolerance(
            activeStations,
            runs,
            request.ArmDifferenceToleranceStation,
            request.ArmDifferenceToleranceAccumulation);

        result.Statistics = BuildStatistics(activeStations);
        result.ClosureResult = _closureService.Calculate(
            activeStations,
            request.MethodOrientationSign,
            result.Statistics.StationsCount,
            result.Statistics.TotalLengthKilometers,
            BuildToleranceOption(request.MethodOption),
            BuildToleranceOption(request.ClassOption));

        result.Stations = activeStations;
        result.Runs = runs;

        return result;
    }

    private static void ApplyConnectivity(List<RunSummaryDto> runs, ConnectivityResult connectivity)
    {
        var runLookup = runs.ToDictionary(r => r.Index);
        foreach (var kvp in connectivity.RunToSystemId)
        {
            if (runLookup.TryGetValue(kvp.Key, out var run))
            {
                run.SystemId = kvp.Value;
            }
        }
    }

    private List<StationDto> ProcessStationsBySystem(
        List<StationDto> stations,
        TraverseProcessingRequest request,
        Dictionary<int, List<string>> sharedPointsByRun)
    {
        var groupsByLine = stations
            .GroupBy(s => s.LineName)
            .Select(g => new TraverseGroup(g.Key, g.ToList()))
            .ToList();

        var activeGroups = groupsByLine
            .Where(g => g.RunSummary?.IsActive ?? true)
            .ToList();

        var groupsBySystem = activeGroups
            .GroupBy(g => g.RunSummary?.SystemId ?? string.Empty)
            .ToList();

        foreach (var systemGroup in groupsBySystem)
        {
            var availableAdjustedHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var availableRawHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in request.KnownHeights)
            {
                if (request.BenchmarkSystems.TryGetValue(kvp.Key, out var systemId) &&
                    string.Equals(systemId, systemGroup.Key, StringComparison.OrdinalIgnoreCase))
                {
                    availableAdjustedHeights[kvp.Key] = kvp.Value;
                    availableRawHeights[kvp.Key] = kvp.Value;
                }
            }

            bool AnchorChecker(string code) => IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey, request);

            ProcessSystemTraverseGroups(
                systemGroup.ToList(),
                availableAdjustedHeights,
                availableRawHeights,
                request,
                AnchorChecker,
                sharedPointsByRun);
        }

        return stations
            .Where(s => s.RunSummary?.IsActive ?? true)
            .ToList();
    }

    private void ProcessSystemTraverseGroups(
        List<TraverseGroup> systemTraverseGroups,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        TraverseProcessingRequest request,
        Func<string, bool> anchorChecker,
        Dictionary<int, List<string>> sharedPointsByRun)
    {
        var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int iteration = 0; iteration < systemTraverseGroups.Count; iteration++)
        {
            bool progress = false;

            foreach (var group in systemTraverseGroups)
            {
                if (processedGroups.Contains(group.LineName))
                    continue;

                var groupItems = group.Items;
                bool hasAnchor = groupItems.Any(r =>
                    (!string.IsNullOrWhiteSpace(r.BackCode) && anchorChecker(r.BackCode!)) ||
                    (!string.IsNullOrWhiteSpace(r.ForeCode) && anchorChecker(r.ForeCode!)));

                if (!hasAnchor && iteration < systemTraverseGroups.Count - 1)
                    continue;

                if (!hasAnchor)
                {
                    var firstCode = groupItems.Select(r => r.BackCode ?? r.ForeCode)
                        .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                    if (!string.IsNullOrWhiteSpace(firstCode))
                    {
                        availableAdjustedHeights[firstCode!] = 0.0;
                        availableRawHeights[firstCode!] = 0.0;
                    }
                }

                _calculationService.ApplyCorrections(
                    groupItems,
                    anchorChecker,
                    request.MethodOrientationSign,
                    request.AdjustmentMode);

                UpdateRunSummaries(groupItems, anchorChecker, sharedPointsByRun);

                CalculateHeightsForRun(groupItems, availableAdjustedHeights, availableRawHeights, request);

                processedGroups.Add(group.LineName);
                progress = true;
            }

            if (!progress)
                break;
        }
    }

    private void UpdateRunSummaries(
        List<StationDto> rows,
        Func<string, bool> isAnchor,
        Dictionary<int, List<string>> sharedPointsByRun)
    {
        if (rows.Count == 0)
            return;

        var runSummary = rows.First().RunSummary;
        if (runSummary == null)
            return;

        double? accumulation = null;
        double? totalDistanceBack = null;
        double? totalDistanceFore = null;

        foreach (var row in rows)
        {
            if (row.ArmDifference.HasValue)
            {
                accumulation = (accumulation ?? 0) + row.ArmDifference.Value;
            }

            if (row.BackDistance.HasValue)
            {
                totalDistanceBack = (totalDistanceBack ?? 0) + row.BackDistance.Value;
            }

            if (row.ForeDistance.HasValue)
            {
                totalDistanceFore = (totalDistanceFore ?? 0) + row.ForeDistance.Value;
            }
        }

        var knownPointsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.BackCode) && isAnchor(row.BackCode))
            {
                knownPointsSet.Add(row.BackCode);
            }
            if (!string.IsNullOrWhiteSpace(row.ForeCode) && isAnchor(row.ForeCode))
            {
                knownPointsSet.Add(row.ForeCode);
            }
        }

        runSummary.TotalDistanceBack = totalDistanceBack;
        runSummary.TotalDistanceFore = totalDistanceFore;
        runSummary.ArmDifferenceAccumulation = accumulation;
        runSummary.KnownPointsCount = knownPointsSet.Count;

        if (sharedPointsByRun.TryGetValue(runSummary.Index, out var sharedCodesForRun))
        {
            runSummary.SharedPointCodes = sharedCodesForRun;
        }
        else
        {
            runSummary.SharedPointCodes = new List<string>();
        }
    }

    private static void CalculateHeightsForRun(
        List<StationDto> items,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        TraverseProcessingRequest request)
    {
        if (items.Count == 0)
            return;

        ResetRowHeights(items);

        var aliasManager = new RunAliasManager(code => IsAnchorAllowed(code, availableAdjustedHeights.ContainsKey, request));
        InitializeAliases(items, aliasManager);

        var adjusted = new Dictionary<string, double>(availableAdjustedHeights, StringComparer.OrdinalIgnoreCase);
        var raw = new Dictionary<string, double>(availableRawHeights, StringComparer.OrdinalIgnoreCase);
        UpdateLocalHeightsForDisabledSharedPoints(items, adjusted, raw, availableAdjustedHeights, availableRawHeights, request);

        PropagateHeightsIteratively(items, adjusted, raw, aliasManager);

        AssignAdjustedHeightsToRows(items, adjusted, availableAdjustedHeights, aliasManager, request);

        var heightTracker = new RunHeightTracker(raw, availableRawHeights);
        CalculateRawHeightsForwardPass(items, heightTracker, aliasManager);
        CalculateRawHeightsBackwardPass(items, adjusted, heightTracker, aliasManager);
        UpdateVirtualStations(items, adjusted, raw, aliasManager);

        UpdateAvailableHeights(adjusted, availableAdjustedHeights, availableRawHeights, aliasManager, request);
    }

    private static void ResetRowHeights(List<StationDto> items)
    {
        foreach (var row in items)
        {
            row.BackHeight = null;
            row.ForeHeight = null;
            row.BackHeightRaw = null;
            row.ForeHeightRaw = null;
            row.IsBackHeightKnown = false;
            row.IsForeHeightKnown = false;
        }
    }

    private static void InitializeAliases(List<StationDto> items, RunAliasManager aliasManager)
    {
        string? previousForeCode = null;

        foreach (var row in items)
        {
            if (!string.IsNullOrWhiteSpace(row.BackCode))
            {
                var reusePrevious = previousForeCode != null &&
                                    string.Equals(previousForeCode, row.BackCode, StringComparison.OrdinalIgnoreCase);
                var alias = aliasManager.RegisterAlias(row.BackCode!, reusePrevious);
                aliasManager.RegisterRowAlias(row, isBack: true, alias);
            }

            if (!string.IsNullOrWhiteSpace(row.ForeCode))
            {
                var alias = aliasManager.RegisterAlias(row.ForeCode!, reusePrevious: false);
                aliasManager.RegisterRowAlias(row, isBack: false, alias);
                previousForeCode = row.ForeCode;
            }
            else
            {
                aliasManager.ResetPreviousFore();
                previousForeCode = null;
            }
        }
    }

    private static void UpdateLocalHeightsForDisabledSharedPoints(
        List<StationDto> items,
        Dictionary<string, double> adjusted,
        Dictionary<string, double> raw,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        TraverseProcessingRequest request)
    {
        var pointsInRun = items.SelectMany(r => new[] { r.BackCode, r.ForeCode })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var runName = items.FirstOrDefault()?.LineName ?? string.Empty;

        foreach (var pointCode in pointsInRun)
        {
            if (!IsSharedPointEnabled(pointCode!, request.SharedPointStates))
            {
                var codeWithRun = GetPointCodeForRun(pointCode!, runName);
                if (availableAdjustedHeights.TryGetValue(codeWithRun, out var adjValue))
                    adjusted[pointCode!] = adjValue;
                if (availableRawHeights.TryGetValue(codeWithRun, out var rawValue))
                    raw[pointCode!] = rawValue;
            }
        }
    }

    private static void PropagateHeightsIteratively(
        List<StationDto> items,
        Dictionary<string, double> adjusted,
        Dictionary<string, double> raw,
        RunAliasManager aliasManager)
    {
        const int maxIterations = 20;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool changedRaw = PropagateHeightsWithinRun(items, raw, useAdjusted: false, aliasManager.GetAlias);
            bool changedAdjusted = PropagateHeightsWithinRun(items, adjusted, useAdjusted: true, aliasManager.GetAlias);

            if (!changedRaw && !changedAdjusted)
                break;
        }
    }

    private static void AssignAdjustedHeightsToRows(
        List<StationDto> items,
        Dictionary<string, double> adjusted,
        Dictionary<string, double> availableAdjustedHeights,
        RunAliasManager aliasManager,
        TraverseProcessingRequest request)
    {
        foreach (var row in items)
        {
            var backAlias = aliasManager.GetAlias(row, isBack: true);
            if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var backZ))
            {
                row.BackHeight = backZ;
                row.IsBackHeightKnown = IsAnchorAllowed(row.BackCode, availableAdjustedHeights.ContainsKey, request);
            }

            var foreAlias = aliasManager.GetAlias(row, isBack: false);
            if (!string.IsNullOrWhiteSpace(foreAlias) && adjusted.TryGetValue(foreAlias!, out var foreZ))
            {
                row.ForeHeight = foreZ;
                row.IsForeHeightKnown = IsAnchorAllowed(row.ForeCode, availableAdjustedHeights.ContainsKey, request);
            }
        }
    }

    private static void CalculateRawHeightsForwardPass(
        List<StationDto> items,
        RunHeightTracker heightTracker,
        RunAliasManager aliasManager)
    {
        foreach (var row in items)
        {
            var delta = row.DeltaH;
            var backAlias = aliasManager.GetAlias(row, isBack: true);
            var foreAlias = aliasManager.GetAlias(row, isBack: false);

            if (row.BackHeightRaw == null)
            {
                var existingBack = heightTracker.GetHeight(backAlias);
                if (existingBack.HasValue)
                {
                    row.BackHeightRaw = existingBack;
                    heightTracker.RecordHeight(backAlias, existingBack.Value);
                }
            }

            if (row.ForeHeightRaw == null)
            {
                var existingFore = heightTracker.GetHeight(foreAlias);
                if (existingFore.HasValue)
                {
                    row.ForeHeightRaw = existingFore;
                    heightTracker.RecordHeight(foreAlias, existingFore.Value);
                }
            }

            if (!delta.HasValue)
                continue;

            var backHeight = row.BackHeightRaw ?? heightTracker.GetHeight(backAlias);
            var foreHeight = row.ForeHeightRaw ?? heightTracker.GetHeight(foreAlias);

            if (backHeight.HasValue)
            {
                var computedFore = backHeight.Value + delta.Value;
                row.ForeHeightRaw = computedFore;
                heightTracker.RecordHeight(foreAlias, computedFore);
            }
            else if (foreHeight.HasValue)
            {
                var computedBack = foreHeight.Value - delta.Value;
                row.BackHeightRaw = computedBack;
                heightTracker.RecordHeight(backAlias, computedBack);
            }

            if (backHeight.HasValue && !heightTracker.HasHistory(backAlias))
                heightTracker.RecordHeight(backAlias, backHeight.Value);
            if (foreHeight.HasValue && !heightTracker.HasHistory(foreAlias))
                heightTracker.RecordHeight(foreAlias, foreHeight.Value);
        }
    }

    private static void CalculateRawHeightsBackwardPass(
        List<StationDto> items,
        Dictionary<string, double> adjusted,
        RunHeightTracker heightTracker,
        RunAliasManager aliasManager)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var row = items[i];
            var delta = row.DeltaH;
            var adjustedDelta = row.AdjustedDeltaH ?? row.DeltaH;

            if (!delta.HasValue)
                continue;

            var backAlias = aliasManager.GetAlias(row, isBack: true);
            var foreAlias = aliasManager.GetAlias(row, isBack: false);

            if (row.ForeHeightRaw.HasValue && !row.BackHeightRaw.HasValue)
            {
                var computedBack = row.ForeHeightRaw.Value - delta.Value;
                row.BackHeightRaw = computedBack;
                heightTracker.RecordHeight(backAlias, computedBack);

                if (!string.IsNullOrWhiteSpace(backAlias) && !adjusted.ContainsKey(backAlias!))
                {
                    var foreAdjusted = row.ForeHeight ?? row.ForeHeightRaw;
                    if (foreAdjusted.HasValue && adjustedDelta.HasValue)
                    {
                        var computedBackAdj = foreAdjusted.Value - adjustedDelta.Value;
                        adjusted[backAlias!] = computedBackAdj;
                        row.BackHeight ??= computedBackAdj;
                    }
                }
            }
            else if (row.BackHeightRaw.HasValue && !row.ForeHeightRaw.HasValue)
            {
                var computedFore = row.BackHeightRaw.Value + delta.Value;
                row.ForeHeightRaw = computedFore;
                heightTracker.RecordHeight(foreAlias, computedFore);

                if (!string.IsNullOrWhiteSpace(foreAlias) && !adjusted.ContainsKey(foreAlias!))
                {
                    var backAdjusted = row.BackHeight ?? row.BackHeightRaw;
                    if (backAdjusted.HasValue && adjustedDelta.HasValue)
                    {
                        var computedForeAdj = backAdjusted.Value + adjustedDelta.Value;
                        adjusted[foreAlias!] = computedForeAdj;
                        row.ForeHeight ??= computedForeAdj;
                    }
                }
            }
        }
    }

    private static void UpdateVirtualStations(
        List<StationDto> items,
        Dictionary<string, double> adjusted,
        Dictionary<string, double> raw,
        RunAliasManager aliasManager)
    {
        foreach (var row in items)
        {
            if (!row.DeltaH.HasValue && !string.IsNullOrWhiteSpace(row.BackCode))
            {
                var backAlias = aliasManager.GetAlias(row, isBack: true);
                if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var height))
                {
                    row.BackHeight ??= height;
                    row.BackHeightRaw ??= raw.TryGetValue(backAlias!, out var rawH) ? rawH : height;
                }
            }
        }
    }

    private static void UpdateAvailableHeights(
        Dictionary<string, double> adjusted,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        RunAliasManager aliasManager,
        TraverseProcessingRequest request)
    {
        foreach (var kvp in adjusted)
        {
            if (!aliasManager.IsCopyAlias(kvp.Key) && AllowPropagation(kvp.Key, request))
            {
                availableAdjustedHeights[kvp.Key] = kvp.Value;
                availableRawHeights[kvp.Key] = kvp.Value;
            }
        }
    }

    private static bool PropagateHeightsWithinRun(
        List<StationDto> sections,
        Dictionary<string, double> heights,
        bool useAdjusted,
        Func<StationDto, bool, string?> aliasSelector)
    {
        bool changed = false;

        foreach (var section in sections)
        {
            var delta = useAdjusted ? section.AdjustedDeltaH : section.DeltaH;
            if (!delta.HasValue)
                continue;

            var backCode = aliasSelector(section, true);
            var foreCode = aliasSelector(section, false);

            if (string.IsNullOrWhiteSpace(backCode) || string.IsNullOrWhiteSpace(foreCode))
                continue;

            if (heights.TryGetValue(backCode, out var backHeight) && !heights.ContainsKey(foreCode))
            {
                heights[foreCode] = backHeight + delta.Value;
                changed = true;
            }
            else if (heights.TryGetValue(foreCode, out var foreHeight) && !heights.ContainsKey(backCode))
            {
                heights[backCode] = foreHeight - delta.Value;
                changed = true;
            }
        }

        return changed;
    }

    private static void ApplyArmDifferenceTolerance(
        List<StationDto> stations,
        List<RunSummaryDto> runs,
        double? stationTolerance,
        double? accumulationTolerance)
    {
        if (!stationTolerance.HasValue && !accumulationTolerance.HasValue)
            return;

        if (stationTolerance.HasValue)
        {
            foreach (var row in stations)
            {
                if (row.ArmDifference.HasValue)
                {
                    row.IsArmDifferenceExceeded = Math.Abs(row.ArmDifference.Value) > stationTolerance.Value;
                }
                else
                {
                    row.IsArmDifferenceExceeded = false;
                }
            }
        }

        if (accumulationTolerance.HasValue)
        {
            foreach (var run in runs)
            {
                if (run.ArmDifferenceAccumulation.HasValue)
                {
                    run.IsArmDifferenceAccumulationExceeded =
                        Math.Abs(run.ArmDifferenceAccumulation.Value) > accumulationTolerance.Value;
                }
            }
        }
    }

    private static TraverseStatisticsDto BuildStatistics(IReadOnlyCollection<StationDto> rows)
    {
        var stationsCount = rows.Count(r => r.DeltaH.HasValue);
        var totalBackDistance = rows.Sum(r => r.BackDistance ?? 0);
        var totalForeDistance = rows.Sum(r => r.ForeDistance ?? 0);
        var totalAverageDistance = stationsCount > 0
            ? (totalBackDistance + totalForeDistance) / 2.0
            : 0;

        return new TraverseStatisticsDto
        {
            StationsCount = stationsCount,
            TotalBackDistance = totalBackDistance,
            TotalForeDistance = totalForeDistance,
            TotalAverageDistance = totalAverageDistance,
            TotalLengthKilometers = totalBackDistance / 1000.0
        };
    }

    private static bool AllowPropagation(string code, TraverseProcessingRequest request)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return request.KnownHeights.ContainsKey(code)
               || IsSharedPointEnabled(code, request.SharedPointStates);
    }

    private static bool IsAnchorAllowed(
        string? code,
        Func<string, bool> contains,
        TraverseProcessingRequest request)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return contains(code) &&
               (request.KnownHeights.ContainsKey(code) || IsSharedPointEnabled(code, request.SharedPointStates));
    }

    private static bool IsSharedPointEnabled(string code, IReadOnlyDictionary<string, bool> states)
    {
        return !states.TryGetValue(code.Trim(), out var enabled) || enabled;
    }

    private static string GetPointCodeForRun(string pointCode, string runName)
    {
        return $"{pointCode} ({runName})";
    }

    private static IToleranceOption? BuildToleranceOption(ToleranceOptionDto? option)
    {
        if (option == null)
            return null;

        return new ToleranceOption(option.Code, option.Mode, option.Coefficient);
    }

    private static SharedPointUsageResult BuildSharedPointUsage(IEnumerable<StationDto> stations)
    {
        var usage = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        void AddUsage(string? code, int runIndex)
        {
            if (runIndex == 0 || string.IsNullOrWhiteSpace(code))
                return;

            var trimmed = code.Trim();
            if (!usage.TryGetValue(trimmed, out var set))
            {
                set = new HashSet<int>();
                usage[trimmed] = set;
            }

            set.Add(runIndex);
        }

        foreach (var station in stations)
        {
            var runIndex = station.RunSummary?.Index ?? 0;
            AddUsage(station.BackCode, runIndex);
            AddUsage(station.ForeCode, runIndex);
        }

        var sharedCodes = usage
            .Where(kvp => kvp.Value.Count > 1)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var sharedPointsByRun = sharedCodes
            .SelectMany(kvp => kvp.Value.Select(run => (run, kvp.Key)))
            .GroupBy(x => x.run)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Key)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        return new SharedPointUsageResult(sharedCodes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList()), sharedPointsByRun);
    }

    private sealed record SharedPointUsageResult(
        Dictionary<string, List<int>> SharedPointRunIndexes,
        Dictionary<int, List<string>> SharedPointsByRun);

    private sealed class ToleranceOption : IToleranceOption
    {
        public ToleranceOption(string code, ToleranceMode mode, double coefficient)
        {
            Code = code;
            Mode = mode;
            Coefficient = coefficient;
        }

        public string Code { get; }
        public string Description => Code;
        public ToleranceMode Mode { get; }
        public double Coefficient { get; }
    }

    private sealed class TraverseGroup
    {
        public TraverseGroup(string lineName, List<StationDto> items)
        {
            LineName = lineName;
            Items = items;
            RunSummary = items.FirstOrDefault()?.RunSummary;
        }

        public string LineName { get; }
        public List<StationDto> Items { get; }
        public RunSummaryDto? RunSummary { get; }
    }

    private sealed class RunAliasManager
    {
        private readonly Dictionary<(StationDto row, bool isBack), string> _aliasByRowSide;
        private readonly Dictionary<string, string> _aliasToOriginal;
        private readonly Dictionary<string, int> _occurrenceCount;
        private readonly Func<string, bool> _isAnchor;
        private string? _previousForeCode;
        private string? _previousForeAlias;

        public RunAliasManager(Func<string, bool> isAnchor)
        {
            _aliasByRowSide = new Dictionary<(StationDto row, bool isBack), string>(new AliasKeyComparer());
            _aliasToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _occurrenceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _isAnchor = isAnchor;
        }

        public string RegisterAlias(string code, bool reusePrevious)
        {
            if (_isAnchor(code))
            {
                _aliasToOriginal[code] = code;
                return code;
            }

            if (reusePrevious && _previousForeAlias != null &&
                string.Equals(_previousForeCode, code, StringComparison.OrdinalIgnoreCase))
            {
                _aliasToOriginal[_previousForeAlias] = code;
                return _previousForeAlias;
            }

            var next = _occurrenceCount.TryGetValue(code, out var count) ? count + 1 : 1;
            _occurrenceCount[code] = next;

            var alias = next == 1 ? code : $"{code} ({next})";
            _aliasToOriginal[alias] = code;
            return alias;
        }

        public void RegisterRowAlias(StationDto row, bool isBack, string alias)
        {
            _aliasByRowSide[(row, isBack)] = alias;

            if (!isBack)
            {
                var code = row.ForeCode;
                _previousForeCode = code;
                _previousForeAlias = alias;
            }
        }

        public void ResetPreviousFore()
        {
            _previousForeCode = null;
            _previousForeAlias = null;
        }

        public string? GetAlias(StationDto row, bool isBack)
        {
            return _aliasByRowSide.TryGetValue((row, isBack), out var value) ? value : null;
        }

        public bool IsCopyAlias(string alias)
        {
            return _aliasToOriginal.TryGetValue(alias, out var original)
                && !string.Equals(alias, original, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class AliasKeyComparer : IEqualityComparer<(StationDto row, bool isBack)>
        {
            public bool Equals((StationDto row, bool isBack) x, (StationDto row, bool isBack) y)
            {
                return ReferenceEquals(x.row, y.row) && x.isBack == y.isBack;
            }

            public int GetHashCode((StationDto row, bool isBack) obj)
            {
                return HashCode.Combine(obj.row, obj.isBack);
            }
        }
    }

    private sealed class RunHeightTracker
    {
        private readonly Dictionary<string, List<double>> _runRawHeights;
        private readonly Dictionary<string, double> _rawHeights;
        private readonly Dictionary<string, double> _availableRawHeights;

        public RunHeightTracker(
            Dictionary<string, double> rawHeights,
            Dictionary<string, double> availableRawHeights)
        {
            _runRawHeights = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            _rawHeights = rawHeights;
            _availableRawHeights = availableRawHeights;
        }

        public double? GetHeight(string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return null;

            if (_runRawHeights.TryGetValue(alias!, out var history) && history.Count > 0)
            {
                return history[^1];
            }

            return _rawHeights.TryGetValue(alias!, out var value) ? value : null;
        }

        public bool HasHistory(string? alias)
        {
            return !string.IsNullOrWhiteSpace(alias)
                   && _runRawHeights.TryGetValue(alias!, out var history)
                   && history.Count > 0;
        }

        public void RecordHeight(string? alias, double value)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return;

            if (!_runRawHeights.TryGetValue(alias!, out var history))
            {
                history = new List<double>();
                _runRawHeights[alias!] = history;
            }

            history.Add(value);

            if (!_rawHeights.ContainsKey(alias!) && !_availableRawHeights.ContainsKey(alias!))
            {
                _rawHeights[alias!] = value;
            }
        }
    }
}
