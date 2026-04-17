namespace Vigilant.Domain.Graph;

public sealed record TransactionNode(
    string Id,
    decimal Amount,
    DateTimeOffset Timestamp,
    string Currency);
