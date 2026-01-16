namespace Nivtropy.Application.Queries;

using Nivtropy.Domain.DTOs;
using Nivtropy.Application.Mappers;
using Nivtropy.Infrastructure.Persistence;

/// <summary>
/// Query для получения сводки по сети.
/// </summary>
public record GetNetworkSummaryQuery(Guid NetworkId);

/// <summary>
/// Handler для получения сводки по сети.
/// </summary>
public class GetNetworkSummaryHandler
{
    private readonly INetworkRepository _repository;
    private readonly INetworkMapper _mapper;

    public GetNetworkSummaryHandler(
        INetworkRepository repository,
        INetworkMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<NetworkSummaryDto?> HandleAsync(GetNetworkSummaryQuery query)
    {
        var network = await _repository.GetByIdAsync(query.NetworkId);
        return network != null ? _mapper.ToSummaryDto(network) : null;
    }
}
