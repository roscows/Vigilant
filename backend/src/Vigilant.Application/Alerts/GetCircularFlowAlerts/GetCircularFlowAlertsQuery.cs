using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Alerts.GetCircularFlowAlerts;

public sealed record GetCircularFlowAlertsQuery(
    int MaxTransfers = 4,
    int LookbackHours = 24,
    int Limit = 25) : IRequest<IReadOnlyCollection<AmlAlertDto>>;

public sealed class GetCircularFlowAlertsQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetCircularFlowAlertsQuery, IReadOnlyCollection<AmlAlertDto>>
{
    public Task<IReadOnlyCollection<AmlAlertDto>> Handle(
        GetCircularFlowAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var maxTransfers = Math.Clamp(request.MaxTransfers, 2, 10);
        var lookbackHours = Math.Clamp(request.LookbackHours, 1, 24 * 30);
        var limit = Math.Clamp(request.Limit, 1, 250);

        return neo4jRepository.FindCircularFlowsAsync(
            maxTransfers,
            TimeSpan.FromHours(lookbackHours),
            limit,
            cancellationToken);
    }
}
