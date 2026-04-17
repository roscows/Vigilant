using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Application.Common.Graph;
using Vigilant.Application.Graph.GetEntityGraph;
using Vigilant.Application.Graph.GetGraph;
using Vigilant.Application.Graph.GetGraphOverview;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/graph")]
[Produces("application/json")]
public sealed class GraphController(ISender sender) : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Returns graph network data",
        Description = "Returns either a focused account subgraph or a transaction-window full graph.")]
    [ProducesResponseType(typeof(EntityGraphDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntityGraphDto>> GetGraph(
        [FromQuery] string? ibanFocus = null,
        [FromQuery] int depth = 2,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var graph = await sender.Send(new GetGraphQuery(ibanFocus, depth, from, to, limit), cancellationToken);
        return Ok(graph);
    }

    [HttpGet("overview")]
    [SwaggerOperation(
        Summary = "Returns the default graph network overview",
        Description = "Returns a bounded full-network graph overview for the analyst dashboard without creating demo data.")]
    [ProducesResponseType(typeof(EntityGraphDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntityGraphDto>> GetGraphOverview(
        [FromQuery] int nodeLimit = 250,
        CancellationToken cancellationToken = default)
    {
        var graph = await sender.Send(new GetGraphOverviewQuery(nodeLimit), cancellationToken);
        return Ok(graph);
    }

    [HttpGet("accounts/{iban}")]
    [SwaggerOperation(
        Summary = "Returns graph network data for an account",
        Description = "Returns normalized nodes and edges that can feed force-directed graph visualizations.")]
    [ProducesResponseType(typeof(EntityGraphDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EntityGraphDto>> GetAccountGraph(
        [FromRoute] string iban,
        [FromQuery] int depth = 4,
        CancellationToken cancellationToken = default)
    {
        var graph = await sender.Send(new GetEntityGraphQuery(iban, depth), cancellationToken);
        return Ok(graph);
    }
}
