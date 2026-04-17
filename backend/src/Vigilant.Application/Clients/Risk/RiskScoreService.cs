using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Clients.Risk;

public sealed class RiskScoreService(INeo4jRepository neo4jRepository) : IRiskScoreService
{
    public Task<ClientRiskScoreDto> RecomputeAsync(string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client id is required.", nameof(clientId));
        }

        return neo4jRepository.RecomputeClientRiskScoreAsync(clientId.Trim(), cancellationToken);
    }

    public Task<ClientRiskScoreDto?> GetAsync(string clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("Client id is required.", nameof(clientId));
        }

        return neo4jRepository.GetClientRiskScoreAsync(clientId.Trim(), cancellationToken);
    }
}
