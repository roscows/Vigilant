using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Vigilant.Application.Clients.Risk;
using Vigilant.Application.Common.Graph;
using Vigilant.Domain.Alerts;
using Vigilant.Infrastructure.Options;

namespace Vigilant.Infrastructure.Graph;

public sealed class Neo4jRepository(
    IDriver driver,
    IOptions<Neo4jOptions> options) : INeo4jRepository
{
    private static readonly string[] OffshoreCountryCodes = ["VG", "KY", "PA", "SC", "BZ", "VU"];
    private readonly Neo4jOptions _options = options.Value;

    public async Task<TransactionGraphWriteResult> WriteTransactionGraphAsync(
        TransactionGraphWriteModel transaction,
        CancellationToken cancellationToken)
    {
        const string cypher = """
            MERGE (sender:Account { IBAN: $senderIban })
              ON CREATE SET sender.Id = randomUUID(),
                            sender.Balance = coalesce($senderBalance, 0.0),
                            sender.CountryCode = $senderAccountCountryCode
              ON MATCH SET sender.CountryCode = coalesce($senderAccountCountryCode, sender.CountryCode)
            MERGE (receiver:Account { IBAN: $receiverIban })
              ON CREATE SET receiver.Id = randomUUID(),
                            receiver.Balance = coalesce($receiverBalance, 0.0),
                            receiver.CountryCode = $receiverAccountCountryCode
              ON MATCH SET receiver.CountryCode = coalesce($receiverAccountCountryCode, receiver.CountryCode)
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
                  ON CREATE SET senderClient.Name = $senderClientName,
                                senderClient.RiskScore = $senderClientRiskScore,
                                senderClient.IsPep = coalesce($senderClientIsPep, false)
                  ON MATCH SET senderClient.Name = coalesce($senderClientName, senderClient.Name),
                               senderClient.IsPep = coalesce($senderClientIsPep, senderClient.IsPep)
                MERGE (senderClient)-[:OWNS]->(sender)
            )
            FOREACH (_ IN CASE WHEN $receiverClientId IS NULL THEN [] ELSE [1] END |
                MERGE (receiverClient:Client { Id: $receiverClientId })
                  ON CREATE SET receiverClient.Name = $receiverClientName,
                                receiverClient.RiskScore = $receiverClientRiskScore,
                                receiverClient.IsPep = coalesce($receiverClientIsPep, false)
                  ON MATCH SET receiverClient.Name = coalesce($receiverClientName, receiverClient.Name),
                               receiverClient.IsPep = coalesce($receiverClientIsPep, receiverClient.IsPep)
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
            ["senderAccountCountryCode"] = transaction.SenderAccountCountryCode,
            ["receiverAccountCountryCode"] = transaction.ReceiverAccountCountryCode,
            ["currency"] = transaction.Currency,
            ["timestampUtc"] = transaction.TimestampUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["deviceId"] = transaction.DeviceId,
            ["ipAddress"] = transaction.IpAddress,
            ["ipCountryCode"] = transaction.IpCountryCode,
            ["browserFingerprint"] = transaction.BrowserFingerprint,
            ["senderClientId"] = transaction.SenderClient?.Id,
            ["senderClientName"] = transaction.SenderClient?.Name,
            ["senderClientRiskScore"] = transaction.SenderClient is null ? null : decimal.ToDouble(transaction.SenderClient.RiskScore),
            ["senderClientIsPep"] = transaction.SenderClient?.IsPep,
            ["receiverClientId"] = transaction.ReceiverClient?.Id,
            ["receiverClientName"] = transaction.ReceiverClient?.Name,
            ["receiverClientRiskScore"] = transaction.ReceiverClient is null ? null : decimal.ToDouble(transaction.ReceiverClient.RiskScore),
            ["receiverClientIsPep"] = transaction.ReceiverClient?.IsPep
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

    public Task<IReadOnlyCollection<AmlAlertDto>> FindCircularFlowsAsync(
        int maxTransfers,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.CircularFlow,
            BuildCircularFlowDetectionQuery(maxTransfers),
            new Dictionary<string, object?>
            {
                ["lookbackFromUtc"] = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["limit"] = limit,
                ["highAmount"] = 10_000.0,
                ["criticalAmount"] = 50_000.0
            });
    }

    public Task<IReadOnlyCollection<AmlAlertDto>> FindSmurfingAsync(
        TimeSpan lookbackWindow,
        int minTransactionCount,
        decimal maxSingleTransactionAmount,
        decimal minTotalAmount,
        decimal highSeverityAmount,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.Smurfing,
            BuildSmurfingDetectionQuery(),
            new Dictionary<string, object?>
            {
                ["lookbackFromUtc"] = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["minTransactionCount"] = minTransactionCount,
                ["maxSingleTransactionAmount"] = decimal.ToDouble(maxSingleTransactionAmount),
                ["minTotalAmount"] = decimal.ToDouble(minTotalAmount),
                ["highSeverityAmount"] = decimal.ToDouble(highSeverityAmount),
                ["limit"] = limit
            });
    }

    public Task<IReadOnlyCollection<AmlAlertDto>> FindRapidFanOutAsync(
        TimeSpan lookbackWindow,
        int minDistinctDestinations,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.RapidFanOut,
            BuildRapidFanOutDetectionQuery(),
            new Dictionary<string, object?>
            {
                ["lookbackFromUtc"] = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["minDistinctDestinations"] = minDistinctDestinations,
                ["limit"] = limit
            });
    }

    public Task<IReadOnlyCollection<AmlAlertDto>> FindSharedDeviceOrIpAsync(
        int minSharedClients,
        decimal highRiskClientScore,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.SharedDeviceOrIp,
            BuildSharedDeviceOrIpDetectionQuery(),
            new Dictionary<string, object?>
            {
                ["minSharedClients"] = minSharedClients,
                ["highRiskClientScore"] = decimal.ToDouble(highRiskClientScore),
                ["limit"] = limit
            });
    }

    public Task<IReadOnlyCollection<AmlAlertDto>> FindRoundTripsAsync(
        TimeSpan lookbackWindow,
        int minTransfers,
        int maxTransfers,
        int mediumSeverityMaxTransfers,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.RoundTrip,
            BuildRoundTripDetectionQuery(minTransfers, maxTransfers),
            new Dictionary<string, object?>
            {
                ["lookbackFromUtc"] = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["mediumSeverityMaxTransfers"] = mediumSeverityMaxTransfers,
                ["limit"] = limit
            });
    }

    public Task<IReadOnlyCollection<AmlAlertDto>> FindPepOffshoreAsync(
        TimeSpan lookbackWindow,
        IReadOnlyCollection<string> offshoreCountryCodes,
        decimal minTransactionAmount,
        int limit,
        CancellationToken cancellationToken)
    {
        return RunAlertQueryAsync(
            AmlAlertTypes.PepOffshore,
            BuildPepOffshoreDetectionQuery(),
            new Dictionary<string, object?>
            {
                ["lookbackFromUtc"] = DateTimeOffset.UtcNow.Subtract(lookbackWindow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                ["offshoreCountryCodes"] = offshoreCountryCodes.Select(code => code.ToUpperInvariant()).ToArray(),
                ["minTransactionAmount"] = decimal.ToDouble(minTransactionAmount),
                ["limit"] = limit
            });
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
                return new EntityGraphDto(
                    record["nodes"].As<List<INode>>().Select(MapNode).ToArray(),
                    record["relationships"].As<List<IRelationship>>().Select(MapRelationship).ToArray());
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<EntityGraphDto> GetGraphOverviewAsync(
        int nodeLimit,
        CancellationToken cancellationToken)
    {
        var safeNodeLimit = Math.Clamp(nodeLimit, 25, 500);
        const string cypher = """
            MATCH (node)
            WHERE node:Client OR node:Account OR node:Transaction OR node:IpAddress OR node:Device
            WITH node
            ORDER BY
                CASE
                    WHEN node:Client THEN 0
                    WHEN node:Account THEN 1
                    WHEN node:Transaction THEN 2
                    WHEN node:IpAddress THEN 3
                    ELSE 4
                END,
                coalesce(node.Id, node.IBAN, node.Address, node.DeviceId, elementId(node))
            LIMIT $nodeLimit
            WITH collect(node) AS graphNodes
            UNWIND graphNodes AS graphNode
            OPTIONAL MATCH (graphNode)-[relationship]-(relatedNode)
            WHERE relatedNode IN graphNodes
            RETURN graphNodes AS nodes, collect(DISTINCT relationship) AS relationships
            """;

        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new { nodeLimit = safeNodeLimit });
                if (!await cursor.FetchAsync())
                {
                    return new EntityGraphDto(Array.Empty<GraphNodeDto>(), Array.Empty<GraphEdgeDto>());
                }

                var record = cursor.Current;
                return new EntityGraphDto(
                    record["nodes"].As<List<INode>>().Select(MapNode).ToArray(),
                    record["relationships"].As<List<IRelationship>>().Select(MapRelationship).ToArray());
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<ClientRiskScoreDto> RecomputeClientRiskScoreAsync(string clientId, CancellationToken cancellationToken)
    {
        var contributions = await GetRiskContributionsAsync(clientId);
        var score = Math.Min(100, contributions.Sum(contribution => contribution.Weight));

        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "MATCH (client:Client { Id: $clientId }) SET client.RiskScore = $riskScore RETURN client.Id",
                    new { clientId, riskScore = score });
            });
        }
        finally
        {
            await session.CloseAsync();
        }

        return new ClientRiskScoreDto(clientId, score, contributions);
    }

    public async Task<ClientRiskScoreDto?> GetClientRiskScoreAsync(string clientId, CancellationToken cancellationToken)
    {
        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));
        try
        {
            var currentScore = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(
                    "MATCH (client:Client { Id: $clientId }) RETURN coalesce(client.RiskScore, 0.0) AS riskScore",
                    new { clientId });

                return await cursor.FetchAsync()
                    ? Convert.ToDecimal(cursor.Current["riskScore"].As<double>())
                    : (decimal?)null;
            });

            if (currentScore is null)
            {
                return null;
            }

            var contributions = await GetRiskContributionsAsync(clientId);
            return new ClientRiskScoreDto(clientId, currentScore.Value, contributions);
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
            WITH origin, path,
                 [node IN nodes(path) WHERE node:Transaction] AS transactions,
                 [node IN nodes(path) WHERE node:Account] AS accounts
            WHERE any(tx IN transactions WHERE tx.Timestamp >= datetime($lookbackFromUtc))
            OPTIONAL MATCH (client:Client)-[:OWNS]->(origin)
            WITH origin, transactions, accounts, collect(DISTINCT client.Id) AS clientIds,
                 reduce(total = 0.0, tx IN transactions | total + coalesce(tx.Amount, 0.0)) AS totalAmount
            RETURN 'CircularFlow' AS type,
                   CASE WHEN totalAmount >= $criticalAmount THEN 'Critical'
                        WHEN totalAmount >= $highAmount THEN 'High'
                        ELSE 'Medium' END AS severity,
                   'Circular flow detected: ' + toString(size(transactions)) + ' transfers totalling ' + toString(round(totalAmount * 100) / 100) + ' from IBAN ' + origin.IBAN AS message,
                   origin.IBAN AS accountIban,
                   [tx IN transactions | tx.Id] AS transactionIds,
                   [account IN accounts | account.IBAN] AS accountIbans,
                   clientIds AS clientIds,
                   [] AS deviceIds,
                   [] AS ipAddresses,
                   totalAmount AS totalAmount
            ORDER BY totalAmount DESC, size(transactions) DESC
            LIMIT $limit
            """;
    }

    public string BuildSmurfingDetectionQuery()
    {
        return """
            MATCH (origin:Account)-[:SENT]->(tx:Transaction)
            WHERE tx.Timestamp >= datetime($lookbackFromUtc)
              AND coalesce(tx.Amount, 0.0) < $maxSingleTransactionAmount
            WITH origin, collect(tx) AS transactions, count(tx) AS transactionCount, sum(coalesce(tx.Amount, 0.0)) AS totalAmount
            WHERE transactionCount > $minTransactionCount AND totalAmount > $minTotalAmount
            OPTIONAL MATCH (client:Client)-[:OWNS]->(origin)
            WITH origin, transactions, transactionCount, totalAmount, collect(DISTINCT client.Id) AS clientIds
            RETURN 'Smurfing' AS type,
                   CASE WHEN totalAmount > $highSeverityAmount THEN 'High' ELSE 'Medium' END AS severity,
                   'Smurfing detected: ' + toString(transactionCount) + ' transactions totalling ' + toString(round(totalAmount * 100) / 100) + ' in 24 h from IBAN ' + origin.IBAN AS message,
                   origin.IBAN AS accountIban,
                   [tx IN transactions | tx.Id] AS transactionIds,
                   [origin.IBAN] AS accountIbans,
                   clientIds AS clientIds,
                   [] AS deviceIds,
                   [] AS ipAddresses,
                   totalAmount AS totalAmount
            ORDER BY totalAmount DESC
            LIMIT $limit
            """;
    }

    public string BuildRapidFanOutDetectionQuery()
    {
        return """
            MATCH (origin:Account)-[:SENT]->(tx:Transaction)-[:RECEIVED_BY]->(destination:Account)
            WHERE tx.Timestamp >= datetime($lookbackFromUtc)
            WITH origin, collect(tx) AS transactions, collect(DISTINCT destination) AS destinations, sum(coalesce(tx.Amount, 0.0)) AS totalAmount
            WHERE size(destinations) > $minDistinctDestinations
            OPTIONAL MATCH (client:Client)-[:OWNS]->(origin)
            WITH origin, transactions, destinations, totalAmount, collect(DISTINCT client.Id) AS clientIds
            RETURN 'RapidFanOut' AS type,
                   'High' AS severity,
                   'Rapid fan-out detected: IBAN ' + origin.IBAN + ' sent money to ' + toString(size(destinations)) + ' distinct accounts within 1 h.' AS message,
                   origin.IBAN AS accountIban,
                   [tx IN transactions | tx.Id] AS transactionIds,
                   [account IN ([origin] + destinations) | account.IBAN] AS accountIbans,
                   clientIds AS clientIds,
                   [] AS deviceIds,
                   [] AS ipAddresses,
                   totalAmount AS totalAmount
            ORDER BY size(destinations) DESC, totalAmount DESC
            LIMIT $limit
            """;
    }

    public string BuildSharedDeviceOrIpDetectionQuery()
    {
        return """
            MATCH (client:Client)-[:OWNS]->(:Account)-[:SENT]->(:Transaction)-[:EXECUTED_ON_DEVICE]->(device:Device)
            WHERE device.BrowserFingerprint IS NOT NULL
            WITH device, collect(DISTINCT client) AS clients
            WHERE size(clients) >= $minSharedClients AND any(client IN clients WHERE coalesce(client.RiskScore, 0.0) > $highRiskClientScore)
            RETURN 'SharedDeviceOrIp' AS type,
                   CASE WHEN size(clients) >= 3 THEN 'Critical' ELSE 'High' END AS severity,
                   'Shared device/IP detected: ' + toString(size(clients)) + ' clients share browser fingerprint ' + device.BrowserFingerprint AS message,
                   '' AS accountIban,
                   [] AS transactionIds,
                   [] AS accountIbans,
                   [client IN clients | client.Id] AS clientIds,
                   [device.DeviceId, device.BrowserFingerprint] AS deviceIds,
                   [] AS ipAddresses,
                   0.0 AS totalAmount
            UNION
            MATCH (client:Client)-[:OWNS]->(:Account)-[:SENT]->(:Transaction)-[:EXECUTED_FROM_IP]->(ip:IpAddress)
            WITH ip, collect(DISTINCT client) AS clients
            WHERE size(clients) >= $minSharedClients AND any(client IN clients WHERE coalesce(client.RiskScore, 0.0) > $highRiskClientScore)
            RETURN 'SharedDeviceOrIp' AS type,
                   CASE WHEN size(clients) >= 3 THEN 'Critical' ELSE 'High' END AS severity,
                   'Shared device/IP detected: ' + toString(size(clients)) + ' clients share IP address ' + ip.Address AS message,
                   '' AS accountIban,
                   [] AS transactionIds,
                   [] AS accountIbans,
                   [client IN clients | client.Id] AS clientIds,
                   [] AS deviceIds,
                   [ip.Address] AS ipAddresses,
                   0.0 AS totalAmount
            LIMIT $limit
            """;
    }

    public string BuildRoundTripDetectionQuery(int minTransfers, int maxTransfers)
    {
        var minRelationshipJumps = Math.Clamp(minTransfers, 2, 8) * 2;
        var maxRelationshipJumps = Math.Clamp(maxTransfers, minTransfers, 10) * 2;

        return $"""
            MATCH path = (origin:Account)-[:SENT|RECEIVED_BY*{minRelationshipJumps}..{maxRelationshipJumps}]->(origin)
            WHERE all(index IN range(0, size(relationships(path)) - 1) WHERE
                (index % 2 = 0 AND type(relationships(path)[index]) = 'SENT') OR
                (index % 2 = 1 AND type(relationships(path)[index]) = 'RECEIVED_BY'))
            WITH origin, path,
                 [node IN nodes(path) WHERE node:Transaction] AS transactions,
                 [node IN nodes(path) WHERE node:Account] AS accounts
            WHERE all(tx IN transactions WHERE tx.Timestamp >= datetime($lookbackFromUtc))
            WITH origin, transactions, accounts,
                 reduce(total = 0.0, tx IN transactions | total + coalesce(tx.Amount, 0.0)) AS totalAmount
            OPTIONAL MATCH (client:Client)-[:OWNS]->(origin)
            WITH origin, transactions, accounts, totalAmount, collect(DISTINCT client.Id) AS clientIds
            RETURN 'RoundTrip' AS type,
                   CASE WHEN size(transactions) <= $mediumSeverityMaxTransfers THEN 'Medium' ELSE 'High' END AS severity,
                   'Boomerang round-trip detected: funds from IBAN ' + origin.IBAN + ' returned within 7 days through ' + toString(size(transactions)) + ' transactions.' AS message,
                   origin.IBAN AS accountIban,
                   [tx IN transactions | tx.Id] AS transactionIds,
                   [account IN accounts | account.IBAN] AS accountIbans,
                   clientIds AS clientIds,
                   [] AS deviceIds,
                   [] AS ipAddresses,
                   totalAmount AS totalAmount
            ORDER BY size(transactions) DESC, totalAmount DESC
            LIMIT $limit
            """;
    }

    public string BuildPepOffshoreDetectionQuery()
    {
        return """
            MATCH (client:Client { IsPep: true })-[:OWNS]->(account:Account)-[:SENT]->(tx:Transaction)
            WHERE account.CountryCode IN $offshoreCountryCodes
              AND tx.Timestamp >= datetime($lookbackFromUtc)
              AND coalesce(tx.Amount, 0.0) > $minTransactionAmount
            RETURN 'PepOffshore' AS type,
                   'Critical' AS severity,
                   'PEP + offshore pattern detected: PEP client ' + client.Name + ' owns offshore account ' + account.IBAN + ' (' + account.CountryCode + ') and sent ' + toString(round(tx.Amount * 100) / 100) + ' ' + tx.Currency + '.' AS message,
                   account.IBAN AS accountIban,
                   [tx.Id] AS transactionIds,
                   [account.IBAN] AS accountIbans,
                   [client.Id] AS clientIds,
                   [] AS deviceIds,
                   [] AS ipAddresses,
                   coalesce(tx.Amount, 0.0) AS totalAmount
            ORDER BY totalAmount DESC
            LIMIT $limit
            """;
    }

    private async Task<IReadOnlyCollection<AmlAlertDto>> RunAlertQueryAsync(
        string alertType,
        string cypher,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var alerts = new List<AmlAlertDto>();

                while (await cursor.FetchAsync())
                {
                    alerts.Add(MapAlert(alertType, cursor.Current));
                }

                return alerts;
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    private static AmlAlertDto MapAlert(string fallbackType, IRecord record)
    {
        var type = GetString(record, "type", fallbackType);
        var transactionIds = GetStringList(record, "transactionIds");
        var accountIbans = GetStringList(record, "accountIbans");
        var clientIds = GetStringList(record, "clientIds");
        var deviceIds = GetStringList(record, "deviceIds");
        var ipAddresses = GetStringList(record, "ipAddresses");
        var involvedNodeKeys = transactionIds
            .Concat(accountIbans)
            .Concat(clientIds)
            .Concat(deviceIds)
            .Concat(ipAddresses)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AmlAlertDto(
            Id: CreateStableAlertId(type, involvedNodeKeys),
            Type: type,
            Severity: GetString(record, "severity", "Medium"),
            Message: GetString(record, "message", $"{type} alert detected."),
            AccountIban: GetString(record, "accountIban", accountIbans.FirstOrDefault() ?? string.Empty),
            TotalAmount: GetDecimal(record, "totalAmount"),
            TransactionIds: transactionIds,
            AccountIbans: accountIbans,
            ClientIds: clientIds,
            DeviceIds: deviceIds,
            IpAddresses: ipAddresses,
            InvolvedNodeKeys: involvedNodeKeys,
            DetectedAtUtc: DateTimeOffset.UtcNow);
    }

    private async Task<IReadOnlyCollection<RiskContributionDto>> GetRiskContributionsAsync(string clientId)
    {
        var contributions = new List<RiskContributionDto>();

        if (await HasClientPatternAsync(clientId, BuildClientSmurfingRiskQuery()))
        {
            contributions.Add(new RiskContributionDto(AmlAlertTypes.Smurfing, "Smurfing alert exists for this client in the last 7 days.", 15));
        }

        if (await HasClientPatternAsync(clientId, BuildClientRapidFanOutRiskQuery()))
        {
            contributions.Add(new RiskContributionDto(AmlAlertTypes.RapidFanOut, "Rapid fan-out alert exists for this client in the last 7 days.", 20));
        }

        if (await HasClientPatternAsync(clientId, BuildClientSharedDeviceOrIpRiskQuery()))
        {
            contributions.Add(new RiskContributionDto(AmlAlertTypes.SharedDeviceOrIp, "Shared device/IP alert exists for this client.", 25));
        }

        if (await HasClientPatternAsync(clientId, BuildClientRoundTripRiskQuery()))
        {
            contributions.Add(new RiskContributionDto(AmlAlertTypes.RoundTrip, "Boomerang round-trip alert exists for this client in the last 7 days.", 20));
        }

        if (await HasClientPatternAsync(clientId, BuildClientPepOffshoreRiskQuery()))
        {
            contributions.Add(new RiskContributionDto(AmlAlertTypes.PepOffshore, "PEP + offshore alert exists for this client in the last 7 days.", 30));
        }

        return contributions;
    }

    private async Task<bool> HasClientPatternAsync(string clientId, string cypher)
    {
        var session = driver.AsyncSession(config => config.WithDatabase(_options.Database));
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, new
                {
                    clientId,
                    sevenDaysAgoUtc = DateTimeOffset.UtcNow.AddDays(-7).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    oneHourAgoUtc = DateTimeOffset.UtcNow.AddHours(-1).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    offshoreCountryCodes = OffshoreCountryCodes
                });

                return await cursor.FetchAsync() && cursor.Current["exists"].As<bool>();
            });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    private static string BuildClientSmurfingRiskQuery()
    {
        return """
            MATCH (client:Client { Id: $clientId })-[:OWNS]->(account:Account)-[:SENT]->(tx:Transaction)
            WHERE tx.Timestamp >= datetime($sevenDaysAgoUtc) AND coalesce(tx.Amount, 0.0) < 10000.0
            WITH account, count(tx) AS txCount, sum(coalesce(tx.Amount, 0.0)) AS totalAmount
            RETURN any(row IN collect({count: txCount, total: totalAmount}) WHERE row.count > 5 AND row.total > 40000.0) AS exists
            """;
    }

    private static string BuildClientRapidFanOutRiskQuery()
    {
        return """
            MATCH (client:Client { Id: $clientId })-[:OWNS]->(account:Account)-[:SENT]->(tx:Transaction)-[:RECEIVED_BY]->(destination:Account)
            WHERE tx.Timestamp >= datetime($sevenDaysAgoUtc)
            WITH account, collect(DISTINCT destination) AS destinations
            RETURN any(destinationSet IN collect(destinations) WHERE size(destinationSet) > 4) AS exists
            """;
    }

    private static string BuildClientSharedDeviceOrIpRiskQuery()
    {
        return """
            MATCH (client:Client { Id: $clientId })
            OPTIONAL MATCH (client)-[:OWNS]->(:Account)-[:SENT]->(:Transaction)-[:EXECUTED_ON_DEVICE]->(device:Device)<-[:EXECUTED_ON_DEVICE]-(:Transaction)<-[:SENT]-(:Account)<-[:OWNS]-(deviceClient:Client)
            WITH client, collect(DISTINCT deviceClient) AS deviceClients
            OPTIONAL MATCH (client)-[:OWNS]->(:Account)-[:SENT]->(:Transaction)-[:EXECUTED_FROM_IP]->(ip:IpAddress)<-[:EXECUTED_FROM_IP]-(:Transaction)<-[:SENT]-(:Account)<-[:OWNS]-(ipClient:Client)
            WITH client, deviceClients, collect(DISTINCT ipClient) AS ipClients
            WITH client,
                 [candidate IN deviceClients WHERE candidate.Id IS NOT NULL] AS deviceClients,
                 [candidate IN ipClients WHERE candidate.Id IS NOT NULL] AS ipClients
            RETURN (size(deviceClients) >= 2 AND any(candidate IN deviceClients WHERE coalesce(candidate.RiskScore, 0.0) > 60.0))
                OR (size(ipClients) >= 2 AND any(candidate IN ipClients WHERE coalesce(candidate.RiskScore, 0.0) > 60.0)) AS exists
            """;
    }

    private static string BuildClientRoundTripRiskQuery()
    {
        return """
            MATCH (client:Client { Id: $clientId })-[:OWNS]->(origin:Account)
            MATCH path = (origin)-[:SENT|RECEIVED_BY*6..16]->(origin)
            WHERE all(index IN range(0, size(relationships(path)) - 1) WHERE
                (index % 2 = 0 AND type(relationships(path)[index]) = 'SENT') OR
                (index % 2 = 1 AND type(relationships(path)[index]) = 'RECEIVED_BY'))
            WITH [node IN nodes(path) WHERE node:Transaction] AS transactions
            WHERE all(tx IN transactions WHERE tx.Timestamp >= datetime($sevenDaysAgoUtc))
            RETURN count(transactions) > 0 AS exists
            """;
    }

    private static string BuildClientPepOffshoreRiskQuery()
    {
        return """
            MATCH (client:Client { Id: $clientId, IsPep: true })-[:OWNS]->(account:Account)-[:SENT]->(tx:Transaction)
            WHERE account.CountryCode IN $offshoreCountryCodes
              AND tx.Timestamp >= datetime($sevenDaysAgoUtc)
              AND coalesce(tx.Amount, 0.0) > 50000.0
            RETURN count(tx) > 0 AS exists
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

    private static Dictionary<string, object?> NormalizeProperties(IReadOnlyDictionary<string, object> properties)
    {
        return properties.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value));
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

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string[] GetStringList(IRecord record, string key)
    {
        if (!record.Keys.Contains(key) || record[key] is null)
        {
            return Array.Empty<string>();
        }

        return record[key].As<List<object>>()
            .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetString(IRecord record, string key, string fallback)
    {
        return record.Keys.Contains(key) && record[key] is not null
            ? Convert.ToString(record[key], CultureInfo.InvariantCulture) ?? fallback
            : fallback;
    }

    private static decimal GetDecimal(IRecord record, string key)
    {
        if (!record.Keys.Contains(key) || record[key] is null)
        {
            return 0m;
        }

        return record[key] switch
        {
            double doubleValue => Convert.ToDecimal(doubleValue),
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => decimalValue,
            _ => decimal.TryParse(Convert.ToString(record[key], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m
        };
    }

    private static string CreateStableAlertId(string type, IReadOnlyCollection<string> involvedNodeKeys)
    {
        var source = $"{type}:{string.Join('|', involvedNodeKeys.Order(StringComparer.OrdinalIgnoreCase))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"alert-{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }
}

