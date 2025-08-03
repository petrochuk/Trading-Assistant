using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AppCore.Hedging;

/// <summary>
/// Delta hedging service for single contract.
/// </summary
[DebuggerDisplay("{Contract}")]
public class DeltaHedger : IDeltaHedger
{
    private readonly ILogger<DeltaHedger> _logger;
    private readonly DeltaHedgerSymbolConfiguration _configuration;
    private readonly Position _underlyingPosition;
    private readonly PositionsCollection _positions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(ILogger<DeltaHedger> logger, Position underlyingPosition, PositionsCollection positions, DeltaHedgerSymbolConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _underlyingPosition = underlyingPosition ?? throw new ArgumentNullException(nameof(underlyingPosition));
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Position UnderlyingPosition => _underlyingPosition;

    public Contract Contract => _underlyingPosition.Contract;

    public void Hedge() {
        _logger.LogDebug($"Executing delta hedger for contract {_underlyingPosition.Contract}");

        var greeks = _positions.CalculateGreeks(_underlyingPosition);
        if (greeks == null) {
            _logger.LogWarning($"No greeks available for contract {_underlyingPosition.Contract}. Cannot hedge.");
            return;
        }

        var deltaWithCharm = greeks.Value.Delta + greeks.Value.Charm;
        if (MathF.Abs(deltaWithCharm) < _configuration.Delta) {
            _logger.LogDebug($"Delta with Charm is within threshold: {deltaWithCharm} < {_configuration.Delta}. Delta: {greeks.Value.Delta}, Charm: {greeks.Value.Charm}. No hedging required.");
            return;
        }

        _logger.LogInformation($"Delta with Charm: {deltaWithCharm} exceeds threshold: {_configuration.Delta}. Delta: {greeks.Value.Delta}, Charm: {greeks.Value.Charm}. Executing hedge.");
    }
}
