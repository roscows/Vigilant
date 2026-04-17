using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Graph.GetGraphOverview;

public sealed record GetGraphOverviewQuery(int NodeLimit = 250) : IRequest<EntityGraphDto>;

public sealed class GetGraphOverviewQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetGraphOverviewQuery, EntityGraphDto>
{
    public Task<EntityGraphDto> Handle(GetGraphOverviewQuery request, CancellationToken cancellationToken)
    {
        var nodeLimit = Math.Clamp(request.NodeLimit, 25, 500);
        return neo4jRepository.GetGraphOverviewAsync(nodeLimit, cancellationToken);
    }
}
