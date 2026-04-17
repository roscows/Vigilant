namespace Vigilant.Application.Common.Graph;

public sealed record AmlAlertDto(
    string Id,
    string Type,
    string Severity,
    string Message,
    string AccountIban,
    decimal TotalAmount,
    IReadOnlyCollection<string> TransactionIds,
    IReadOnlyCollection<string> AccountIbans,
    IReadOnlyCollection<string> ClientIds,
    IReadOnlyCollection<string> DeviceIds,
    IReadOnlyCollection<string> IpAddresses,
    IReadOnlyCollection<string> InvolvedNodeKeys,
    DateTimeOffset DetectedAtUtc);
