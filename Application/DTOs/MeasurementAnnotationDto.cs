using Nivtropy.Domain.Model;

namespace Nivtropy.Application.DTOs;

public class MeasurementAnnotationDto
{
    public MeasurementRecord Measurement { get; init; } = new();
    public int ShotIndexWithinLine { get; init; }
    public bool IsLineStart { get; init; }
    public bool IsLineEnd { get; init; }
}
