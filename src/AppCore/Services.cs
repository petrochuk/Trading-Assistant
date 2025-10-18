using AppCore.Hedging;
using AppCore.Interfaces;
using AppCore.Models;
using AppCore.Statistics;
using Microsoft.Extensions.DependencyInjection;

namespace AppCore;

public static class IServiceCollectionExtension
{
    public static IServiceCollection AddAppCore(this IServiceCollection services) {

        services
            .AddTransient<IAccountFactory, AccountFactory>()
            .AddTransient<IVolForecaster, HarRvForecaster>(f => 
                new HarRvForecaster(
                    includeDaily: true,
                    includeWeekly: true,
                    includeMonthly: true,
                    useLogVariance: true,
                    includeLeverageEffect: true))
            .AddSingleton<IContractFactory, ContractFactory>()
            .AddSingleton<ExpirationCalendar>()
            .AddTransient<IDeltaHedgerFactory, DeltaHedgerFactory>()
            .AddSingleton<ISoundPlayer, Media.SoundPlayer>();

        return services;
    }
}

