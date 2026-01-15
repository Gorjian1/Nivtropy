namespace Nivtropy.Infrastructure.Persistence;

using Nivtropy.Domain.Model;

/// <summary>
/// Интерфейс репозитория для LevelingNetwork (Domain Layer).
/// </summary>
public interface INetworkRepository
{
    Task<LevelingNetwork?> GetByIdAsync(Guid id);
    Task SaveAsync(LevelingNetwork network);
}

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
