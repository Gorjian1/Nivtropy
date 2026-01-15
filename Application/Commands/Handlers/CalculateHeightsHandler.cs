namespace Nivtropy.Application.Commands.Handlers;

using Nivtropy.Domain.Services;
using Nivtropy.Infrastructure.Persistence;

/// <summary>
/// Handler для команды вычисления высот.
/// Оркестрирует Domain Services для выполнения расчётов.
/// </summary>
public class CalculateHeightsHandler
{
    private readonly INetworkRepository _repository;
    private readonly IHeightPropagator _heightPropagator;
    private readonly IClosureDistributor _closureDistributor;

    public CalculateHeightsHandler(
        INetworkRepository repository,
        IHeightPropagator heightPropagator,
        IClosureDistributor closureDistributor)
    {
        _repository = repository;
        _heightPropagator = heightPropagator;
        _closureDistributor = closureDistributor;
    }

    public async Task<CalculateHeightsResult> HandleAsync(CalculateHeightsCommand command)
    {
        var network = await _repository.GetByIdAsync(command.NetworkId);
        if (network == null)
            throw new InvalidOperationException($"Network {command.NetworkId} not found");

        var closures = new List<RunClosureDto>();

        // 1. Вычисляем невязки и распределяем поправки
        foreach (var run in network.Runs.Where(r => r.IsActive))
        {
            // Допуск: 10мм * √(L км)
            var toleranceMm = 10.0 * Math.Sqrt(run.TotalLength.Kilometers);
            run.CalculateClosure(toleranceMm);

            if (run.Closure?.IsWithinTolerance == true)
            {
                _closureDistributor.DistributeClosureWithSections(run);
            }

            closures.Add(new RunClosureDto(
                run.Id,
                run.Name,
                run.Closure?.ValueMm,
                run.Closure?.ToleranceMm,
                run.Closure?.IsWithinTolerance
            ));
        }

        // 2. Распространяем высоты от реперов
        var calculatedCount = _heightPropagator.PropagateHeights(network);

        // 3. Сохраняем
        await _repository.SaveAsync(network);

        return new CalculateHeightsResult(calculatedCount, closures);
    }
}
