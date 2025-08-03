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
public class Account
{
    #region Fields
    
    private readonly ILogger<Account> _logger;
    private readonly IDeltaHedgerFactory _deltaHedgerFactory;
    private readonly ConcurrentDictionary<int, IDeltaHedger> _deltaHedgers = new();
    private readonly DeltaHedgerConfiguration _deltaHedgerConfiguration;

    #endregion

    [SetsRequiredMembers]
    public Account(
        ILogger<Account> logger,
        PositionsCollection positionsCollection, 
        IDeltaHedgerFactory deltaHedgerFactory,
        DeltaHedgerConfiguration deltaHedgerConfiguration) 
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Positions = positionsCollection;
        _deltaHedgerFactory = deltaHedgerFactory ?? throw new ArgumentNullException(nameof(deltaHedgerFactory));
        _deltaHedgerConfiguration = deltaHedgerConfiguration ?? throw new ArgumentNullException(nameof(deltaHedgerConfiguration));

        Positions.Underlyings.CollectionChanged += OnUnderlyingsChanged;
    }

    public required string Name { get; init; } = string.Empty;

    public required string Id { get; init; } = string.Empty;

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
                        _logger.LogInformation($"Delta hedger already exists for contract {position.Contract}");
                        continue;
                    }

                    if (!_deltaHedgerConfiguration.Configs.ContainsKey(position.Contract.Symbol)) {
                        _logger.LogInformation($"Delta hedger configuration not found for contract {position.Contract}. Skipping.");
                        continue;
                    }

                    var deltaHedger = _deltaHedgerFactory.Create(position.Contract, _deltaHedgerConfiguration);
                    if (_deltaHedgers.TryAdd(position.Contract.Id, deltaHedger)) {
                        _logger.LogInformation($"Delta hedger added for contract {position.Contract}");
                    }
                }
            }
        }
    }
}
