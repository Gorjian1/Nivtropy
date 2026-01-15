namespace Nivtropy.Domain.Services;

using Nivtropy.Domain.Model;

/// <summary>
/// Сервис распределения невязки по наблюдениям хода.
/// </summary>
public interface IClosureDistributor
{
    /// <summary>
    /// Распределить невязку по наблюдениям хода.
    /// </summary>
    /// <param name="run">Ход</param>
    void DistributeClosure(Run run);

    /// <summary>
    /// Распределить невязку с разбиением по секциям (между известными точками).
    /// </summary>
    /// <param name="run">Ход</param>
    void DistributeClosureWithSections(Run run);
}
