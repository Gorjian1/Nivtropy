namespace Nivtropy.Application.Commands.Handlers;

using Nivtropy.Application.Services;
using Nivtropy.Application.DTOs;
using Nivtropy.Application.Persistence;
using Nivtropy.Domain.Model;
using Nivtropy.Domain.ValueObjects;

public class BuildNetworkHandler
{
    private readonly INetworkRepository _repository;
    private readonly IRunAnnotationService _annotationService;

    public BuildNetworkHandler(
        INetworkRepository repository,
        IRunAnnotationService annotationService)
    {
        _repository = repository;
        _annotationService = annotationService;
    }

    public async Task<Guid> HandleAsync(BuildNetworkCommand command)
    {
        var networkName = string.IsNullOrWhiteSpace(command.ProjectName)
            ? "Новый проект"
            : command.ProjectName.Trim();

        var network = new LevelingNetwork(networkName);
        var defaultSystem = network.CreateSystem("Основная");

        if (command.Records.Count == 0)
        {
            await _repository.SaveAsync(network);
            return network.Id;
        }

        var errors = new List<string>();
        var groups = _annotationService.AnnotateRuns(command.Records.ToList());

        foreach (var group in groups)
        {
            var runName = BuildRunName(group.Summary);
            var run = network.CreateRun(runName);
            run.OriginalNumber = group.Summary.OriginalLineNumber;
            network.AddRunToSystem(run, defaultSystem);

            BuildRunObservations(
                network,
                run,
                runName,
                group.Records.Select(r => r.Measurement).ToList(),
                command.SharedPointStates,
                errors);
        }

        foreach (var kvp in command.KnownHeights)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;

            var code = new PointCode(kvp.Key.Trim());
            if (network.GetPoint(code) == null)
                continue;

            network.SetBenchmarkHeight(code, Height.Known(kvp.Value));
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Ошибка импорта: обнаружены незакрытые пары отсчётов.\n" +
                string.Join("\n", errors));
        }

        await _repository.SaveAsync(network);
        return network.Id;
    }

    private static string BuildRunName(RunSummaryDto summary)
    {
        if (!string.IsNullOrWhiteSpace(summary.OriginalLineNumber))
            return $"Ход {summary.OriginalLineNumber}";

        return $"Ход {summary.Index:D2}";
    }

    private static void BuildRunObservations(
        LevelingNetwork network,
        Run run,
        string runName,
        IReadOnlyList<MeasurementRecord> records,
        IReadOnlyDictionary<string, bool> sharedPointStates,
        List<string> errors)
    {
        if (records.Count == 0)
            return;

        string mode = "BF";
        PendingObservation? pending = null;

        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.LineMarker))
            {
                if (record.LineMarker == "Start-Line" && !string.IsNullOrWhiteSpace(record.Mode))
                {
                    var modeUpper = record.Mode.Trim().ToUpperInvariant();
                    if (modeUpper == "BF" || modeUpper == "FB")
                        mode = modeUpper;
                }

                continue;
            }

            bool isBF = mode == "BF";
            var pointCode = NormalizePointCode(record.Target);
            if (string.IsNullOrWhiteSpace(pointCode))
                continue;

            if (record.Rb_m.HasValue)
            {
                if (pending == null)
                {
                    pending = new PendingObservation();
                    if (isBF)
                    {
                        pending.BackCode = pointCode;
                        pending.BackReading = record.Rb_m;
                        pending.BackDistance = record.HD_m;
                    }
                    else
                    {
                        pending.ForeCode = pointCode;
                        pending.BackReading = record.Rb_m;
                        pending.ForeDistance = record.HD_m;
                    }
                }
                else
                {
                    if (isBF)
                    {
                        pending.BackReading ??= record.Rb_m;
                        pending.BackDistance ??= record.HD_m;
                        pending.BackCode ??= pointCode;
                    }
                    else
                    {
                        pending.BackReading ??= record.Rb_m;
                        pending.ForeDistance ??= record.HD_m;
                        pending.ForeCode ??= pointCode;
                    }

                    AddObservationIfComplete(network, run, runName, pending, sharedPointStates, errors);
                    pending = null;
                }

                continue;
            }

            if (record.Rf_m.HasValue)
            {
                if (pending == null)
                {
                    pending = new PendingObservation();
                    if (isBF)
                    {
                        pending.ForeCode = pointCode;
                        pending.ForeReading = record.Rf_m;
                        pending.ForeDistance = record.HD_m;
                    }
                    else
                    {
                        pending.BackCode = pointCode;
                        pending.ForeReading = record.Rf_m;
                        pending.BackDistance = record.HD_m;
                    }
                }
                else
                {
                    if (isBF)
                    {
                        pending.ForeReading ??= record.Rf_m;
                        pending.ForeDistance ??= record.HD_m;
                        pending.ForeCode ??= pointCode;
                    }
                    else
                    {
                        pending.ForeReading ??= record.Rf_m;
                        pending.BackDistance ??= record.HD_m;
                        pending.BackCode ??= pointCode;
                    }

                    AddObservationIfComplete(network, run, runName, pending, sharedPointStates, errors);
                    pending = null;
                }
            }
        }

        if (pending != null)
        {
            errors.Add($"Ход \"{runName}\": незакрытая пара отсчётов ({pending.BackCode ?? "?"} → {pending.ForeCode ?? "?"})");
        }
    }

    private static void AddObservationIfComplete(
        LevelingNetwork network,
        Run run,
        string runName,
        PendingObservation pending,
        IReadOnlyDictionary<string, bool> sharedPointStates,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(pending.BackCode) ||
            string.IsNullOrWhiteSpace(pending.ForeCode) ||
            !pending.BackReading.HasValue ||
            !pending.ForeReading.HasValue)
        {
            errors.Add($"Ход \"{runName}\": неполная пара отсчётов ({pending.BackCode ?? "?"} → {pending.ForeCode ?? "?"})");
            return;
        }

        var backCode = MapPointCode(pending.BackCode, runName, sharedPointStates);
        var foreCode = MapPointCode(pending.ForeCode, runName, sharedPointStates);

        var backDistance = pending.BackDistance ?? 0;
        var foreDistance = pending.ForeDistance ?? 0;

        network.AddObservation(
            run,
            new PointCode(backCode),
            new PointCode(foreCode),
            Reading.FromMeters(pending.BackReading.Value),
            Reading.FromMeters(pending.ForeReading.Value),
            Distance.FromMeters(backDistance),
            Distance.FromMeters(foreDistance));
    }

    private static string MapPointCode(
        string code,
        string runName,
        IReadOnlyDictionary<string, bool> sharedPointStates)
    {
        var trimmed = code.Trim();
        if (sharedPointStates.TryGetValue(trimmed, out var enabled) && !enabled)
        {
            return $"{trimmed} ({runName})";
        }

        return trimmed;
    }

    private static string? NormalizePointCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        var tokens = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 ? tokens[0] : null;
    }

    private sealed class PendingObservation
    {
        public string? BackCode { get; set; }
        public string? ForeCode { get; set; }
        public double? BackReading { get; set; }
        public double? ForeReading { get; set; }
        public double? BackDistance { get; set; }
        public double? ForeDistance { get; set; }
    }
}
