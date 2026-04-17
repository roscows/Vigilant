using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Graph.GetGraph;

public sealed record GetGraphQuery(
    string? IbanFocus,
    int Depth,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Limit) : IRequest<EntityGraphDto>;

public sealed class GetGraphQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetGraphQuery, EntityGraphDto>
{
    public Task<EntityGraphDto> Handle(GetGraphQuery request, CancellationToken cancellationToken)
    {
        return neo4jRepository.GetGraphAsync(
            request.IbanFocus,
            Math.Clamp(request.Depth, 1, 8),
            request.From,
            request.To,
            Math.Clamp(request.Limit, 25, 1_000),
            cancellationToken);
    }
}
