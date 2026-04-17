namespace Vigilant.Application.Common.Graph;

public sealed record ClientGraphSnapshot(
    string Id,
    string Name,
    decimal RiskScore);
