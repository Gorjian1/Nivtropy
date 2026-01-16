namespace Nivtropy.Domain.Enums;

/// <summary>
/// Режим замыкания хода
/// </summary>
public enum TraverseClosureMode
{
    /// <summary>Открытый ход - без замыкания</summary>
    Open,
    /// <summary>Простое замыкание - один репер в начале и конце</summary>
    Simple,
    /// <summary>Локальное уравнивание - несколько реперов внутри хода</summary>
    Local
}
