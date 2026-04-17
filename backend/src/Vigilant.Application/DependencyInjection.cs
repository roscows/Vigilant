using Microsoft.Extensions.DependencyInjection;
using Vigilant.Application.Transactions.ProcessTransaction;

namespace Vigilant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(TransactionProcessorCommand).Assembly));

        return services;
    }
}
