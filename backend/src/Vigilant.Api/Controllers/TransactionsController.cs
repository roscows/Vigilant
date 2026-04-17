using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using Vigilant.Api.Contracts.Transactions;
using Vigilant.Api.Hubs;
using Vigilant.Application.Transactions.ProcessTransaction;
using Vigilant.Application.Transactions.SeedTransactions;

namespace Vigilant.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public sealed class TransactionsController(
    ISender sender,
    ITransactionSeedService transactionSeedService,
    IHubContext<AlertsHub> alertsHub) : ControllerBase
{
    [HttpPost("process")]
    [SwaggerOperation(
        Summary = "Ingests a transaction into the AML graph",
        Description = "Creates the Account, Transaction, IpAddress, and Device graph block and links optional Client ownership context.")]
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
            ReceiverClient: request.ToReceiverClientSnapshot());

        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("seed")]
    [SwaggerOperation(
        Summary = "Seeds a realistic AML demo graph",
        Description = "Generates clients, accounts, transactions, devices, IP addresses, and deliberate circular-flow transactions, then broadcasts detected alerts over SignalR.")]
    [ProducesResponseType(typeof(SeedTransactionsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeedTransactionsResult>> Seed(
        [FromBody] SeedTransactionsApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await transactionSeedService.SeedAsync(request.ToApplicationRequest(), cancellationToken);

        if (result.TriggeredAlerts.Count > 0)
        {
            await alertsHub.Clients.Group(AlertsHub.AllAlertsGroup)
                .SendAsync("alerts.detected", result.TriggeredAlerts, cancellationToken);
        }

        return Ok(result);
    }
}
