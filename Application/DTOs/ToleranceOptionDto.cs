using Nivtropy.Application.Services;

namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO опции допуска для расчёта невязки.
/// </summary>
public class ToleranceOptionDto
{
    public string Code { get; set; } = string.Empty;
    public ToleranceMode Mode { get; set; }
    public double Coefficient { get; set; }
}
