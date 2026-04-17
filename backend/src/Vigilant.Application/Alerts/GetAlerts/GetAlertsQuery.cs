using MediatR;
using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;

namespace Vigilant.Application.Alerts.GetAlerts;

public sealed record GetAlertsQuery(
    AlertStatus? StatusFilter,
    DateTime? From,
    DateTime? To) : IRequest<IReadOnlyCollection<AlertNode>>;

public sealed class GetAlertsQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetAlertsQuery, IReadOnlyCollection<AlertNode>>
{
    public async Task<IReadOnlyCollection<AlertNode>> Handle(
        GetAlertsQuery request,
        CancellationToken cancellationToken)
    {
        return await neo4jRepository.GetAlertsAsync(
            request.StatusFilter,
            request.From,
            request.To,
            cancellationToken);
    }
}
