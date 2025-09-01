using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.Logging;

namespace AppCore.Hedging;

/// <summary>
/// Factory for creating delta hedgers.
/// </summary>
public class DeltaHedgerFactory : IDeltaHedgerFactory
{
    private readonly ILogger<DeltaHedger> _logger;
    private readonly TimeProvider _timeProvider;

    public DeltaHedgerFactory(ILogger<DeltaHedger> logger, TimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public IDeltaHedger Create(IBroker broker, 
        string accountId, UnderlyingPosition underlying, PositionsCollection positions, DeltaHedgerConfiguration configuration)
    {
        var symbolConfiguration = configuration.Configs[underlying.Symbol];
        if (symbolConfiguration == null)
            throw new ArgumentException($"No configuration found for symbol {underlying.Symbol}", nameof(configuration));

        return new DeltaHedger(_logger, _timeProvider, broker, accountId, underlying, positions, symbolConfiguration);
    }
}
