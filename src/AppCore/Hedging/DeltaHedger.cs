using AppCore.Args;
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
public class DeltaHedger : IDeltaHedger, IDisposable
{
    private readonly ILogger<DeltaHedger> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IBroker _broker;
    private readonly string _accountId;
    private readonly DeltaHedgerSymbolConfiguration _configuration;
    private readonly Position _underlyingPosition;
    private readonly PositionsCollection _positions;
    private readonly SemaphoreSlim _hedgeSemaphore = new(1, 1);
    private Guid? _activeOrderId;
    private DateTimeOffset? _hedgeDelay;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(ILogger<DeltaHedger> logger, TimeProvider timeProvider, IBroker broker, string accountId, 
        Position underlyingPosition, PositionsCollection positions, DeltaHedgerSymbolConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(accountId));
        _accountId = accountId;
        _underlyingPosition = underlyingPosition ?? throw new ArgumentNullException(nameof(underlyingPosition));
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _broker.OnOrderPlaced += Broker_OnOrderPlaced;
    }

    public Position UnderlyingPosition => _underlyingPosition;

    public Contract Contract => _underlyingPosition.Contract;

    public void Hedge() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DeltaHedger), "Cannot hedge after the hedger has been disposed.");

        if (_activeOrderId.HasValue) {
            _logger.LogDebug($"Hedge order already in progress for contract {_underlyingPosition.Contract}. Skipping.");
            return;
        }

        if (_hedgeDelay.HasValue && _timeProvider.GetUtcNow() < _hedgeDelay.Value)
        {
            _logger.LogDebug($"Hedge execution delayed for contract {_underlyingPosition.Contract}. Skipping until delay expires in {_hedgeDelay.Value - _timeProvider.GetUtcNow():hh\\:mm\\:ss}.");
            return;
        }

        // Try to acquire the semaphore without blocking
        if (!_hedgeSemaphore.Wait(0))
        {
            _logger.LogDebug($"Hedge execution already in progress for contract {_underlyingPosition.Contract}. Skipping overlapping execution.");
            return;
        }

        try
        {
            _logger.LogDebug($"Executing delta hedger for contract {_underlyingPosition.Contract}");

            var greeks = _positions.CalculateGreeks(_configuration.MinIV, _underlyingPosition);
            if (greeks == null || float.IsNaN(greeks.Value.Delta) || float.IsNaN(greeks.Value.Charm)) {
                _logger.LogWarning($"No greeks available for contract {_underlyingPosition.Contract} or NaN. Cannot hedge.");
                return;
            }

            var deltaPlus1 = greeks.Value.Delta;
            if (MathF.Abs(deltaPlus1) < _configuration.Delta + _configuration.MinDeltaAdjustment) {
                _logger.LogDebug($"Delta+1 is within threshold: Abs({deltaPlus1:f3}) < {_configuration.Delta + _configuration.MinDeltaAdjustment}. Delta: {greeks.Value.Delta:f3}, Charm: {greeks.Value.Charm:f3}. No hedging required.");
                return;
            }

            _logger.LogInformation($"Delta+1: Abs({deltaPlus1:f3}) exceeds threshold: {_configuration.Delta + _configuration.MinDeltaAdjustment}. Delta: {greeks.Value.Delta:f3}, Charm: {greeks.Value.Charm:f3}. Executing hedge.");

            // Round delta down to 0 in whole numbers
            var deltaHedgeSize = 0 < deltaPlus1 ? MathF.Ceiling(_configuration.Delta - deltaPlus1) : MathF.Floor(-_configuration.Delta - deltaPlus1);

            _logger.LogDebug($"Placing delta hedge size: {deltaHedgeSize} for contract {_underlyingPosition.Contract}");
            // Set a delay to prevent immediate re-hedging
            _hedgeDelay = _timeProvider.GetUtcNow().AddMinutes(2);
            _activeOrderId = Guid.NewGuid(); // Generate a new order ID for tracking
            _broker.PlaceOrder(_accountId, _activeOrderId.Value, Contract, deltaHedgeSize);
        }
        finally
        {
            _hedgeSemaphore.Release();
        }
    }

    private void Broker_OnOrderPlaced(object? sender, OrderPlacedArgs e)
    {
        if (e.AccountId != _accountId || e.Contract.Id != _underlyingPosition.Contract.Id)
        {
            _logger.LogDebug($"Order placed for different account or contract. Ignoring: AccountId={e.AccountId}, ContractId={e.Contract.Id}");
            return;
        }

        if (e.OrderId != _activeOrderId)
        {
            _logger.LogDebug($"Order placed with different ID. Ignoring: Expected={_activeOrderId}, Actual={e.OrderId}");
            return;
        }

        if (!string.IsNullOrEmpty(e.ErrorMessage)) {
            _logger.LogInformation($"Delta hedge order {_activeOrderId} failed to be placed. Delay hedging");
            _hedgeDelay = _timeProvider.GetUtcNow().AddHours(1);
            _activeOrderId = null; // Reset active order ID
            return;
        }

        _logger.LogInformation($"Delta hedge order {_activeOrderId} placed successfully for contract {_underlyingPosition.Contract}.");
        _activeOrderId = null; // Reset active order ID after successful placement
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
