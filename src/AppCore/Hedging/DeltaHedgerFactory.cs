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

    public DeltaHedgerFactory(ILogger<DeltaHedger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IDeltaHedger Create(Position underlyingPosition, PositionsCollection positions, DeltaHedgerConfiguration configuration)
    {
        var symbolConfiguration = configuration.Configs[underlyingPosition.Contract.Symbol];
        if (symbolConfiguration == null)
            throw new ArgumentException($"No configuration found for symbol {underlyingPosition.Contract.Symbol}", nameof(configuration));

        return new DeltaHedger(_logger, underlyingPosition, positions, symbolConfiguration);
    }
}
