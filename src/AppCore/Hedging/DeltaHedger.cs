using AppCore.Args;
using AppCore.Configuration;
using AppCore.Extenstions;
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
    private readonly UnderlyingPosition _underlyingPosition;
    private readonly PositionsCollection _positions;
    private readonly SemaphoreSlim _hedgeSemaphore = new(1, 1);
    private readonly IVolForecaster? _volForecaster;
    private readonly ISoundPlayer? _soundPlayer;
    private Guid? _activeOrderId;
    private DateTimeOffset? _hedgeDelay;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(ILogger<DeltaHedger> logger, TimeProvider timeProvider, IBroker broker, string accountId,
        UnderlyingPosition underlyingPosition, PositionsCollection positions, DeltaHedgerSymbolConfiguration configuration,
        IVolForecaster? volForecaster, ISoundPlayer? soundPlayer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(accountId));
        _accountId = accountId;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _underlyingPosition = underlyingPosition ?? throw new ArgumentNullException(nameof(underlyingPosition));
        _underlyingPosition.RealizedVol?.Reset(_configuration.InitialIV);
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));
        _volForecaster = volForecaster;
        _soundPlayer = soundPlayer; 

        _broker.OnOrderPlaced += Broker_OnOrderPlaced;

    }

    public UnderlyingPosition UnderlyingPosition => _underlyingPosition;

    public DeltaHedgerSymbolConfiguration Configuration => _configuration;

    public Greeks? LastGreeks { get; private set; }

    public void Hedge() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DeltaHedger), "Cannot hedge after the hedger has been disposed.");

        if (_activeOrderId.HasValue) {
            _logger.LogDebug($"Hedge order already in progress for {_underlyingPosition.Symbol}. Skipping.");
            return;
        }

        if (_underlyingPosition.FrontContract == null) {
            _logger.LogWarning($"No front contract available for {_underlyingPosition.Symbol}. Cannot hedge.");
            return;
        }

        if (_volForecaster != null && !_volForecaster.IsCalibrated) {
            _logger.LogInformation($"Vol forecaster not calibrated. Calibrating from file for {_underlyingPosition.Symbol}.");
            _volForecaster.CalibrateFromFile(_underlyingPosition.FrontContract.OHLCHistoryFilePath);
            _volForecaster.Symbol = _underlyingPosition.FrontContract.OHLCHistoryFilePath;
        }

        if (_hedgeDelay.HasValue && _timeProvider.GetUtcNow() < _hedgeDelay.Value)
        {
            _logger.LogDebug($"Hedge execution delayed for {_underlyingPosition.Symbol}. Skipping until delay expires in {_hedgeDelay.Value - _timeProvider.GetUtcNow():hh\\:mm\\:ss}.");
            return;
        }

        // Check blackout periods
        var now = _timeProvider.EstNow().TimeOfDay;
        if (_configuration.BlackOutStart != null && _configuration.BlackOutEnd != null)
        {
            // Normal case: blackout period does not cross midnight
            if (_configuration.BlackOutStart < _configuration.BlackOutEnd)
            {
                if (now >= _configuration.BlackOutStart && now <= _configuration.BlackOutEnd)
                {
                    _logger.LogDebug($"Current time {now} is within blackout period {_configuration.BlackOutStart} - {_configuration.BlackOutEnd} for {_underlyingPosition.Symbol}. Skipping hedge.");
                    return;
                }
            }
        }

        // Try to acquire the semaphore without blocking
        if (!_hedgeSemaphore.Wait(0))
        {
            _logger.LogDebug($"Hedging in progress for {_underlyingPosition.Symbol}. Skipping overlapping execution.");
            return;
        }

        try
        {
            _logger.LogDebug($"Executing delta hedger for {_underlyingPosition.Symbol}");
            if (_underlyingPosition.RealizedVol != null) {
                if (_underlyingPosition.RealizedVol.TryGetValue(out var realizedVol))
                    _logger.LogDebug($"Realized Vol for {_underlyingPosition.Symbol}: {realizedVol:f4}");
                if (_underlyingPosition.RealizedVol.TryGetVolatilityOfVolatility(out var volOfVol))
                    _logger.LogDebug($"Vol of Vol for {_underlyingPosition.Symbol}: {volOfVol:f4}");
            }
            else
                _logger.LogDebug($"Vol of Vol: N/A for {_underlyingPosition.Symbol}");

            LastGreeks = _positions.CalculateGreeks(_configuration.MinIV, _underlyingPosition, _volForecaster, addOvervaluedOptions: true);
            if (LastGreeks == null || float.IsNaN(LastGreeks.Delta) || float.IsNaN(LastGreeks.Charm)) {
                _logger.LogWarning($"No greeks available for contract {_underlyingPosition.Symbol} or NaN. Cannot hedge.");
                return;
            }
            _logger.LogInformation($"Greeks for {_underlyingPosition.Symbol}: Delta: {LastGreeks.Delta:f3}, Theta: {LastGreeks.Theta:f3}");

            var deltaHedgeSize = 0 < LastGreeks.Delta ? -MathF.Floor(LastGreeks.Delta) : -MathF.Ceiling(LastGreeks.Delta);
            if (MathF.Abs(LastGreeks.DeltaHedge - deltaHedgeSize) < _configuration.Delta) {
                _logger.LogDebug($"{_accountId.Mask()} {_underlyingPosition.Symbol} delta is within threshold: Abs({LastGreeks.Delta:f3}) < {_configuration.Delta}. Delta: {LastGreeks.Delta:f3}. No hedging required.");
                return;
            }

            _logger.LogInformation($"{_accountId.Mask()} {_underlyingPosition.Symbol} delta Abs({LastGreeks.DeltaTotal:f3}) exceeds threshold: {_configuration.Delta}. Delta: {LastGreeks.Delta:f3}, Executing hedge.");

            // Round delta down to 0 in whole numbers
            var deltaAdjustment = deltaHedgeSize - LastGreeks.DeltaHedge;
            if (MathF.Abs(deltaAdjustment) < _configuration.MinDeltaAdjustment ) {
                _logger.LogDebug($"{_accountId.Mask()} {_underlyingPosition.Symbol} delta hedge adjustment {deltaAdjustment:f3} is below minimum adjustment {_configuration.MinDeltaAdjustment}. No hedging required.");
                return;
            }

            _logger.LogDebug($"Placing delta hedge size: {deltaAdjustment} for {_underlyingPosition.Symbol}");
            // Set a delay to prevent immediate re-hedging
            _hedgeDelay = _timeProvider.GetUtcNow().AddMinutes(2);
            _activeOrderId = Guid.NewGuid(); // Generate a new order ID for tracking
            _broker.PlaceOrder(_accountId, _activeOrderId.Value, _underlyingPosition.FrontContract, deltaAdjustment);
        }
        finally
        {
            _hedgeSemaphore.Release();
        }
    }

    private void Broker_OnOrderPlaced(object? sender, OrderPlacedArgs e)
    {
        if (e.AccountId != _accountId || _underlyingPosition.FrontContract == null || e.Contract.Id != _underlyingPosition.FrontContract.Id)
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
            _hedgeDelay = _timeProvider.GetUtcNow().AddMinutes(5);
            _activeOrderId = null; // Reset active order ID
            _soundPlayer?.PlaySound("CarAlarm");
            return;
        }

        _logger.LogInformation($"Delta hedge order {_activeOrderId} placed successfully for {_underlyingPosition.Symbol}.");
        _activeOrderId = null; // Reset active order ID after successful placement
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_hedgeSemaphore != null) {
            _hedgeSemaphore.Wait();
            _hedgeSemaphore.Dispose();
        }
        _logger.LogDebug($"DeltaHedger for {_underlyingPosition.Symbol} disposed.");
        _disposed = true;
    }
}
