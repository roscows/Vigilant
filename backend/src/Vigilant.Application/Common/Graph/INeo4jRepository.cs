using Vigilant.Application.Clients.Risk;
using Vigilant.Domain.Alerts;

namespace Vigilant.Application.Common.Graph;

public interface INeo4jRepository
{
    Task<TransactionGraphWriteResult> WriteTransactionGraphAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindCircularFlowsAsync(
        int maxTransfers,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindSmurfingAsync(
        TimeSpan lookbackWindow,
        int minTransactionCount,
        decimal maxSingleTransactionAmount,
        decimal minTotalAmount,
        decimal highSeverityAmount,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindRapidFanOutAsync(
        TimeSpan lookbackWindow,
        int minDistinctDestinations,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindVelocityChainsAsync(
        int minChainLength,
        TimeSpan chainWindow,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindFanOutAnomaliesAsync(
        TimeSpan lookbackWindow,
        int minDistinctDestinations,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindSharedDeviceOrIpAsync(
        int minSharedClients,
        decimal highRiskClientScore,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindRoundTripsAsync(
        TimeSpan lookbackWindow,
        int minTransfers,
        int maxTransfers,
        int mediumSeverityMaxTransfers,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> FindPepOffshoreAsync(
        TimeSpan lookbackWindow,
        IReadOnlyCollection<string> offshoreCountryCodes,
        decimal minTransactionAmount,
        int limit,
        CancellationToken cancellationToken);

    Task<EntityGraphDto> GetEntityGraphAsync(
        string accountIban,
        int depth,
        CancellationToken cancellationToken);

    Task<EntityGraphDto> GetGraphOverviewAsync(
        int nodeLimit,
        CancellationToken cancellationToken);

    Task<EntityGraphDto> GetGraphAsync(
        string? ibanFocus,
        int depth,
        DateTimeOffset? from,
        DateTimeOffset? toDate,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AmlAlertDto>> RunAllRulesAsync(CancellationToken cancellationToken);

    Task UpdateClientRiskScoreAsync(
        string accountId,
        int delta,
        CancellationToken cancellationToken);

    Task SaveAlertAsync(
        AlertNode alert,
        CancellationToken cancellationToken);

    Task<List<AlertNode>> GetAlertsAsync(
        AlertStatus? statusFilter,
        DateTime? from,
        DateTime? toDate,
        CancellationToken cancellationToken);

    Task<AlertNode?> GetAlertByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task UpdateAlertStatusAsync(
        Guid id,
        AlertStatus newStatus,
        string analystName,
        string comment,
        CancellationToken cancellationToken);

    Task<ClientRiskScoreDto> RecomputeClientRiskScoreAsync(
        string clientId,
        CancellationToken cancellationToken);

    Task<ClientRiskScoreDto?> GetClientRiskScoreAsync(
        string clientId,
        CancellationToken cancellationToken);

    string BuildCircularFlowDetectionQuery(int maxTransfers);
    string BuildSmurfingDetectionQuery();
    string BuildRapidFanOutDetectionQuery();
    string BuildVelocityChainDetectionQuery(int minChainLength, int maxChainLength);
    string BuildFanOutAnomalyDetectionQuery();
    string BuildSharedDeviceOrIpDetectionQuery();
    string BuildRoundTripDetectionQuery(int minTransfers, int maxTransfers);
    string BuildPepOffshoreDetectionQuery();
}