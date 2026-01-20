using System;
using System.Collections.Generic;
using System.Linq;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Enums;
using Nivtropy.Application.Services.TraverseCalculation;

namespace Nivtropy.Application.Services;

public interface ITraverseCalculationWorkflowService
{
    TraverseCalculationResult Calculate(TraverseCalculationRequest request);
}

public class TraverseCalculationWorkflowService : ITraverseCalculationWorkflowService
{
    private readonly ITraverseCalculationService _traverseCalculationService;
    private readonly IClosureCalculationService _closureCalculationService;

    public TraverseCalculationWorkflowService(
        ITraverseCalculationService traverseCalculationService,
        IClosureCalculationService closureCalculationService)
    {
        _traverseCalculationService = traverseCalculationService ?? throw new ArgumentNullException(nameof(traverseCalculationService));
        _closureCalculationService = closureCalculationService ?? throw new ArgumentNullException(nameof(closureCalculationService));
    }

    public TraverseCalculationResult Calculate(TraverseCalculationRequest request)
    {
        var stations = request.Stations.ToList();
        var runs = request.Runs.ToList();

        var runLookup = runs.ToDictionary(GetRunDisplayName, r => r, StringComparer.OrdinalIgnoreCase);
        var traverseGroups = stations
            .GroupBy(r => r.LineName)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var system in request.Systems.OrderBy(s => s.Order))
        {
            var systemTraverseGroups = traverseGroups
                .Where(kvp => runLookup.TryGetValue(kvp.Key, out var run)
                              && run.SystemId == system.Id
                              && run.IsActive)
                .ToList();

            if (systemTraverseGroups.Count == 0)
                continue;

            var availableAdjustedHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var availableRawHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in request.KnownHeights)
            {
                if (request.BenchmarkSystems.TryGetValue(kvp.Key, out var benchSystemId)
                    && benchSystemId == system.Id)
                {
                    availableAdjustedHeights[kvp.Key] = kvp.Value;
                    availableRawHeights[kvp.Key] = kvp.Value;
                }
            }

            bool AnchorChecker(string code) => IsAnchorAllowed(
                code,
                availableAdjustedHeights.ContainsKey,
                request.KnownHeights,
                request.SharedPointStates);

            ProcessSystemTraverseGroups(
                systemTraverseGroups,
                availableAdjustedHeights,
                availableRawHeights,
                AnchorChecker,
                request);

            UpdateArmDifferenceAccumulation(systemTraverseGroups, AnchorChecker, runLookup, request.SharedPointsByRun);
        }

        var activeStations = stations
            .Where(row => runLookup.TryGetValue(row.LineName, out var run) && run.IsActive)
            .ToList();

        if (request.ClassOption != null)
        {
            ApplyArmDifferenceTolerances(activeStations, runLookup, request.ClassOption);
        }

        var totalBackDistance = activeStations.Sum(r => r.BackDistance ?? 0);
        var totalForeDistance = activeStations.Sum(r => r.ForeDistance ?? 0);
        var stationsCount = activeStations.Count;
        var totalAverageDistance = stationsCount > 0 ? (totalBackDistance + totalForeDistance) / 2.0 : 0;

        var totalLengthKilometers = totalBackDistance / 1000.0;
        var closureResult = _closureCalculationService.Calculate(
            activeStations,
            request.MethodOrientationSign,
            stationsCount,
            totalLengthKilometers,
            request.MethodOption,
            request.ClassOption);

        return new TraverseCalculationResult
        {
            Stations = stations,
            Runs = runs,
            Closure = closureResult,
            StationsCount = stationsCount,
            TotalBackDistance = totalBackDistance,
            TotalForeDistance = totalForeDistance,
            TotalAverageDistance = totalAverageDistance
        };
    }

    private void ProcessSystemTraverseGroups(
        List<KeyValuePair<string, List<StationDto>>> systemTraverseGroups,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        Func<string, bool> anchorChecker,
        TraverseCalculationRequest request)
    {
        var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int iteration = 0; iteration < systemTraverseGroups.Count; iteration++)
        {
            bool progress = false;

            foreach (var group in systemTraverseGroups)
            {
                if (processedGroups.Contains(group.Key))
                    continue;

                var groupItems = group.Value;
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

                _traverseCalculationService.ApplyCorrections(
                    groupItems,
                    anchorChecker,
                    request.MethodOrientationSign,
                    request.AdjustmentMode);

                CalculateHeightsForRun(groupItems, availableAdjustedHeights, availableRawHeights, group.Key, request);

                processedGroups.Add(group.Key);
                progress = true;
            }

            if (!progress)
                break;
        }
    }

    private static void CalculateHeightsForRun(
        List<StationDto> items,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        string runName,
        TraverseCalculationRequest request)
    {
        if (items.Count == 0)
            return;

        ResetRowHeights(items);

        var aliasManager = new RunAliasManager(code => IsAnchorAllowed(
            code,
            availableAdjustedHeights.ContainsKey,
            request.KnownHeights,
            request.SharedPointStates));

        InitializeAliases(items, aliasManager);

        var adjusted = new Dictionary<string, double>(availableAdjustedHeights, StringComparer.OrdinalIgnoreCase);
        var raw = new Dictionary<string, double>(availableRawHeights, StringComparer.OrdinalIgnoreCase);
        UpdateLocalHeightsForDisabledSharedPoints(items, runName, availableAdjustedHeights, availableRawHeights, adjusted, raw, request.SharedPointStates);

        PropagateHeightsIteratively(items, adjusted, raw, aliasManager);

        AssignAdjustedHeightsToRows(items, adjusted, availableAdjustedHeights, aliasManager, request);

        var heightTracker = new RunHeightTracker(raw, availableRawHeights);
        CalculateRawHeightsForwardPass(items, heightTracker, aliasManager);
        CalculateRawHeightsBackwardPass(items, adjusted, heightTracker, aliasManager);
        UpdateVirtualStations(items, adjusted, raw, aliasManager);

        UpdateAvailableHeights(adjusted, availableAdjustedHeights, availableRawHeights, aliasManager, code => AllowPropagation(code, request));
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
        string runName,
        Dictionary<string, double> availableAdjustedHeights,
        Dictionary<string, double> availableRawHeights,
        Dictionary<string, double> adjusted,
        Dictionary<string, double> raw,
        IReadOnlyDictionary<string, bool> sharedPointStates)
    {
        var pointsInRun = items.SelectMany(r => new[] { r.BackCode, r.ForeCode })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var pointCode in pointsInRun)
        {
            if (!IsSharedPointEnabled(pointCode!, sharedPointStates))
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
        TraverseCalculationRequest request)
    {
        foreach (var row in items)
        {
            var backAlias = aliasManager.GetAlias(row, isBack: true);
            if (!string.IsNullOrWhiteSpace(backAlias) && adjusted.TryGetValue(backAlias!, out var backZ))
            {
                row.BackHeight = backZ;
                row.IsBackHeightKnown = IsAnchorAllowed(
                    row.BackCode,
                    availableAdjustedHeights.ContainsKey,
                    request.KnownHeights,
                    request.SharedPointStates);
            }

            var foreAlias = aliasManager.GetAlias(row, isBack: false);
            if (!string.IsNullOrWhiteSpace(foreAlias) && adjusted.TryGetValue(foreAlias!, out var foreZ))
            {
                row.ForeHeight = foreZ;
                row.IsForeHeightKnown = IsAnchorAllowed(
                    row.ForeCode,
                    availableAdjustedHeights.ContainsKey,
                    request.KnownHeights,
                    request.SharedPointStates);
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
        Func<string, bool> allowPropagation)
    {
        foreach (var kvp in adjusted)
        {
            if (!aliasManager.IsCopyAlias(kvp.Key) && allowPropagation(kvp.Key))
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

    private static void UpdateArmDifferenceAccumulation(
        List<KeyValuePair<string, List<StationDto>>> traverseGroups,
        Func<string, bool> isAnchor,
        Dictionary<string, RunSummaryDto> runLookup,
        IReadOnlyDictionary<int, List<string>> sharedPointsByRun)
    {
        foreach (var group in traverseGroups)
        {
            if (!runLookup.TryGetValue(group.Key, out var run))
                continue;

            double? accumulation = null;
            double? totalDistanceBack = null;
            double? totalDistanceFore = null;

            foreach (var row in group.Value)
            {
                if (row.ArmDifference.HasValue)
                    accumulation = (accumulation ?? 0) + row.ArmDifference.Value;

                if (row.BackDistance.HasValue)
                    totalDistanceBack = (totalDistanceBack ?? 0) + row.BackDistance.Value;

                if (row.ForeDistance.HasValue)
                    totalDistanceFore = (totalDistanceFore ?? 0) + row.ForeDistance.Value;
            }

            var knownPointsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in group.Value)
            {
                if (!string.IsNullOrWhiteSpace(row.BackCode) && isAnchor(row.BackCode))
                    knownPointsSet.Add(row.BackCode);
                if (!string.IsNullOrWhiteSpace(row.ForeCode) && isAnchor(row.ForeCode))
                    knownPointsSet.Add(row.ForeCode);
            }

            run.TotalDistanceBack = totalDistanceBack;
            run.TotalDistanceFore = totalDistanceFore;
            run.ArmDifferenceAccumulation = accumulation;
            run.KnownPointsCount = knownPointsSet.Count;

            if (sharedPointsByRun.TryGetValue(run.Index, out var sharedCodesForRun))
                run.SharedPointCodes = sharedCodesForRun.ToList();
            else
                run.SharedPointCodes = new List<string>();
        }
    }

    private static void ApplyArmDifferenceTolerances(
        IReadOnlyList<StationDto> stations,
        Dictionary<string, RunSummaryDto> runLookup,
        LevelingClassOption selectedClass)
    {
        var stationTolerance = selectedClass.ArmDifferenceToleranceStation;
        var accumulationTolerance = selectedClass.ArmDifferenceToleranceAccumulation;

        foreach (var row in stations)
        {
            if (row.ArmDifference.HasValue)
                row.IsArmDifferenceExceeded = Math.Abs(row.ArmDifference.Value) > stationTolerance;
            else
                row.IsArmDifferenceExceeded = false;
        }

        var lineGroups = stations.GroupBy(r => r.LineName);
        foreach (var group in lineGroups)
        {
            if (runLookup.TryGetValue(group.Key, out var run) && run.ArmDifferenceAccumulation.HasValue)
            {
                run.IsArmDifferenceAccumulationExceeded =
                    Math.Abs(run.ArmDifferenceAccumulation.Value) > accumulationTolerance;
            }
        }
    }

    private static bool IsAnchorAllowed(
        string? code,
        Func<string, bool> contains,
        IReadOnlyDictionary<string, double> knownHeights,
        IReadOnlyDictionary<string, bool> sharedPointStates)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return contains(code) && (knownHeights.ContainsKey(code) || IsSharedPointEnabled(code, sharedPointStates));
    }

    private static bool AllowPropagation(string code, TraverseCalculationRequest request)
    {
        return request.KnownHeights.ContainsKey(code) || IsSharedPointEnabled(code, request.SharedPointStates);
    }

    private static bool IsSharedPointEnabled(string code, IReadOnlyDictionary<string, bool> sharedPointStates)
    {
        return !sharedPointStates.TryGetValue(code.Trim(), out var enabled) || enabled;
    }

    private static string GetRunDisplayName(RunSummaryDto run)
    {
        return !string.IsNullOrWhiteSpace(run.OriginalLineNumber)
            ? $"Ход {run.OriginalLineNumber}"
            : $"Ход {run.Index:D2}";
    }

    private static string GetPointCodeForRun(string pointCode, string runName)
    {
        return $"{pointCode} ({runName})";
    }
}
