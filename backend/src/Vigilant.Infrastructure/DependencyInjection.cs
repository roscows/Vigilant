using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using Vigilant.Application.Common.Graph;
using Vigilant.Infrastructure.Graph;
using Vigilant.Application.Transactions.SeedTransactions;
using Vigilant.Infrastructure.Options;
using Vigilant.Infrastructure.Seed;

namespace Vigilant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Neo4jOptions>(configuration.GetSection(Neo4jOptions.SectionName));

        services.AddSingleton<IDriver>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<Neo4jOptions>>().Value;
            return GraphDatabase.Driver(options.Uri, AuthTokens.Basic(options.Username, options.Password));
        });

        services.AddScoped<INeo4jRepository, Neo4jRepository>();
        services.AddScoped<ITransactionSeedService, DataSeederService>();

        return services;
    }
}

