namespace Vigilant.Api.Contracts.Alerts;

public sealed record UpdateAlertStatusRequest(
    string NewStatus,
    string AnalystName,
    string Comment);