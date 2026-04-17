namespace Vigilant.Domain.Graph;

public sealed record ClientNode(
    string Id,
    string Name,
    decimal RiskScore,
    bool IsPep = false);
