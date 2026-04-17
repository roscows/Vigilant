using MediatR;
using Vigilant.Application.Alerts.Detection;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Transactions.ProcessTransaction;

public sealed class TransactionProcessorCommandHandler(
    INeo4jRepository neo4jRepository,
    IAmlDetectionService amlDetectionService) : IRequestHandler<TransactionProcessorCommand, TransactionProcessorResult>
{
    public async Task<TransactionProcessorResult> Handle(
        TransactionProcessorCommand request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var transactionId = $"txn-{Guid.NewGuid():N}";
        var timestampUtc = DateTimeOffset.UtcNow;

        var graphWrite = new TransactionGraphWriteModel(
            TransactionId: transactionId,
            SenderIban: request.SenderIban.Trim(),
            ReceiverIban: request.ReceiverIban.Trim(),
            Amount: request.Amount,
            Currency: request.Currency.Trim().ToUpperInvariant(),
            TimestampUtc: timestampUtc,
            DeviceId: request.DeviceId.Trim(),
            IpAddress: request.IpAddress.Trim(),
            IpCountryCode: NormalizeCountryCode(request.IpCountryCode),
            BrowserFingerprint: string.IsNullOrWhiteSpace(request.BrowserFingerprint) ? null : request.BrowserFingerprint.Trim(),
            SenderClient: request.SenderClient,
            ReceiverClient: request.ReceiverClient,
            SenderAccountCountryCode: NormalizeCountryCode(request.SenderAccountCountryCode),
            ReceiverAccountCountryCode: NormalizeCountryCode(request.ReceiverAccountCountryCode));

        await neo4jRepository.WriteTransactionGraphAsync(graphWrite, cancellationToken);
        var triggeredAlerts = await amlDetectionService.EvaluateAndPublishAsync(graphWrite, cancellationToken);
        await UpdateRiskScoresAsync(triggeredAlerts, cancellationToken);

        return new TransactionProcessorResult(
            transactionId,
            graphWrite.SenderIban,
            graphWrite.ReceiverIban,
            graphWrite.Amount,
            graphWrite.Currency,
            timestampUtc,
            triggeredAlerts);
    }

    private static string? NormalizeCountryCode(string? countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant();
    }

    private async Task UpdateRiskScoresAsync(
        IReadOnlyCollection<AmlAlertDto> alerts,
        CancellationToken cancellationToken)
    {
        foreach (var alert in alerts)
        {
            var delta = GetRiskDelta(alert.Type);
            if (delta == 0)
            {
                continue;
            }

            foreach (var accountId in alert.AccountIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await neo4jRepository.UpdateClientRiskScoreAsync(accountId, delta, cancellationToken);
            }
        }
    }

    private static int GetRiskDelta(string ruleType)
    {
        return ruleType switch
        {
            "CircularFlow" => 30,
            "Smurfing" => 20,
            "VelocityChain" => 25,
            "FanOut" => 15,
            _ => 0
        };
    }

    private static void Validate(TransactionProcessorCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.SenderIban))
        {
            throw new ArgumentException("Sender IBAN is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ReceiverIban))
        {
            throw new ArgumentException("Receiver IBAN is required.", nameof(request));
        }

        if (string.Equals(request.SenderIban.Trim(), request.ReceiverIban.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Sender and receiver IBANs must be different.", nameof(request));
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Transaction amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a three-letter ISO code.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ArgumentException("Device ID is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.IpAddress))
        {
            throw new ArgumentException("IP address is required.", nameof(request));
        }
    }
}
