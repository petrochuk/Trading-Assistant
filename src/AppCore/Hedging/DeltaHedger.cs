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
    #region Fields & Constructor

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

    #endregion

    #region Properties

    public UnderlyingPosition UnderlyingPosition => _underlyingPosition;

    public DeltaHedgerSymbolConfiguration Configuration => _configuration;

    public Greeks? LastGreeks { get; private set; }

    #endregion

    public void Hedge() {
        // Try to acquire the semaphore without blocking
        if (!_hedgeSemaphore.Wait(0)) {
            _logger.LogDebug($"Hedging in progress. Skipping overlapping execution.");
            return;
        }

        try {
            LastGreeks = _positions.CalculateGreeks(_configuration.MinIV, _underlyingPosition, _volForecaster, addOvervaluedOptions: true, logger: _logger);
            if (LastGreeks != null) {
                _logger.LogInformation($"Greeks Delta: {LastGreeks.DeltaTotal:f3}, Heston Delta: {LastGreeks.DeltaHestonTotal:f3}, Theta: {LastGreeks.Theta:f3}");
            }

            if (!IsHedgeExecutionAllowed()) {
                return;
            }

            _logger.LogDebug($"Executing delta hedger");
            if (_underlyingPosition.RealizedVol != null) {
                if (_underlyingPosition.RealizedVol.TryGetValue(out var realizedVol))
                    _logger.LogDebug($"Realized Vol: {realizedVol:f4}");
                if (_underlyingPosition.RealizedVol.TryGetVolatilityOfVolatility(out var volOfVol))
                    _logger.LogDebug($"Vol of Vol: {volOfVol:f4}");
            } else
                _logger.LogDebug($"Vol of Vol: N/A");

            if (LastGreeks == null || !LastGreeks.IsDeltaValid) {
                _logger.LogWarning($"No greeks available for contract {_underlyingPosition.Symbol} or NaN. Cannot hedge.");
                return;
            }

            var deltaOTMHedgeSize = 0 < LastGreeks.DeltaOTM ? -MathF.Round(LastGreeks.DeltaOTM) : -MathF.Round(LastGreeks.DeltaOTM);
            var deltaITMHedgeSize = 0 < LastGreeks.DeltaITM ? -MathF.Round(LastGreeks.DeltaITM) : -MathF.Round(LastGreeks.DeltaITM);
            var deltaBuffer = 0.20f;
            var deltaHedgeSize = 0 < LastGreeks.DeltaHestonTotal ? -MathF.Round(LastGreeks.DeltaHestonTotal - deltaBuffer) : -MathF.Round(LastGreeks.DeltaHestonTotal + deltaBuffer);
            //var deltaHedgeSize = deltaOTMHedgeSize + deltaITMHedgeSize;
            if (MathF.Abs(deltaHedgeSize) < _configuration.Delta) {
                _logger.LogDebug($"{_underlyingPosition.Symbol} delta is within threshold: Abs({LastGreeks.DeltaHedge - deltaHedgeSize:f3}) < {_configuration.Delta}. Total:{LastGreeks.DeltaTotal:f3} Total Heston:{LastGreeks.DeltaHestonTotal:f3} OTM:{LastGreeks.DeltaOTM:f3} ITM:{LastGreeks.DeltaITM:f3}. No hedging required.");
                return;
            }

            _logger.LogInformation($"{_accountId.Mask()} {_underlyingPosition.Symbol} delta total:{LastGreeks.DeltaTotal:f3} Total Heston:{LastGreeks.DeltaHestonTotal:f3} exceeds threshold: {_configuration.Delta}. OTM:{LastGreeks.DeltaOTM:f3} ITM:{LastGreeks.DeltaITM:f3}, Executing hedge.");

            // Round delta down to 0 in whole numbers
            var deltaAdjustment = deltaHedgeSize;// - LastGreeks.DeltaHedge;
            if (MathF.Abs(deltaAdjustment) < _configuration.MinDeltaAdjustment) {
                _logger.LogDebug($"{_underlyingPosition.Symbol} delta hedge adjustment {deltaAdjustment:f3} is below minimum adjustment {_configuration.MinDeltaAdjustment}. No hedging required.");
                return;
            }

            _logger.LogDebug($"Placing delta hedge size: {deltaAdjustment} for {_underlyingPosition.Symbol}");
            _activeOrderId = Guid.NewGuid(); // Generate a new order ID for tracking
            _broker.PlaceOrder(_accountId, _activeOrderId.Value, _underlyingPosition.FrontContract, 0 < deltaAdjustment ? 1 : -1, deltaAdjustment);
        } finally {
            _hedgeSemaphore.Release();
        }
    }

    private bool IsHedgeExecutionAllowed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DeltaHedger), "Cannot hedge after the hedger has been disposed.");

        if (_underlyingPosition.FrontContract == null) {
            _logger.LogWarning($"No front contract available for {_underlyingPosition.Symbol}. Cannot hedge.");
            return false;
        }

        if (_activeOrderId.HasValue) {
            _logger.LogDebug($"Hedge order already in progress for {_underlyingPosition.Symbol}. Skipping.");
            return false;
        }

        if (_hedgeDelay.HasValue && _timeProvider.EstNow() < _hedgeDelay.Value) {
            _logger.LogDebug($"Hedge execution delayed for {_underlyingPosition.Symbol}. Skipping until delay expires in {_hedgeDelay.Value - _timeProvider.EstNow():hh\\:mm\\:ss}.");
            return false;
        }

        // Check blackout periods and holidays
        var now = _timeProvider.EstNow();
        if (!now.IsOpen()) {
            _logger.LogDebug($"Market is closed. Skipping hedge for {_underlyingPosition.Symbol}.");
            return false;
        }
        if (_configuration.BlackOutStart != null && _configuration.BlackOutEnd != null) {
            // Normal case: blackout period does not cross midnight
            if (_configuration.BlackOutStart < _configuration.BlackOutEnd) {
                if (now.TimeOfDay >= _configuration.BlackOutStart && now.TimeOfDay <= _configuration.BlackOutEnd) {
                    _logger.LogDebug($"Current time {now} is within blackout period {_configuration.BlackOutStart} - {_configuration.BlackOutEnd} for {_underlyingPosition.Symbol}. Skipping hedge.");
                    return false;
                }
            }
        }

        return true;
    }

    private void Broker_OnOrderPlaced(object? sender, OrderPlacedArgs e)
    {
        if (e.AccountId != _accountId || _underlyingPosition.FrontContract == null || e.Contract.Id != _underlyingPosition.FrontContract.Id)
        {
            _logger.LogTrace($"Order placed for different account or contract. Ignoring: AccountId={e.AccountId}, ContractId={e.Contract.Id}");
            return;
        }

        if (e.OrderId != _activeOrderId)
        {
            _logger.LogTrace($"Order placed with different ID. Ignoring: Expected={_activeOrderId}, Actual={e.OrderId}");
            return;
        }

        // Delay next hedge attempt by 5 minutes both on success and failure
        _hedgeDelay = _timeProvider.EstNow().AddMinutes(5);
        _activeOrderId = null; // Reset active order ID
        _logger.LogDebug($"Next hedge attempt delayed until {_hedgeDelay.Value}.");

        if (!string.IsNullOrEmpty(e.ErrorMessage)) {
            _logger.LogInformation($"Delta hedge order {_activeOrderId} failed to be placed. Delay hedging");
            _soundPlayer?.PlaySound("CarAlarm");
            return;
        }

        _logger.LogInformation($"Delta hedge order {_activeOrderId} placed successfully for {_underlyingPosition.Symbol}.");
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
