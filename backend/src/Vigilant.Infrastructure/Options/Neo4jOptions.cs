namespace Vigilant.Infrastructure.Options;

public sealed class Neo4jOptions
{
    public const string SectionName = "Neo4j";

    public string Uri { get; init; } = "bolt://localhost:7687";
    public string Username { get; init; } = "neo4j";
    public string Password { get; init; } = "vigilant_dev_password";
    public string Database { get; init; } = "neo4j";
}
