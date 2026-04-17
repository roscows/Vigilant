using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Transactions.SeedTransactions;

public sealed record SeedTransactionsRequest(
    int ClientCount = 18,
    int AccountCount = 30,
    int RandomTransactionCount = 60,
    int CircularFlowCount = 2);

public sealed record SeedTransactionsResult(
    int ClientsCreated,
    int AccountsCreated,
    int TransactionsCreated,
    int CircularFlowsCreated,
    string FocusAccountIban,
    IReadOnlyCollection<string> CircularAccountIbans,
    IReadOnlyCollection<string> TransactionIds,
    IReadOnlyCollection<AmlAlertDto> TriggeredAlerts);

public interface ITransactionSeedService
{
    Task<SeedTransactionsResult> SeedAsync(
        SeedTransactionsRequest request,
        CancellationToken cancellationToken);
}
