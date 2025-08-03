using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;

namespace AppCore.Hedging;

/// <summary>
/// Delta hedging service for single contract.
/// </summary
[DebuggerDisplay("{Contract}")]
public class DeltaHedger : IDeltaHedger, IDisposable
{
    private readonly ILogger<DeltaHedger> _logger;
    private readonly DeltaHedgerSymbolConfiguration _configuration;
    private readonly Position _underlyingPosition;
    private readonly PositionsCollection _positions;
    private readonly SemaphoreSlim _hedgeSemaphore = new(1, 1);
    private bool _disposed = false;

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
        if (_disposed)
            throw new ObjectDisposedException(nameof(DeltaHedger), "Cannot hedge after the hedger has been disposed.");

        // Try to acquire the semaphore without blocking
        if (!_hedgeSemaphore.Wait(0))
        {
            _logger.LogDebug($"Hedge execution already in progress for contract {_underlyingPosition.Contract}. Skipping overlapping execution.");
            return;
        }

        try
        {
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
        finally
        {
            _hedgeSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _hedgeSemaphore?.Dispose();
        _logger.LogDebug($"DeltaHedger for contract {_underlyingPosition.Contract} disposed.");
    }
}
