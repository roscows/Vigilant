using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Api.Contracts.Alerts;
using Vigilant.Application.Alerts.GetAlertById;
using Vigilant.Application.Alerts.GetAlerts;
using Vigilant.Application.Alerts.UpdateAlertStatus;
using Vigilant.Domain.Alerts;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Produces("application/json")]
public sealed class AlertsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Returns persisted AML alerts",
        Description = "Returns alert lifecycle records with optional status and date filters.")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AlertNode>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AlertNode>>> GetAlerts(
        [FromQuery] AlertStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var alerts = await sender.Send(new GetAlertsQuery(status, from, to), cancellationToken);
        return Ok(alerts);
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Returns a persisted AML alert by ID",
        Description = "Returns the alert lifecycle record and audit log for a single alert.")]
    [ProducesResponseType(typeof(AlertNode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertNode>> GetAlertById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var alert = await sender.Send(new GetAlertByIdQuery(id), cancellationToken);
        return alert is null ? NotFound() : Ok(alert);
    }

    [HttpPatch("{id:guid}/status")]
    [SwaggerOperation(
        Summary = "Updates an AML alert status",
        Description = "Updates the alert lifecycle status and appends an audit entry.")]
    [ProducesResponseType(typeof(AlertNode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertNode>> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAlertStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AlertStatus>(request.NewStatus, ignoreCase: true, out var newStatus))
        {
            return BadRequest($"Unsupported alert status '{request.NewStatus}'.");
        }

        try
        {
            var updatedAlert = await sender.Send(
                new UpdateAlertStatusCommand(id, newStatus, request.AnalystName, request.Comment),
                cancellationToken);

            return updatedAlert is null ? NotFound() : Ok(updatedAlert);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
