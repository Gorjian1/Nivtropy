namespace Nivtropy.Domain.Enums;

/// <summary>
/// Режим уравнивания нивелирного хода
/// </summary>
public enum AdjustmentMode
{
    /// <summary>Без уравнивания</summary>
    None = 0,
    /// <summary>Локальное уравнивание (по ходу)</summary>
    Local = 1,
    /// <summary>Сетевое уравнивание (МНК)</summary>
    Network = 2
}
