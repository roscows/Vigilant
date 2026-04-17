using Vigilant.Application.Common.Graph;

namespace Vigilant.Api.Contracts.Transactions;

public sealed record ProcessTransactionRequest(
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string Currency,
    string DeviceId,
    string IpAddress,
    string? IpCountryCode,
    string? BrowserFingerprint,
    ClientSnapshotRequest? SenderClient,
    ClientSnapshotRequest? ReceiverClient)
{
    public ClientGraphSnapshot? ToSenderClientSnapshot() => SenderClient?.ToSnapshot();
    public ClientGraphSnapshot? ToReceiverClientSnapshot() => ReceiverClient?.ToSnapshot();
}

public sealed record ClientSnapshotRequest(
    string Id,
    string Name,
    decimal RiskScore)
{
    public ClientGraphSnapshot ToSnapshot() => new(Id, Name, RiskScore);
}
