using Microsoft.AspNetCore.SignalR;

namespace Vigilant.Api.Hubs;

public sealed class AlertsHub : Hub
{
    public const string AllAlertsGroup = "aml-alerts";

    public Task SubscribeToAlerts()
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, AllAlertsGroup);
    }

    public Task UnsubscribeFromAlerts()
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, AllAlertsGroup);
    }
}
