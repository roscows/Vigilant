namespace Vigilant.Domain.Alerts;

public sealed record AlertAuditEntry(
    string AnalystName,
    AlertStatus FromStatus,
    AlertStatus ToStatus,
    string Comment,
    DateTime ChangedAt);
