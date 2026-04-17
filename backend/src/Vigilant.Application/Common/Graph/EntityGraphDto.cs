namespace Vigilant.Application.Common.Graph;

public sealed record EntityGraphDto(
    IReadOnlyCollection<GraphNodeDto> Nodes,
    IReadOnlyCollection<GraphEdgeDto> Edges);

public sealed record GraphNodeDto(
    string Id,
    string Label,
    IReadOnlyCollection<string> Labels,
    IReadOnlyDictionary<string, object?> Properties);

public sealed record GraphEdgeDto(
    string Id,
    string SourceId,
    string TargetId,
    string Type,
    IReadOnlyDictionary<string, object?> Properties);
