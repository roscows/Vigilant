namespace Vigilant.Domain.Alerts;

public sealed record AlertNode(
    Guid Id,
    string RuleType,
    string Severity,
    AlertStatus Status,
    IReadOnlyCollection<string> InvolvedAccountIds,
    DateTime DetectedAt,
    IReadOnlyCollection<AlertAuditEntry> AuditLog,
    string Message = "");
