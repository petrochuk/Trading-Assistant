using Microsoft.Extensions.DependencyInjection;

namespace AppCore;

public static class IServiceCollectionExtension
{
    public static IServiceCollection AddAppCore(this IServiceCollection services) {

        services
            .AddSingleton<PositionsCollection>();

        return services;
    }
}

