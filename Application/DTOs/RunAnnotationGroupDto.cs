using System.Collections.Generic;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Группа аннотированных измерений для одного хода.
/// </summary>
public class RunAnnotationGroupDto
{
    public RunSummaryDto Summary { get; init; } = new();
    public List<MeasurementDto> Records { get; init; } = new();
}
