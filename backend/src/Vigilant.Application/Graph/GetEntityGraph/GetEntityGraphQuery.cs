using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Graph.GetEntityGraph;

public sealed record GetEntityGraphQuery(
    string AccountIban,
    int Depth = 4) : IRequest<EntityGraphDto>;

public sealed class GetEntityGraphQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetEntityGraphQuery, EntityGraphDto>
{
    public Task<EntityGraphDto> Handle(GetEntityGraphQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountIban))
        {
            throw new ArgumentException("Account IBAN is required.", nameof(request));
        }

        var depth = Math.Clamp(request.Depth, 1, 8);
        return neo4jRepository.GetEntityGraphAsync(request.AccountIban.Trim(), depth, cancellationToken);
    }
}
