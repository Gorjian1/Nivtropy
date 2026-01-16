namespace Nivtropy.Domain.DTOs;

/// <summary>
/// DTO общей точки для расчётов связности.
/// </summary>
public class SharedPointDto
{
    public string Code { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<int> RunIndexes { get; set; } = new();
}
