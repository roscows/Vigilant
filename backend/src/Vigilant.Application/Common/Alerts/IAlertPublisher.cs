using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Common.Alerts;

public interface IAlertPublisher
{
    Task PublishAsync(IReadOnlyCollection<AmlAlertDto> alerts, CancellationToken cancellationToken);
}
