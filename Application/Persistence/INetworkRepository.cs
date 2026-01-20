using Nivtropy.Domain.Model;

namespace Nivtropy.Application.Persistence;

/// <summary>
/// Интерфейс репозитория для LevelingNetwork (Domain Layer).
/// </summary>
public interface INetworkRepository
{
    Task<LevelingNetwork?> GetByIdAsync(Guid id);
    Task SaveAsync(LevelingNetwork network);
}
