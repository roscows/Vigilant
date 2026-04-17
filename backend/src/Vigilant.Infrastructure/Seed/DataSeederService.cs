using Bogus;
using Vigilant.Application.Alerts.Detection;
using Vigilant.Application.Common.Graph;
using Vigilant.Application.Transactions.SeedTransactions;

namespace Vigilant.Infrastructure.Seed;

public sealed class DataSeederService(
    INeo4jRepository neo4jRepository,
    IAmlDetectionService amlDetectionService) : ITransactionSeedService
{
    private static readonly string[] OffshoreCountryCodes = ["VG", "KY", "PA", "SC", "BZ", "VU"];
    private const string Currency = "EUR";
    private readonly Faker _faker = new("en");

    public async Task<SeedTransactionsResult> SeedAsync(
        SeedTransactionsRequest request,
        CancellationToken cancellationToken)
    {
        var clientCount = Math.Clamp(request.ClientCount, 12, 80);
        var accountCount = Math.Clamp(request.AccountCount, 30, 160);
        var randomTransactionCount = Math.Clamp(request.RandomTransactionCount, 12, 600);
        var circularFlowCount = Math.Clamp(request.CircularFlowCount, 1, 12);

        var clients = Enumerable.Range(1, clientCount).Select(CreateClient).ToArray();
        var accounts = Enumerable.Range(1, accountCount)
            .Select(index => CreateAccount(index, clients[(index - 1) % clients.Length]))
            .ToArray();

        var transactionIds = new List<string>(randomTransactionCount + circularFlowCount * 3 + 60);
        var circularAccountIbans = new List<string>(circularFlowCount * 3);
        var baseTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-15);

        for (var index = 0; index < randomTransactionCount; index++)
        {
            var sender = _faker.PickRandom(accounts);
            var receiver = _faker.PickRandom(accounts.Where(account => account.Iban != sender.Iban).ToArray());
            var amount = _faker.Finance.Amount(150, 18_500, 2);
            var timestampUtc = DateTimeOffset.UtcNow.AddMinutes(-_faker.Random.Int(20, 1_800));
            var result = await WriteSeedTransactionAsync(sender, receiver, amount, timestampUtc, "seed-txn", false, cancellationToken);
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
                var result = await WriteSeedTransactionAsync(
                    transaction.Sender,
                    transaction.Receiver,
                    transaction.Amount,
                    flowTimeUtc.AddSeconds(step * 47),
                    "seed-cycle",
                    true,
                    cancellationToken);

                transactionIds.Add(result.TransactionId);
            }
        }

        await AddSmurfingScenarioAsync(accounts, transactionIds, cancellationToken);
        await AddVelocityChainScenariosAsync(accounts, transactionIds, cancellationToken);
        await AddFanOutScenarioAsync(accounts, transactionIds, cancellationToken);
        await AddFalsePositiveTransactionsAsync(accounts, transactionIds, cancellationToken);
        await AddPepOffshoreScenarioAsync(accounts, transactionIds, cancellationToken);

        var alerts = await amlDetectionService.EvaluateAndPublishAsync(
            CreateTransactionWriteModel(accounts[0], accounts[1], 1m, DateTimeOffset.UtcNow, "seed-evaluation", false),
            cancellationToken);
        var alertsBySeverity = alerts
            .GroupBy(alert => alert.Severity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var distinctCircularIbans = circularAccountIbans.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return new SeedTransactionsResult(
            ClientsCreated: clients.Length,
            AccountsCreated: accounts.Length,
            TransactionsCreated: transactionIds.Count,
            AlertsDetected: alerts.Count,
            AlertsBySeverity: alertsBySeverity,
            CircularFlowsCreated: circularFlowCount,
            FocusAccountIban: distinctCircularIbans[0],
            CircularAccountIbans: distinctCircularIbans,
            TransactionIds: transactionIds,
            TriggeredAlerts: alerts);
    }

    private async Task AddSmurfingScenarioAsync(
        IReadOnlyList<SeedAccount> accounts,
        List<string> transactionIds,
        CancellationToken cancellationToken)
    {
        for (var pairIndex = 0; pairIndex < 3; pairIndex++)
        {
            var sender = accounts[2 + pairIndex];
            var receiver = accounts[12 + pairIndex];
            var transactionCount = _faker.Random.Int(4, 6);

            for (var index = 0; index < transactionCount; index++)
            {
                var result = await WriteSeedTransactionAsync(
                    sender,
                    receiver,
                    _faker.Finance.Amount(3_100, 9_400, 2),
                    DateTimeOffset.UtcNow.AddMinutes(-90 + pairIndex * 8 + index * 4),
                    "seed-smurfing",
                    false,
                    cancellationToken);
                transactionIds.Add(result.TransactionId);
            }
        }
    }

    private async Task AddVelocityChainScenariosAsync(
        IReadOnlyList<SeedAccount> accounts,
        List<string> transactionIds,
        CancellationToken cancellationToken)
    {
        for (var chainIndex = 0; chainIndex < 2; chainIndex++)
        {
            var offset = 6 + chainIndex * 5;
            var chainAccounts = accounts.Skip(offset).Take(5).ToArray();
            var startTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-25 + chainIndex * 3);

            for (var hop = 0; hop < chainAccounts.Length - 1; hop++)
            {
                var result = await WriteSeedTransactionAsync(
                    chainAccounts[hop],
                    chainAccounts[hop + 1],
                    _faker.Finance.Amount(8_000, 22_000, 2),
                    startTimeUtc.AddMinutes(hop * 7),
                    "seed-velocity",
                    false,
                    cancellationToken);
                transactionIds.Add(result.TransactionId);
            }
        }
    }

    private async Task AddFanOutScenarioAsync(
        IReadOnlyList<SeedAccount> accounts,
        List<string> transactionIds,
        CancellationToken cancellationToken)
    {
        var sender = accounts[4];
        for (var index = 0; index < 12; index++)
        {
            var receiver = accounts[18 + index];
            var result = await WriteSeedTransactionAsync(
                sender,
                receiver,
                _faker.Finance.Amount(1_200, 7_500, 2),
                DateTimeOffset.UtcNow.AddMinutes(-55 + index * 3),
                "seed-fanout",
                false,
                cancellationToken);
            transactionIds.Add(result.TransactionId);
        }
    }

    private async Task AddFalsePositiveTransactionsAsync(
        IReadOnlyList<SeedAccount> accounts,
        List<string> transactionIds,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < 10; index++)
        {
            var sender = accounts[(index + 8) % accounts.Count];
            var receiver = accounts[(index + 19) % accounts.Count];
            if (sender.Iban == receiver.Iban)
            {
                receiver = accounts[(index + 20) % accounts.Count];
            }

            var result = await WriteSeedTransactionAsync(
                sender,
                receiver,
                _faker.Finance.Amount(9_050, 9_850, 2),
                DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(index * 11),
                "seed-false-positive",
                false,
                cancellationToken);
            transactionIds.Add(result.TransactionId);
        }
    }

    private async Task AddPepOffshoreScenarioAsync(
        IReadOnlyList<SeedAccount> accounts,
        List<string> transactionIds,
        CancellationToken cancellationToken)
    {
        var result = await WriteSeedTransactionAsync(
            accounts[0],
            accounts[1],
            72_500m,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            "seed-pep-offshore",
            false,
            cancellationToken);
        transactionIds.Add(result.TransactionId);
    }

    private async Task<TransactionGraphWriteResult> WriteSeedTransactionAsync(
        SeedAccount sender,
        SeedAccount receiver,
        decimal amount,
        DateTimeOffset timestampUtc,
        string prefix,
        bool isCircularFlow,
        CancellationToken cancellationToken)
    {
        var writeModel = CreateTransactionWriteModel(sender, receiver, amount, timestampUtc, prefix, isCircularFlow);
        return await neo4jRepository.WriteTransactionGraphAsync(writeModel, cancellationToken);
    }

    private ClientGraphSnapshot CreateClient(int index)
    {
        var name = _faker.Name.FullName();
        var isPep = index == 1 || _faker.Random.Bool(0.12f);
        var riskScore = index is 1 or 2 or 3
            ? _faker.Random.Decimal(65, 92)
            : _faker.Random.Decimal(8, 74);

        return new ClientGraphSnapshot($"seed-client-{index:000}-{_faker.Random.AlphaNumeric(5)}", name, Math.Round(riskScore, 2), isPep);
    }

    private SeedAccount CreateAccount(int index, ClientGraphSnapshot owner)
    {
        var iban = $"RS{_faker.Random.ReplaceNumbers("####################")}";
        var balance = _faker.Finance.Amount(1_000, 180_000, 2);
        var countryCode = index == 1
            ? "KY"
            : _faker.Random.Bool(0.16f)
                ? _faker.PickRandom(OffshoreCountryCodes)
                : _faker.Address.CountryCode().ToUpperInvariant();

        return new SeedAccount(iban, balance, countryCode, owner);
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
        string transactionPrefix,
        bool isCircularFlow)
    {
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
            IpCountryCode: isCircularFlow ? "CY" : _faker.Address.CountryCode().ToUpperInvariant(),
            BrowserFingerprint: isCircularFlow ? $"layering-fingerprint-{_faker.Random.Int(1, 3):00}" : _faker.Random.Hash(24),
            SenderClient: sender.Owner,
            ReceiverClient: receiver.Owner,
            SenderBalance: sender.Balance,
            ReceiverBalance: receiver.Balance,
            SenderAccountCountryCode: sender.CountryCode,
            ReceiverAccountCountryCode: receiver.CountryCode);
    }

    private sealed record SeedAccount(
        string Iban,
        decimal Balance,
        string CountryCode,
        ClientGraphSnapshot Owner);
}

