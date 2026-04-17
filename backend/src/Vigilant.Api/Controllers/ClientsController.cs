using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Application.Clients.Risk;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Produces("application/json")]
public sealed class ClientsController(IRiskScoreService riskScoreService) : ControllerBase
{
    [HttpGet("{id}/risk")]
    [SwaggerOperation(
        Summary = "Returns a client's computed AML risk score",
        Description = "Returns the persisted client risk score and current contributing AML alert categories.")]
    [ProducesResponseType(typeof(ClientRiskScoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientRiskScoreDto>> GetRisk(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var risk = await riskScoreService.GetAsync(id, cancellationToken);
        return risk is null ? NotFound() : Ok(risk);
    }
}
