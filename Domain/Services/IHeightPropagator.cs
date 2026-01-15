namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

/// <summary>
/// Сервис распространения высот от реперов по графу сети.
/// </summary>
public interface IHeightPropagator
{
    /// <summary>
    /// Распространить высоты от известных точек (реперов) по всей сети.
    /// </summary>
    /// <param name="network">Нивелирная сеть</param>
    /// <returns>Количество точек, для которых вычислена высота</returns>
    int PropagateHeights(LevelingNetwork network);

    /// <summary>
    /// Распространить высоты в рамках одного хода.
    /// </summary>
    /// <param name="run">Ход</param>
    /// <returns>Количество точек, для которых вычислена высота</returns>
    int PropagateHeightsInRun(Run run);
}
