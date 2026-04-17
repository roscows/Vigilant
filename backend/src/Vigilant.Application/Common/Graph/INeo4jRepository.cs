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

    Task<EntityGraphDto> GetEntityGraphAsync(
        string accountIban,
        int depth,
        CancellationToken cancellationToken);

    string BuildCircularFlowDetectionQuery(int maxTransfers);
}
