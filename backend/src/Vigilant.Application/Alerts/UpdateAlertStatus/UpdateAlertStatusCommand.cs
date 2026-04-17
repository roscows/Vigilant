using MediatR;
using Vigilant.Application.Common.Alerts;
using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;

namespace Vigilant.Application.Alerts.UpdateAlertStatus;

public sealed record UpdateAlertStatusCommand(
    Guid AlertId,
    AlertStatus NewStatus,
    string AnalystName,
    string Comment) : IRequest<AlertNode?>;

public sealed class UpdateAlertStatusCommandHandler(
    INeo4jRepository neo4jRepository,
    IAlertPublisher alertPublisher) : IRequestHandler<UpdateAlertStatusCommand, AlertNode?>
{
    public async Task<AlertNode?> Handle(UpdateAlertStatusCommand request, CancellationToken cancellationToken)
    {
        await neo4jRepository.UpdateAlertStatusAsync(
            request.AlertId,
            request.NewStatus,
            request.AnalystName,
            request.Comment,
            cancellationToken);

        var updatedAlert = await neo4jRepository.GetAlertByIdAsync(request.AlertId, cancellationToken);
        if (updatedAlert is not null)
        {
            await alertPublisher.PublishUpdatedAsync(updatedAlert, cancellationToken);
        }

        return updatedAlert;
    }
}
