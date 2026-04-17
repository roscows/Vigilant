namespace Vigilant.Application.Common.Graph;

public sealed record TransactionGraphWriteModel(
    string TransactionId,
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string Currency,
    DateTimeOffset TimestampUtc,
    string DeviceId,
    string IpAddress,
    string? IpCountryCode,
    string? BrowserFingerprint,
    ClientGraphSnapshot? SenderClient,
    ClientGraphSnapshot? ReceiverClient,
    decimal? SenderBalance = null,
    decimal? ReceiverBalance = null,
    string? SenderAccountCountryCode = null,
    string? ReceiverAccountCountryCode = null);
