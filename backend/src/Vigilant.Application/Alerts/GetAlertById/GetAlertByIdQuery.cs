using MediatR;
using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;

namespace Vigilant.Application.Alerts.GetAlertById;

public sealed record GetAlertByIdQuery(Guid AlertId) : IRequest<AlertNode?>;

public sealed class GetAlertByIdQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetAlertByIdQuery, AlertNode?>
{
    public Task<AlertNode?> Handle(GetAlertByIdQuery request, CancellationToken cancellationToken)
    {
        return neo4jRepository.GetAlertByIdAsync(request.AlertId, cancellationToken);
    }
}
