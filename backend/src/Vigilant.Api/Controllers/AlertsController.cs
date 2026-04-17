using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Api.Hubs;
using Vigilant.Application.Alerts.GetCircularFlowAlerts;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Produces("application/json")]
public sealed class AlertsController(
    ISender sender,
    IHubContext<AlertsHub> alertsHub) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Returns AML alerts",
        Description = "Currently evaluates circular-flow graph patterns over recent transaction paths.")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AmlAlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AmlAlertDto>>> GetAlerts(
        [FromQuery] int maxTransfers = 4,
        [FromQuery] int lookbackHours = 24,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var alerts = await sender.Send(
            new GetCircularFlowAlertsQuery(maxTransfers, lookbackHours, limit),
            cancellationToken);

        if (alerts.Count > 0)
        {
            await alertsHub.Clients.Group(AlertsHub.AllAlertsGroup)
                .SendAsync("alerts.detected", alerts, cancellationToken);
        }

        return Ok(alerts);
    }
}
