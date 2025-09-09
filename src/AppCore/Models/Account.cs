using AppCore.Configuration;
using AppCore.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AppCore.Models;

/// <summary>
/// Brokerage account.
/// </summary>
public class Account : IDisposable, INotifyPropertyChanged
{
    #region Fields
    
    private readonly ILogger<Account> _logger;
    private readonly IBroker _broker;
    private readonly IDeltaHedgerFactory _deltaHedgerFactory;
    private readonly ConcurrentDictionary<string, IDeltaHedger> _deltaHedgers = new();
    private readonly DeltaHedgerConfiguration _deltaHedgerConfiguration;
    private readonly Timer? _hedgeTimer;
    private bool _disposed = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    public Account(
        string id, string name,
        ILogger<Account> logger,
        IBroker broker,
        PositionsCollection positionsCollection, 
        IDeltaHedgerFactory deltaHedgerFactory,
        DeltaHedgerConfiguration deltaHedgerConfiguration) 
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(id));
        Id = id;
        Name = name;

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        Positions = positionsCollection;
        _deltaHedgerFactory = deltaHedgerFactory ?? throw new ArgumentNullException(nameof(deltaHedgerFactory));
        _deltaHedgerConfiguration = deltaHedgerConfiguration ?? throw new ArgumentNullException(nameof(deltaHedgerConfiguration));

        // Initialize the hedge timer
        if (_deltaHedgerConfiguration.SupportedAccounts.Contains(Id)) {
            Positions.Underlyings.CollectionChanged += OnUnderlyingsChanged;
            _hedgeTimer = new Timer(ExecuteHedgers, null, _deltaHedgerConfiguration.HedgeInterval, _deltaHedgerConfiguration.HedgeInterval);
            _logger.LogInformation($"Delta hedge timer started with interval: {_deltaHedgerConfiguration.HedgeInterval}");
        }
        else {
            _logger.LogInformation($"Account {Name} is not supported for delta hedging. Skipping timer initialization.");
        }
    }

    public string Name { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    private float _netLiquidationValue;
    public float NetLiquidationValue {
        get => _netLiquidationValue;
        set {
            if (_netLiquidationValue != value) {
                _netLiquidationValue = value;
                ExcessLiquidityPct = _netLiquidationValue != 0 ? (_excessLiquidity / _netLiquidationValue): 0;
                PostExpirationExcessPct = _netLiquidationValue != 0 ? (_postExpirationExcess / _netLiquidationValue) : 0;
                OnPropertyChanged();
            }
        }
    }

    private float _excessLiquidity;
    public float ExcessLiquidity {
        get => _excessLiquidity;
        set {
            if (_excessLiquidity != value) {
                _excessLiquidity = value;
                ExcessLiquidityPct = _netLiquidationValue != 0 ? (_excessLiquidity / _netLiquidationValue): 0;
                OnPropertyChanged();
            }
        }
    }

    private float _excessLiquidityPct;
    public float ExcessLiquidityPct {
        get => _excessLiquidityPct;
        set {
            if (_excessLiquidityPct != value) {
                _excessLiquidityPct = value;
                OnPropertyChanged();
            }
        }
    }

    private float _postExpirationExcess;
    public float PostExpirationExcess {
        get => _postExpirationExcess;
        set {
            if (_postExpirationExcess != value) {
                _postExpirationExcess = value;
                PostExpirationExcessPct = _netLiquidationValue != 0 ? (_postExpirationExcess / _netLiquidationValue) : 0;
                OnPropertyChanged();
            }
        }
    }

    private float _postExpirationExcessPct;
    public float PostExpirationExcessPct {
        get => _postExpirationExcessPct;
        set {
            if (_postExpirationExcessPct != value) {
                _postExpirationExcessPct = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyDictionary<string, IDeltaHedger> DeltaHedgers => _deltaHedgers;

    public override string ToString() {
        return $"{Name} ({Id})";
    }

    public PositionsCollection Positions { get; init; }

    private void OnUnderlyingsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action == NotifyCollectionChangedAction.Add) {
            foreach (var item in e.NewItems ?? Array.Empty<object>()) {
                if (item is UnderlyingPosition underlying) {

                    if (_deltaHedgers.ContainsKey(underlying.Symbol)) {
                        _logger.LogInformation($"Delta hedger for {underlying.Symbol} already exists");
                        continue;
                    }

                    if (!_deltaHedgerConfiguration.Configs.ContainsKey(underlying.Symbol)) {
                        _logger.LogInformation($"Delta hedger configuration not found for {underlying.Symbol}. Skipping.");
                        continue;
                    }

                    var deltaHedger = _deltaHedgerFactory.Create(_broker, Id, underlying, Positions, _deltaHedgerConfiguration);
                    if (_deltaHedgers.TryAdd(underlying.Symbol, deltaHedger)) {
                        _logger.LogInformation($"Delta hedger added for {underlying.Symbol}");
                    }
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove) {
            // Build list of symbols
            var symbols = new HashSet<string>(Positions.Underlyings.Select(u => u.Symbol));

            // Remove hedgers for positions no longer in the underlyings collection
            var toRemove = _deltaHedgers.Keys.Except(symbols).ToList();
            foreach (var symbol in toRemove) {
                if (_deltaHedgers.TryRemove(symbol, out var deltaHedger)) {
                    deltaHedger.Dispose();
                    _logger.LogInformation($"Delta hedger removed and disposed for contract symbol {symbol}");
                }
            }
        }
    }

    private void ExecuteHedgers(object? state)
    {
        if (_disposed)
            return;

        try
        {
            if (_deltaHedgers.IsEmpty)
            {
                _logger.LogDebug("No delta hedgers to execute");
                return;
            }

            _logger.LogDebug($"Executing {_deltaHedgers.Count} delta hedger(s) on '{Name}'");

            foreach (var hedger in _deltaHedgers.Values)
            {
                try
                {
                    hedger.Hedge();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error executing delta hedger for {hedger.UnderlyingPosition.Symbol}");
                }
            }

            _logger.LogDebug("Completed executing all delta hedgers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hedge timer execution");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _hedgeTimer?.Dispose();
        
        // Dispose all delta hedgers
        foreach (var hedger in _deltaHedgers.Values)
        {
            try
            {
                hedger.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disposing delta hedger for {hedger.UnderlyingPosition.Symbol}");
            }
        }
        _deltaHedgers.Clear();
        
        Positions.Underlyings.CollectionChanged -= OnUnderlyingsChanged;
        _logger.LogInformation($"Account {Name} is disposed and hedge timer stopped");
    }
}
