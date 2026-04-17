namespace Vigilant.Application.Common.Graph;

public sealed record TransactionGraphWriteResult(
    string TransactionId,
    string SenderAccountId,
    string ReceiverAccountId,
    DateTimeOffset PersistedAtUtc);
