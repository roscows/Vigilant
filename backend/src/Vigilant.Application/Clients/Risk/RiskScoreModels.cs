namespace Vigilant.Application.Clients.Risk;

public sealed record RiskContributionDto(
    string AlertType,
    string Description,
    int Weight);

public sealed record ClientRiskScoreDto(
    string ClientId,
    decimal RiskScore,
    IReadOnlyCollection<RiskContributionDto> ContributingAlerts);

public interface IRiskScoreService
{
    Task<ClientRiskScoreDto> RecomputeAsync(string clientId, CancellationToken cancellationToken);
    Task<ClientRiskScoreDto?> GetAsync(string clientId, CancellationToken cancellationToken);
}
