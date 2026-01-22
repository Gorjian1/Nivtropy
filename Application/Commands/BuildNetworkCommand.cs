using Nivtropy.Domain.Model;

namespace Nivtropy.Application.Commands;

public record BuildNetworkCommand(
    IReadOnlyList<MeasurementRecord> Records,
    IReadOnlyDictionary<string, double> KnownHeights,
    IReadOnlyDictionary<string, bool> SharedPointStates,
    string? ProjectName
);
