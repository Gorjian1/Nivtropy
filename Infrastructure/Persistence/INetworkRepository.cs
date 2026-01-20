using Nivtropy.Application.Persistence;
using Nivtropy.Domain.Model;

namespace Nivtropy.Infrastructure.Persistence;

/// <summary>
/// In-memory реализация репозитория для прототипирования.
/// </summary>
public class InMemoryNetworkRepository : INetworkRepository
{
    private readonly Dictionary<Guid, LevelingNetwork> _networks = new();
    private LevelingNetwork? _current;

    public Task<LevelingNetwork?> GetByIdAsync(Guid id)
    {
        _networks.TryGetValue(id, out var network);
        return Task.FromResult(network);
    }

    public Task SaveAsync(LevelingNetwork network)
    {
        _networks[network.Id] = network;
        _current = network;
        return Task.CompletedTask;
    }

    /// <summary>Получить текущую сеть (для упрощения)</summary>
    public LevelingNetwork? GetCurrent() => _current;
}
