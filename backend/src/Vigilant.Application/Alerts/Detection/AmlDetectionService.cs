using Vigilant.Application.Common.Alerts;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Alerts.Detection;

public sealed class AmlDetectionService(
    INeo4jRepository neo4jRepository,
    IAlertPublisher alertPublisher) : IAmlDetectionService
{
    public async Task<IReadOnlyCollection<AmlAlertDto>> EvaluateAndPublishAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken)
    {
        var alerts = await neo4jRepository.RunAllRulesAsync(cancellationToken);

        if (alerts.Count > 0)
        {
            await alertPublisher.PublishAsync(alerts, cancellationToken);
        }

        return alerts;
    }
}