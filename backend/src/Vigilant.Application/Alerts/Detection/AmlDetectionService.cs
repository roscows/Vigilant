using Vigilant.Application.Common.Alerts;
using Vigilant.Application.Common.Graph;
using Vigilant.Application.Clients.Risk;

namespace Vigilant.Application.Alerts.Detection;

public sealed class AmlDetectionService(
    INeo4jRepository neo4jRepository,
    IAlertPublisher alertPublisher,
    IRiskScoreService riskScoreService) : IAmlDetectionService
{
    private static readonly string[] OffshoreCountryCodes = ["VG", "KY", "PA", "SC", "BZ", "VU"];

    public async Task<IReadOnlyCollection<AmlAlertDto>> EvaluateAndPublishAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken)
    {
        var alertSets = await Task.WhenAll(
            neo4jRepository.FindCircularFlowsAsync(4, TimeSpan.FromHours(24), 25, cancellationToken),
            neo4jRepository.FindSmurfingAsync(TimeSpan.FromHours(24), 5, 10_000m, 40_000m, 80_000m, 25, cancellationToken),
            neo4jRepository.FindRapidFanOutAsync(TimeSpan.FromHours(1), 4, 25, cancellationToken),
            neo4jRepository.FindSharedDeviceOrIpAsync(2, 60m, 25, cancellationToken),
            neo4jRepository.FindRoundTripsAsync(TimeSpan.FromDays(7), 3, 8, 4, 25, cancellationToken),
            neo4jRepository.FindPepOffshoreAsync(TimeSpan.FromDays(7), OffshoreCountryCodes, 50_000m, 25, cancellationToken));

        var alerts = alertSets
            .SelectMany(alert => alert)
            .GroupBy(alert => alert.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(alert => SeverityRank(alert.Severity))
            .ThenByDescending(alert => alert.TotalAmount)
            .ToArray();

        if (alerts.Length > 0)
        {
            await alertPublisher.PublishAsync(alerts, cancellationToken);
        }

        foreach (var clientId in GetTouchedClientIds(transaction))
        {
            await riskScoreService.RecomputeAsync(clientId, cancellationToken);
        }

        return alerts;
    }

    private static IEnumerable<string> GetTouchedClientIds(TransactionGraphWriteModel transaction)
    {
        if (!string.IsNullOrWhiteSpace(transaction.SenderClient?.Id))
        {
            yield return transaction.SenderClient.Id;
        }

        if (!string.IsNullOrWhiteSpace(transaction.ReceiverClient?.Id) &&
            !string.Equals(transaction.SenderClient?.Id, transaction.ReceiverClient.Id, StringComparison.OrdinalIgnoreCase))
        {
            yield return transaction.ReceiverClient.Id;
        }
    }

    private static int SeverityRank(string severity)
    {
        return severity switch
        {
            "Critical" => 3,
            "High" => 2,
            "Medium" => 1,
            _ => 0
        };
    }
}
