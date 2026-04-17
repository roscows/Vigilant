using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Alerts.GetAmlAlerts;

public sealed record GetAmlAlertsQuery(
    int MaxTransfers = 8,
    int LookbackHours = 168,
    int Limit = 100) : IRequest<IReadOnlyCollection<AmlAlertDto>>;

public sealed class GetAmlAlertsQueryHandler(
    INeo4jRepository neo4jRepository) : IRequestHandler<GetAmlAlertsQuery, IReadOnlyCollection<AmlAlertDto>>
{
    private static readonly string[] OffshoreCountryCodes = ["VG", "KY", "PA", "SC", "BZ", "VU"];

    public async Task<IReadOnlyCollection<AmlAlertDto>> Handle(
        GetAmlAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var maxTransfers = Math.Clamp(request.MaxTransfers, 3, 10);
        var lookbackHours = Math.Clamp(request.LookbackHours, 1, 24 * 30);
        var limit = Math.Clamp(request.Limit, 1, 250);
        var lookbackWindow = TimeSpan.FromHours(lookbackHours);

        var alertSets = await Task.WhenAll(
            neo4jRepository.FindCircularFlowsAsync(maxTransfers, lookbackWindow, limit, cancellationToken),
            neo4jRepository.FindSmurfingAsync(TimeSpan.FromHours(24), 5, 10_000m, 40_000m, 80_000m, limit, cancellationToken),
            neo4jRepository.FindRapidFanOutAsync(TimeSpan.FromHours(1), 4, limit, cancellationToken),
            neo4jRepository.FindSharedDeviceOrIpAsync(2, 60m, limit, cancellationToken),
            neo4jRepository.FindRoundTripsAsync(TimeSpan.FromDays(7), 3, maxTransfers, 4, limit, cancellationToken),
            neo4jRepository.FindPepOffshoreAsync(TimeSpan.FromDays(7), OffshoreCountryCodes, 50_000m, limit, cancellationToken));

        return alertSets
            .SelectMany(alert => alert)
            .GroupBy(alert => alert.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(alert => SeverityRank(alert.Severity))
            .ThenByDescending(alert => alert.TotalAmount)
            .Take(limit)
            .ToArray();
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
