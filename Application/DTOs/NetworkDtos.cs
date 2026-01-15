namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO для сводной информации о нивелирной сети.
/// Используется для передачи данных в UI без зависимости от Domain.
/// </summary>
public record NetworkSummaryDto(
    Guid Id,
    string Name,
    int PointCount,
    int BenchmarkCount,
    int RunCount,
    int TotalStationCount,
    double TotalLengthMeters,
    bool IsConnected,
    IReadOnlyList<RunSummaryDto> Runs
);

/// <summary>
/// DTO для сводной информации о ходе.
/// </summary>
public record RunSummaryDto(
    Guid Id,
    string Name,
    string? OriginalNumber,
    int StationCount,
    double TotalLengthMeters,
    string StartPointCode,
    string EndPointCode,
    double DeltaHSum,
    double? ClosureValueMm,
    double? ClosureToleranceMm,
    bool? IsClosureWithinTolerance,
    bool IsActive,
    string? SystemName
);

/// <summary>
/// DTO для наблюдения (станции).
/// </summary>
public record ObservationDto(
    Guid Id,
    int StationIndex,
    string FromPointCode,
    string ToPointCode,
    double BackReadingM,
    double ForeReadingM,
    double BackDistanceM,
    double ForeDistanceM,
    double DeltaH,
    double Correction,
    double AdjustedDeltaH,
    double? FromHeight,
    double? ToHeight
);

/// <summary>
/// DTO для точки нивелирования.
/// </summary>
public record PointDto(
    string Code,
    double? Height,
    bool IsKnown,
    string Type,
    int Degree,
    bool IsSharedPoint
);
