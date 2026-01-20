namespace Nivtropy.Application.Commands.Handlers;

using Nivtropy.Application.Enums;
using Nivtropy.Application.Persistence;
using Nivtropy.Domain.Services;

/// <summary>
/// Handler для команды вычисления высот.
/// Оркестрирует Domain Services для выполнения расчётов.
/// </summary>
public class CalculateHeightsHandler
{
    private readonly INetworkRepository _repository;
    private readonly IHeightPropagator _heightPropagator;
    private readonly IClosureDistributor _closureDistributor;
    private readonly INetworkAdjuster _networkAdjuster;

    public CalculateHeightsHandler(
        INetworkRepository repository,
        IHeightPropagator heightPropagator,
        IClosureDistributor closureDistributor,
        INetworkAdjuster networkAdjuster)
    {
        _repository = repository;
        _heightPropagator = heightPropagator;
        _closureDistributor = closureDistributor;
        _networkAdjuster = networkAdjuster;
    }

    public async Task<CalculateHeightsResult> HandleAsync(CalculateHeightsCommand command)
    {
        var network = await _repository.GetByIdAsync(command.NetworkId);
        if (network == null)
            throw new InvalidOperationException($"Network {command.NetworkId} not found");

        var closures = new List<RunClosureDto>();

        network.ResetCorrections();

        // 1. Вычисляем невязки и распределяем поправки
        foreach (var run in network.Runs.Where(r => r.IsActive))
        {
            // Допуск: 10мм * √(L км)
            var toleranceMm = 10.0 * Math.Sqrt(run.TotalLength.Kilometers);
            run.CalculateClosure(toleranceMm);

            if (command.Mode == AdjustmentMode.Local && run.Closure?.IsWithinTolerance == true)
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

        if (command.Mode == AdjustmentMode.Network)
        {
            _networkAdjuster.Adjust(network);
        }

        // 2. Распространяем высоты от реперов
        var calculatedCount = _heightPropagator.PropagateHeights(network);

        // 3. Сохраняем
        await _repository.SaveAsync(network);

        return new CalculateHeightsResult(calculatedCount, closures);
    }
}
