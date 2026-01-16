namespace Nivtropy.Application.Commands;

using System;
using Nivtropy.Domain.Enums;

/// <summary>
/// Команда для вычисления высот в сети.
/// </summary>
public record CalculateHeightsCommand(Guid NetworkId, AdjustmentMode Mode);

/// <summary>
/// Результат вычисления высот.
/// </summary>
public record CalculateHeightsResult(
    int CalculatedPointCount,
    IReadOnlyList<RunClosureDto> Closures
);

/// <summary>
/// DTO для информации о невязке хода.
/// </summary>
public record RunClosureDto(
    Guid RunId,
    string RunName,
    double? ClosureMm,
    double? ToleranceMm,
    bool? IsWithinTolerance
);
