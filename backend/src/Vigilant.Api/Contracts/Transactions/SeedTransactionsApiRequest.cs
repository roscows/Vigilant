using Vigilant.Application.Transactions.SeedTransactions;

namespace Vigilant.Api.Contracts.Transactions;

public sealed record SeedTransactionsApiRequest(
    int ClientCount = 18,
    int AccountCount = 30,
    int RandomTransactionCount = 60,
    int CircularFlowCount = 2)
{
    public SeedTransactionsRequest ToApplicationRequest()
    {
        return new SeedTransactionsRequest(
            ClientCount,
            AccountCount,
            RandomTransactionCount,
            CircularFlowCount);
    }
}
