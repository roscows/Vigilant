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
    DateTimeOffset DetectedAtUtc);
