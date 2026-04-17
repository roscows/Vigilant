using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Application.Common.Graph;
using Vigilant.Application.Graph.GetEntityGraph;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/graph")]
[Produces("application/json")]
public sealed class GraphController(ISender sender) : ControllerBase
{
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
