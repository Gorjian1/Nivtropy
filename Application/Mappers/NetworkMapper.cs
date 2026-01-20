namespace Nivtropy.Application.Mappers;

using Nivtropy.Application.DTOs;
using Nivtropy.Domain.Model;

/// <summary>
/// Интерфейс маппера для конвертации доменных моделей в DTOs.
/// </summary>
public interface INetworkMapper
{
    NetworkSummaryDto ToSummaryDto(LevelingNetwork network);
    NetworkRunSummaryDto ToSummaryDto(Run run);
    ObservationDto ToDto(Observation observation);
    PointDto ToDto(Point point);
}

/// <summary>
/// Реализация маппера для конвертации доменных моделей в DTOs.
/// </summary>
public class NetworkMapper : INetworkMapper
{
    public NetworkSummaryDto ToSummaryDto(LevelingNetwork network)
    {
        return new NetworkSummaryDto(
            Id: network.Id,
            Name: network.Name,
            PointCount: network.Points.Count,
            BenchmarkCount: network.Benchmarks.Count(),
            RunCount: network.Runs.Count,
            TotalStationCount: network.TotalStationCount,
            TotalLengthMeters: network.TotalLength.Meters,
            IsConnected: network.IsConnected(),
            Runs: network.Runs.Select(ToSummaryDto).ToList()
        );
    }

    public NetworkRunSummaryDto ToSummaryDto(Run run)
    {
        return new NetworkRunSummaryDto(
            Id: run.Id,
            Name: run.Name,
            OriginalNumber: run.OriginalNumber,
            StationCount: run.StationCount,
            TotalLengthMeters: run.TotalLength.Meters,
            StartPointCode: run.StartPoint?.Code.Value ?? "",
            EndPointCode: run.EndPoint?.Code.Value ?? "",
            DeltaHSum: run.DeltaHSum,
            ClosureValueMm: run.Closure?.ValueMm,
            ClosureToleranceMm: run.Closure?.ToleranceMm,
            IsClosureWithinTolerance: run.Closure?.IsWithinTolerance,
            IsActive: run.IsActive,
            SystemName: run.System?.Name
        );
    }

    public ObservationDto ToDto(Observation obs)
    {
        return new ObservationDto(
            Id: obs.Id,
            StationIndex: obs.StationIndex,
            FromPointCode: obs.From.Code.Value,
            ToPointCode: obs.To.Code.Value,
            BackReadingM: obs.BackReading.Meters,
            ForeReadingM: obs.ForeReading.Meters,
            BackDistanceM: obs.BackDistance.Meters,
            ForeDistanceM: obs.ForeDistance.Meters,
            DeltaH: obs.DeltaH,
            Correction: obs.Correction,
            AdjustedDeltaH: obs.AdjustedDeltaH,
            FromHeight: obs.From.Height.IsKnown ? obs.From.Height.Value : null,
            ToHeight: obs.To.Height.IsKnown ? obs.To.Height.Value : null
        );
    }

    public PointDto ToDto(Point point)
    {
        return new PointDto(
            Code: point.Code.Value,
            Height: point.Height.IsKnown ? point.Height.Value : null,
            IsKnown: point.Height.IsKnown,
            Type: point.Type.ToString(),
            Degree: point.Degree,
            IsSharedPoint: point.IsSharedPoint
        );
    }
}
