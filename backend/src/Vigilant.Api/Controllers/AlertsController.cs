using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Api.Hubs;
using Vigilant.Application.Alerts.GetAmlAlerts;
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
        Description = "Evaluates circular flow, smurfing, rapid fan-out, shared device/IP, round-trip, and PEP offshore graph patterns.")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AmlAlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AmlAlertDto>>> GetAlerts(
        [FromQuery] int maxTransfers = 8,
        [FromQuery] int lookbackHours = 168,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var alerts = await sender.Send(new GetAmlAlertsQuery(maxTransfers, lookbackHours, limit), cancellationToken);

        if (alerts.Count > 0)
        {
            await alertsHub.Clients.Group(AlertsHub.AllAlertsGroup)
                .SendAsync("alerts.detected", alerts, cancellationToken);
        }

        return Ok(alerts);
    }
}
