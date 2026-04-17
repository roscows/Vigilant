using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Api.Contracts.Transactions;
using Vigilant.Application.Transactions.ProcessTransaction;
using Vigilant.Application.Transactions.SeedTransactions;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public sealed class TransactionsController(
    ISender sender,
    ITransactionSeedService transactionSeedService) : ControllerBase
{
    [HttpPost("process")]
    [SwaggerOperation(
        Summary = "Ingests a transaction into the AML graph",
        Description = "Creates the graph block, evaluates AML rules, recomputes client risk, and broadcasts alerts.")]
    [ProducesResponseType(typeof(TransactionProcessorResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionProcessorResult>> Process(
        [FromBody] ProcessTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new TransactionProcessorCommand(
            SenderIban: request.SenderIban,
            ReceiverIban: request.ReceiverIban,
            Amount: request.Amount,
            Currency: request.Currency,
            DeviceId: request.DeviceId,
            IpAddress: request.IpAddress,
            IpCountryCode: request.IpCountryCode,
            BrowserFingerprint: request.BrowserFingerprint,
            SenderClient: request.ToSenderClientSnapshot(),
            ReceiverClient: request.ToReceiverClientSnapshot(),
            SenderAccountCountryCode: request.SenderAccountCountryCode,
            ReceiverAccountCountryCode: request.ReceiverAccountCountryCode);

        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("seed")]
    [SwaggerOperation(
        Summary = "Seeds a realistic AML demo graph",
        Description = "Generates clients, accounts, transactions, devices, IP addresses, and deliberate AML typology examples.")]
    [ProducesResponseType(typeof(SeedTransactionsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeedTransactionsResult>> Seed(
        [FromBody] SeedTransactionsApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await transactionSeedService.SeedAsync(request.ToApplicationRequest(), cancellationToken);
        return Ok(result);
    }
}
