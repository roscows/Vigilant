using Microsoft.AspNetCore.SignalR;
using Vigilant.Api.Hubs;
using Vigilant.Application.Common.Alerts;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Api.Services;

public sealed class SignalRAlertPublisher(IHubContext<AlertsHub> alertsHub) : IAlertPublisher
{
    public Task PublishAsync(IReadOnlyCollection<AmlAlertDto> alerts, CancellationToken cancellationToken)
    {
        if (alerts.Count == 0)
        {
            return Task.CompletedTask;
        }

        return alertsHub.Clients.Group(AlertsHub.AllAlertsGroup)
            .SendAsync("alerts.detected", alerts, cancellationToken);
    }
}
