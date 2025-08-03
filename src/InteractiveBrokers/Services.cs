using AppCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InteractiveBrokers;

public static class IServiceCollectionExtension
{
    public static IServiceCollection AddInteractiveBrokers(this IServiceCollection services) {

        services
            .AddSingleton<IBClient>()
            .AddSingleton<IBroker, IBClient>()
            .AddSingleton<IBWebSocket>();

        return services;
    }
}
