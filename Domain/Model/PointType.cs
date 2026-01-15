namespace Nivtropy.Domain.Model;

/// <summary>
/// Тип точки нивелирования
/// </summary>
public enum PointType
{
    /// <summary>Репер - точка с известной высотой</summary>
    Benchmark,

    /// <summary>Связующая точка - используется для связи станций</summary>
    TurningPoint,

    /// <summary>Промежуточная точка</summary>
    Intermediate
}
