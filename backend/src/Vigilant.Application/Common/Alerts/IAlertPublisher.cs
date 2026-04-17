using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;

namespace Vigilant.Application.Common.Alerts;

public interface IAlertPublisher
{
    Task PublishAsync(IReadOnlyCollection<AmlAlertDto> alerts, CancellationToken cancellationToken);

    Task PublishUpdatedAsync(AlertNode alert, CancellationToken cancellationToken);
}