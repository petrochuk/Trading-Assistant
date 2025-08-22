using AppCore.Configuration;
using AppCore.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

/// <summary>
/// Brokerage account.
/// </summary>
public class Account : IDisposable
{
    #region Fields
    
    private readonly ILogger<Account> _logger;
    private readonly IBroker _broker;
    private readonly IDeltaHedgerFactory _deltaHedgerFactory;
    private readonly ConcurrentDictionary<int, IDeltaHedger> _deltaHedgers = new();
    private readonly DeltaHedgerConfiguration _deltaHedgerConfiguration;
    private readonly Timer? _hedgeTimer;
    private bool _disposed = false;

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
            _logger.LogInformation($"Account {Id} is not supported for delta hedging. Skipping timer initialization.");
        }
    }

    public string Name { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public float NetLiquidationValue { get; set; }

    public override string ToString() {
        return $"{Name} ({Id})";
    }

    public PositionsCollection Positions { get; init; }

    private void OnUnderlyingsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (e.Action == NotifyCollectionChangedAction.Add) {
            foreach (var item in e.NewItems ?? Array.Empty<object>()) {
                if (item is Position position) {

                    if (_deltaHedgers.ContainsKey(position.Contract.Id)) {
                        _logger.LogInformation($"Delta hedger already exists for contract {position.Contract} with id: {position.Contract.Id}");
                        continue;
                    }

                    if (!_deltaHedgerConfiguration.Configs.ContainsKey(position.Contract.Symbol)) {
                        _logger.LogInformation($"Delta hedger configuration not found for contract {position.Contract} with id: {position.Contract.Id}. Skipping.");
                        continue;
                    }

                    var deltaHedger = _deltaHedgerFactory.Create(_broker, Id, position, Positions, _deltaHedgerConfiguration);
                    if (_deltaHedgers.TryAdd(position.Contract.Id, deltaHedger)) {
                        _logger.LogInformation($"Delta hedger added for contract {position.Contract}");
                    }
                }
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove) {
            foreach (var item in e.OldItems ?? Array.Empty<object>()) {
                if (item is Position position) {
                    if (_deltaHedgers.TryRemove(position.Contract.Id, out var deltaHedger)) {
                        deltaHedger.Dispose();
                        _logger.LogInformation($"Delta hedger removed and disposed for contract {position.Contract}");
                    }
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
                    _logger.LogError(ex, $"Error executing delta hedger for contract {hedger.Contract}");
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
                _logger.LogError(ex, $"Error disposing delta hedger for contract {hedger.Contract}");
            }
        }
        _deltaHedgers.Clear();
        
        Positions.Underlyings.CollectionChanged -= OnUnderlyingsChanged;
        _logger.LogInformation("Account disposed and hedge timer stopped");
    }
}
