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
    private readonly IBroker _broker;
    private readonly string _accountId;
    private readonly DeltaHedgerSymbolConfiguration _configuration;
    private readonly Position _underlyingPosition;
    private readonly PositionsCollection _positions;
    private readonly SemaphoreSlim _hedgeSemaphore = new(1, 1);
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(ILogger<DeltaHedger> logger, IBroker broker, string accountId, Position underlyingPosition, PositionsCollection positions, DeltaHedgerSymbolConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(accountId));
        _accountId = accountId;
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
            if (greeks == null || float.IsNaN(greeks.Value.Delta) || float.IsNaN(greeks.Value.Gamma)) {
                _logger.LogWarning($"No greeks available for contract {_underlyingPosition.Contract} or NaN. Cannot hedge.");
                return;
            }

            var deltaWithCharm = greeks.Value.Delta + greeks.Value.Charm;
            if (MathF.Abs(deltaWithCharm) < _configuration.Delta + _configuration.MinDeltaAdjustment) {
                _logger.LogDebug($"Delta with Charm is within threshold: {MathF.Abs(deltaWithCharm):f3} < {_configuration.Delta + _configuration.MinDeltaAdjustment}. Delta: {greeks.Value.Delta:f3}, Charm: {greeks.Value.Charm:f3}. No hedging required.");
                _hedgeSemaphore.Release();
                return;
            }

            _logger.LogInformation($"Delta with Charm: {MathF.Abs(deltaWithCharm):f3} exceeds threshold: {_configuration.Delta + _configuration.MinDeltaAdjustment}. Delta: {greeks.Value.Delta:f3}, Charm: {greeks.Value.Charm:f3}. Executing hedge.");

            // Round delta down to 0 in whole numbers
            var deltaHedgeSize = 0 < deltaWithCharm ? MathF.Ceiling(_configuration.Delta - deltaWithCharm) : MathF.Floor(-_configuration.Delta - deltaWithCharm);

            _logger.LogDebug($"Placing delta hedge size: {deltaHedgeSize} for contract {_underlyingPosition.Contract}");
            _broker.PlaceOrder(_accountId, Contract, deltaHedgeSize);
        }
        finally
        {
            // _hedgeSemaphore.Release();
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
