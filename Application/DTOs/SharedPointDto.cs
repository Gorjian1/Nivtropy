namespace Nivtropy.Application.DTOs;

/// <summary>
/// DTO общей точки для расчётов связности (замена SharedPointLinkItem).
/// </summary>
public class SharedPointDto
{
    public string Code { get; set; } = "";
    public bool IsEnabled { get; set; }
    public List<int> RunIndexes { get; set; } = new();
}
