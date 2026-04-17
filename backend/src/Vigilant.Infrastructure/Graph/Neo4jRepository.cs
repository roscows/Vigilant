using System.Globalization;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;
using Vigilant.Infrastructure.Options;

namespace Vigilant.Infrastructure.Graph;

public sealed class Neo4jRepository(
    IDriver driver,
    IOptions<Neo4jOptions> options) : INeo4jRepository
{
    private readonly Neo4jOptions _options = options.Value;

    public async Task<TransactionGraphWriteResult> WriteTransactionGraphAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken)
    {
        const string cypher = """
            MERGE (sender:Account { IBAN: $senderIban })
              ON CREATE SET sender.Id = randomUUID(), sender.Balance = coalesce($senderBalance, 0.0)
            MERGE (receiver:Account { IBAN: $receiverIban })
              ON CREATE SET receiver.Id = randomUUID(), receiver.Balance = coalesce($receiverBalance, 0.0)
            CREATE (tx:Transaction {
                Id: $transactionId,
                Amount: $amount,
                Timestamp: datetime($timestampUtc),
                Currency: $currency
            })
            MERGE (ip:IpAddress { Address: $ipAddress })
              ON CREATE SET ip.CountryCode = $ipCountryCode
              ON MATCH SET ip.CountryCode = coalesce($ipCountryCode, ip.CountryCode)
            MERGE (device:Device { DeviceId: $deviceId })
              ON CREATE SET device.BrowserFingerprint = $browserFingerprint
              ON MATCH SET device.BrowserFingerprint = coalesce($browserFingerprint, device.BrowserFingerprint)
            MERGE (sender)-[:SENT]->(tx)
            MERGE (tx)-[:RECEIVED_BY]->(receiver)
            MERGE (tx)-[:EXECUTED_FROM_IP]->(ip)
            MERGE (tx)-[:EXECUTED_ON_DEVICE]->(device)
            WITH sender, receiver, tx
            FOREACH (_ IN CASE WHEN $senderClientId IS NULL THEN [] ELSE [1] END |
                MERGE (senderClient:Client { Id: $senderClientId })
                  ON CREATE SET senderClient.Name = $senderClientName, senderClient.RiskScore = $senderClientRiskScore
                  ON MATCH SET senderClient.Name = coalesce($senderClientName, senderClient.Name),
                               senderClient.RiskScore = coalesce($senderClientRiskScore, senderClient.RiskScore)
                MERGE (senderClient)-[:OWNS]->(sender)
            )
            FOREACH (_ IN CASE WHEN $receiverClientId IS NULL THEN [] ELSE [1] END |
                MERGE (receiverClient:Client { Id: $receiverClientId })
                  ON CREATE SET receiverClient.Name = $receiverClientName, receiverClient.RiskScore = $receiverClientRiskScore
                  ON MATCH SET receiverClient.Name = coalesce($receiverClientName, receiverClient.Name),
                               receiverClient.RiskScore = coalesce($receiverClientRiskScore, receiverClient.RiskScore)
                MERGE (receiverClient)-[:OWNS]->(receiver)
            )
            RETURN tx.Id AS transactionId, sender.Id AS senderAccountId, receiver.Id AS receiverAccountId
            """;

        var parameters = new Dictionary<string, object?>
        {
            ["transactionId"] = transaction.TransactionId,
            ["senderIban"] = transaction.SenderIban,
            ["receiverIban"] = transaction.ReceiverIban,
            ["amount"] = decimal.ToDouble(transaction.Amount),
            ["senderBalance"] = transaction.SenderBalance is null ? null : decimal.ToDouble(transaction.SenderBalance.Value),
            ["receiverBalance"] = transaction.ReceiverBalance is null ? null : decimal.ToDouble(transaction.ReceiverBalance.Value),
            ["currency"] = transaction.Currency,
            ["timestampUtc"] = transaction.TimestampUtc.UtcDateTime.ToString("O"),
            ["deviceId"] = transaction.DeviceId,
            ["ipAddress"] = transaction.IpAddress,
            ["ipCountryCode"] = transaction.IpCountryCode,
            ["browserFingerprint"] = transaction.BrowserFingerprint,
            ["senderClientId"] = transaction.SenderClient?.Id,
            ["senderClientName"] = transaction.SenderClient?.Name,
            ["senderClientRiskScore"] = transaction.SenderClient is null ? null : decimal.ToDouble(transaction.SenderClient.RiskScore),
            ["receiverClientId"] = transaction.ReceiverClient?.Id,
            ["receiverClientName"] = transaction.ReceiverClient?.Name,
            ["receiverClientRiskScore"] = transaction.ReceiverClient is null ? null : decimal.ToDouble(transaction.ReceiverClient.RiskScore)
        };

        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));

        try
        {
            return await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var record = await cursor.SingleAsync();

                return new TransactionGraphWriteResult(
                    record["transactionId"].As<string>(),
                    record["senderAccountId"].As<string>(),
                    record["receiverAccountId"].As<string>(),
                    DateTimeOffset.UtcNow);
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyCollection<AmlAlertDto>> FindCircularFlowsAsync(
        int maxTransfers,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken)
    {
        var cypher = BuildCircularFlowDetectionQuery(maxTransfers);
        var lookbackFromUtc = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O");

        var parameters = new Dictionary<string, object?>
        {
            ["lookbackFromUtc"] = lookbackFromUtc,
            ["limit"] = limit,
            ["highAmount"] = 10_000.0,
            ["criticalAmount"] = 50_000.0
        };

        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));

        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var alerts = new List<AmlAlertDto>();

                while (await cursor.FetchAsync())
                {
                    var record = cursor.Current;
                    var transactionIds = record["transactionIds"].As<List<object>>()
                        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)!)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var accountIbans = record["accountIbans"].As<List<object>>()
                        .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)!)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var totalAmount = Convert.ToDecimal(record["totalAmount"].As<double>());
                    var accountIban = record["accountIban"].As<string>();
                    var transferCount = Convert.ToInt32(record["transferCount"].As<long>());

                    alerts.Add(new AmlAlertDto(
                        Id: $"alert-{Guid.NewGuid():N}",
                        Type: AmlAlertTypes.CircularFlow,
                        Severity: record["severity"].As<string>(),
                        Message: $"Circular money flow detected for {accountIban} across {transferCount} transfers.",
                        AccountIban: accountIban,
                        TotalAmount: totalAmount,
                        TransactionIds: transactionIds,
                        AccountIbans: accountIbans,
                        DetectedAtUtc: DateTimeOffset.UtcNow));
                }

                return alerts;
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<EntityGraphDto> GetEntityGraphAsync(
        string accountIban,
        int depth,
        CancellationToken cancellationToken)
    {
        var safeDepth = Math.Clamp(depth, 1, 8);
        var cypher = $$"""
            MATCH (start:Account { IBAN: $accountIban })
            MATCH path = (start)-[:OWNS|SENT|RECEIVED_BY|EXECUTED_FROM_IP|EXECUTED_ON_DEVICE*0..{{safeDepth}}]-()
            WITH collect(path) AS paths
            UNWIND paths AS path
            UNWIND nodes(path) AS node
            WITH paths, collect(DISTINCT node) AS graphNodes
            UNWIND paths AS path
            UNWIND relationships(path) AS relationship
            RETURN graphNodes AS nodes, collect(DISTINCT relationship) AS relationships
            """;

        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));

        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { accountIban });

                if (!await cursor.FetchAsync())
                {
                    return new EntityGraphDto(Array.Empty<GraphNodeDto>(), Array.Empty<GraphEdgeDto>());
                }

                var record = cursor.Current;
                var nodes = record["nodes"].As<List<INode>>()
                    .Select(MapNode)
                    .ToArray();

                var relationships = record["relationships"].As<List<IRelationship>>()
                    .Select(MapRelationship)
                    .ToArray();

                return new EntityGraphDto(nodes, relationships);
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public string BuildCircularFlowDetectionQuery(int maxTransfers)
    {
        var safeMaxTransfers = Math.Clamp(maxTransfers, 2, 10);
        var maxRelationshipJumps = safeMaxTransfers * 2;

        return $"""
            MATCH path = (origin:Account)-[:SENT|RECEIVED_BY*4..{maxRelationshipJumps}]->(origin)
            WHERE all(index IN range(0, size(relationships(path)) - 1) WHERE
                (index % 2 = 0 AND type(relationships(path)[index]) = 'SENT') OR
                (index % 2 = 1 AND type(relationships(path)[index]) = 'RECEIVED_BY'))
            WITH origin,
                 path,
                 [node IN nodes(path) WHERE node:Transaction] AS transactions,
                 [node IN nodes(path) WHERE node:Account] AS accounts
            WHERE any(tx IN transactions WHERE tx.Timestamp >= datetime($lookbackFromUtc))
            WITH origin,
                 transactions,
                 accounts,
                 reduce(total = 0.0, tx IN transactions | total + coalesce(tx.Amount, 0.0)) AS totalAmount
            RETURN origin.IBAN AS accountIban,
                   [tx IN transactions | tx.Id] AS transactionIds,
                   [account IN accounts | account.IBAN] AS accountIbans,
                   size(transactions) AS transferCount,
                   totalAmount AS totalAmount,
                   CASE
                     WHEN totalAmount >= $criticalAmount THEN 'Critical'
                     WHEN totalAmount >= $highAmount THEN 'High'
                     ELSE 'Medium'
                   END AS severity
            ORDER BY totalAmount DESC, transferCount DESC
            LIMIT $limit
            """;
    }

    private static GraphNodeDto MapNode(INode node)
    {
        var labels = node.Labels.ToArray();
        var label = labels.FirstOrDefault() ?? "Entity";

        return new GraphNodeDto(
            node.ElementId,
            label,
            labels,
            NormalizeProperties(node.Properties));
    }

    private static GraphEdgeDto MapRelationship(IRelationship relationship)
    {
        return new GraphEdgeDto(
            relationship.ElementId,
            relationship.StartNodeElementId,
            relationship.EndNodeElementId,
            relationship.Type,
            NormalizeProperties(relationship.Properties));
    }

    private static Dictionary<string, object?> NormalizeProperties(
        IReadOnlyDictionary<string, object> properties)
    {
        return properties.ToDictionary(
            pair => pair.Key,
            pair => NormalizeValue(pair.Value));
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string or bool or int or long or double or float or decimal or DateTime or DateTimeOffset)
        {
            return value;
        }

        if (value is IEnumerable<object> values)
        {
            return values.Select(NormalizeValue).ToArray();
        }

        return value.ToString();
    }
}



