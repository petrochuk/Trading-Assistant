using AppCore.Hedging;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AppCore;

public static class IServiceCollectionExtension
{
    public static IServiceCollection AddAppCore(this IServiceCollection services) {

        services
            .AddTransient<IAccountFactory, AccountFactory>()
            .AddSingleton<ExpirationCalendar>()
            .AddTransient<IDeltaHedgerFactory, DeltaHedgerFactory>();

        return services;
    }
}

