using System.Collections.Generic;
using Nivtropy.Domain.DTOs;
using Nivtropy.Models;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// Группа аннотированных измерений для одного хода.
/// </summary>
public class RunAnnotationGroupDto
{
    public RunSummaryDto Summary { get; init; } = new();
    public List<MeasurementRecord> Records { get; init; } = new();
}
