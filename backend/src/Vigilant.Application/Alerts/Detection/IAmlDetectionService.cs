using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Alerts.Detection;

public interface IAmlDetectionService
{
    Task<IReadOnlyCollection<AmlAlertDto>> EvaluateAndPublishAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken);
}
