using Bogus;
using Vigilant.Application.Common.Graph;
using Vigilant.Application.Transactions.SeedTransactions;

namespace Vigilant.Infrastructure.Seed;

public sealed class DataSeederService(INeo4jRepository neo4jRepository) : ITransactionSeedService
{
    private const string Currency = "EUR";
    private readonly Faker _faker = new("en");

    public async Task<SeedTransactionsResult> SeedAsync(
        SeedTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var clientCount = Math.Clamp(request.ClientCount, 6, 80);
        var accountCount = Math.Clamp(request.AccountCount, 9, 160);
        var randomTransactionCount = Math.Clamp(request.RandomTransactionCount, 12, 600);
        var circularFlowCount = Math.Clamp(request.CircularFlowCount, 1, 12);

        var clients = Enumerable.Range(1, clientCount)
            .Select(CreateClient)
            .ToArray();

        var accounts = Enumerable.Range(1, accountCount)
            .Select(index => CreateAccount(index, clients[(index - 1) % clients.Length]))
            .ToArray();

        var transactionIds = new List<string>(randomTransactionCount + circularFlowCount * 3);
        var circularAccountIbans = new List<string>(circularFlowCount * 3);
        var baseTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-15);

        for (var index = 0; index < randomTransactionCount; index++)
        {
            var sender = _faker.PickRandom(accounts);
            var receiver = _faker.PickRandom(accounts.Where(account => account.Iban != sender.Iban).ToArray());
            var amount = _faker.Finance.Amount(150, 18_500, 2);
            var timestampUtc = DateTimeOffset.UtcNow.AddMinutes(-_faker.Random.Int(20, 1_800));

            var writeModel = CreateTransactionWriteModel(sender, receiver, amount, timestampUtc, isCircularFlow: false);
            var result = await neo4jRepository.WriteTransactionGraphAsync(writeModel, cancellationToken);
            transactionIds.Add(result.TransactionId);
        }

        for (var flowIndex = 0; flowIndex < circularFlowCount; flowIndex++)
        {
            var ring = PickCircularAccounts(accounts);
            circularAccountIbans.AddRange(ring.Select(account => account.Iban));

            var amount = _faker.Finance.Amount(11_000, 32_000, 2);
            var flowTimeUtc = baseTimeUtc.AddMinutes(flowIndex * 3);

            var cycle = new[]
            {
                (Sender: ring[0], Receiver: ring[1], Amount: amount),
                (Sender: ring[1], Receiver: ring[2], Amount: Math.Round(amount * _faker.Random.Decimal(0.88m, 0.98m), 2)),
                (Sender: ring[2], Receiver: ring[0], Amount: Math.Round(amount * _faker.Random.Decimal(0.82m, 0.94m), 2))
            };

            for (var step = 0; step < cycle.Length; step++)
            {
                var transaction = cycle[step];
                var writeModel = CreateTransactionWriteModel(
                    transaction.Sender,
                    transaction.Receiver,
                    transaction.Amount,
                    flowTimeUtc.AddSeconds(step * 47),
                    isCircularFlow: true);

                var result = await neo4jRepository.WriteTransactionGraphAsync(writeModel, cancellationToken);
                transactionIds.Add(result.TransactionId);
            }
        }

        var triggeredAlerts = await neo4jRepository.FindCircularFlowsAsync(
            maxTransfers: 4,
            lookbackWindow: TimeSpan.FromHours(24),
            limit: 25,
            cancellationToken: cancellationToken);

        var distinctCircularIbans = circularAccountIbans
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SeedTransactionsResult(
            ClientsCreated: clients.Length,
            AccountsCreated: accounts.Length,
            TransactionsCreated: transactionIds.Count,
            CircularFlowsCreated: circularFlowCount,
            FocusAccountIban: distinctCircularIbans[0],
            CircularAccountIbans: distinctCircularIbans,
            TransactionIds: transactionIds,
            TriggeredAlerts: triggeredAlerts);
    }

    private ClientGraphSnapshot CreateClient(int index)
    {
        var name = _faker.Name.FullName();
        var riskScore = Math.Round(_faker.Random.Decimal(8, 92), 2);
        return new ClientGraphSnapshot($"seed-client-{index:000}-{_faker.Random.AlphaNumeric(5)}", name, riskScore);
    }

    private SeedAccount CreateAccount(int index, ClientGraphSnapshot owner)
    {
        var iban = $"RS{_faker.Random.ReplaceNumbers("####################")}";
        var balance = _faker.Finance.Amount(1_000, 180_000, 2);
        return new SeedAccount(iban, balance, owner);
    }

    private SeedAccount[] PickCircularAccounts(IReadOnlyCollection<SeedAccount> accounts)
    {
        return _faker.PickRandom(accounts, 3).ToArray();
    }

    private TransactionGraphWriteModel CreateTransactionWriteModel(
        SeedAccount sender,
        SeedAccount receiver,
        decimal amount,
        DateTimeOffset timestampUtc,
        bool isCircularFlow)
    {
        var transactionPrefix = isCircularFlow ? "seed-cycle" : "seed-txn";
        var deviceId = isCircularFlow
            ? $"shared-layering-device-{_faker.Random.Int(1, 4):00}"
            : $"device-{_faker.Random.AlphaNumeric(10).ToLowerInvariant()}";

        return new TransactionGraphWriteModel(
            TransactionId: $"{transactionPrefix}-{Guid.NewGuid():N}",
            SenderIban: sender.Iban,
            ReceiverIban: receiver.Iban,
            Amount: amount,
            Currency: Currency,
            TimestampUtc: timestampUtc,
            DeviceId: deviceId,
            IpAddress: isCircularFlow ? $"198.51.100.{_faker.Random.Int(10, 220)}" : _faker.Internet.Ip(),
            IpCountryCode: isCircularFlow ? "CY" : _faker.Address.CountryCode(),
            BrowserFingerprint: isCircularFlow
                ? $"layering-fingerprint-{_faker.Random.Int(1, 3):00}"
                : _faker.Random.Hash(24),
            SenderClient: sender.Owner,
            ReceiverClient: receiver.Owner,
            SenderBalance: sender.Balance,
            ReceiverBalance: receiver.Balance);
    }

    private sealed record SeedAccount(
        string Iban,
        decimal Balance,
        ClientGraphSnapshot Owner);
}

