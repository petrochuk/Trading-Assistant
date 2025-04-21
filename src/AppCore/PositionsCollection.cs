using AppCore.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("c:{Count}")]
public class PositionsCollection : ConcurrentDictionary<int, Position>
{
    #region Fields

    private readonly ILogger<PositionsCollection> _logger;
    private readonly Lock _lock = new();

    #endregion

    #region Constructors

    public PositionsCollection(ILogger<PositionsCollection> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #endregion

    #region Events

    public event EventHandler<Position>? OnPositionAdded;
    public event EventHandler<Position>? OnPositionRemoved;

    #endregion

    #region Properties

    public SortedList<string, (Contract Contract, Position? Position)> Underlyings { get; set; } = new();

    public Position? DefaultUnderlying { get; set; } = null;

    #endregion

    #region Methods

    public void Reconcile(Dictionary<int, IPosition> positions) {
        lock (_lock) { 
            // Remove positions that are not in the new list
            foreach (var contractId in Keys.ToList()) {
                if (!positions.ContainsKey(contractId)) {
                    if (contractId != DefaultUnderlying?.ContractId) {
                        RemovePosition(contractId);
                    }
                }
            }

            // Add or update positions from the new list
            foreach (var positionKV in positions) {
                if (TryGetValue(positionKV.Key, out var existingPosition)) {
                    existingPosition.UpdateFrom(positionKV.Value);
                }
                else {
                    if (positionKV.Value.Size == 0) {
                        _logger.LogTrace($"Skipping position {positionKV.Value.ContractDesciption} with size 0");
                        continue;
                    }
                    var position = new Position(positionKV.Value);
                    if (TryAdd(positionKV.Key, position)) {
                        _logger.LogInformation($"Added {position.Size} position {position.ContractDesciption}");
                        OnPositionAdded?.Invoke(this, position);
                    }
                }
            }

            // Update the list of underlyings
            UpdateUnderlyings();
        }
    }

    public Position? AddPosition(Contract contract) {
        lock (_lock) {
            if (ContainsKey(contract.ContractId)) {
                _logger.LogTrace($"Position {contract} already exists");
                return null;
            }
            var position = new Position(contract);
            TryAdd(contract.ContractId, position);
            _logger.LogInformation($"Added empty position for {contract}");
            DefaultUnderlying = position;

            return position;
        }
    }

    private void RemovePosition(int contractId) {
        if (TryRemove(contractId, out var removedPosition)) {
            _logger.LogInformation($"Removed position {removedPosition!.ContractDesciption}");
            OnPositionRemoved?.Invoke(this, removedPosition);
        }
    }

    private void UpdateUnderlyings() {
        Underlyings.Clear();

        foreach (var position in Values) {
            switch (position.AssetClass) {
                case AssetClass.Stock:
                    Underlyings.Add(position.Symbol, new() {
                        Contract = new Contract() {
                            Symbol = position.Symbol,
                            AssetClass = position.AssetClass,
                            ContractId = position.ContractId
                        },
                        Position = position });
                    break;
                case AssetClass.Future:
                    if (Underlyings.TryGetValue(position.Symbol, out var existingUnderlying)) {
                        if (existingUnderlying.Position == null) {
                            Underlyings[position.Symbol] = new() {
                                Contract = new Contract() {
                                    Symbol = position.Symbol,
                                    AssetClass = position.AssetClass,
                                    ContractId = position.ContractId,
                                    Expiration = position.Expiration
                                },
                                Position = position
                            };
                        }
                        else {
                            // Replace with front month future
                            if (position.Expiration < existingUnderlying.Contract.Expiration) {
                                Underlyings[position.Symbol] = new() {
                                    Contract = new Contract() {
                                        Symbol = position.Symbol,
                                        AssetClass = position.AssetClass,
                                        ContractId = position.ContractId,
                                        Expiration = position.Expiration
                                    },
                                    Position = position
                                };
                            }
                        }
                        DefaultUnderlying = position;
                    }
                    break;
                case AssetClass.FutureOption:
                    if (!Underlyings.TryGetValue(position.Symbol, out var existingPosition2)) {
                        // Add placeholder for position
                        Underlyings.Add(position.Symbol, new() {
                            Contract = new Contract() {
                                Symbol = position.Symbol,
                                AssetClass = AssetClass.Future
                            },
                            Position = null
                        });
                    }
                    break;
            }
        }
    }

    public Greeks CalculateGreeks() {
        var greeks = new Greeks();

        lock (_lock) {
            foreach (var position in Values) {
                if (position.Symbol != DefaultUnderlying?.Symbol) {
                    continue;
                }
                if (position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.Stock) {
                    greeks.Delta += position.Size;
                }
                else if (position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option) {
                    if (position.Delta.HasValue) {
                        greeks.Delta += position.Delta.Value * position.Size;
                    }
                    if (position.Delta.HasValue && position.Theta.HasValue && position.MarketPrice != 0) {
                        var absTheta = MathF.Abs(position.Theta.Value);
                        if (position.MarketPrice < absTheta)
                            absTheta = position.MarketPrice;
                        if (-0.5f < position.Delta.Value && position.Delta.Value < 0.5f)
                            greeks.Charm -= position.Delta.Value * (absTheta / position.MarketPrice) * position.Size;
                        else {
                            var intrinsicValue = position.IsCall ? DefaultUnderlying.MarketPrice - position.Strike : position.Strike - DefaultUnderlying.MarketPrice;
                            if (intrinsicValue < 0)
                                intrinsicValue = 0;
                            var extrinsicValue = position.MarketPrice - intrinsicValue;
                            if (extrinsicValue < 0)
                                extrinsicValue = 0;
                            if (extrinsicValue < absTheta)
                                absTheta = extrinsicValue;

                            greeks.Charm += ((position.IsCall ? 1f : -1f) - position.Delta.Value) * (absTheta / extrinsicValue) * position.Size;
                        }
                    }
                    if (position.Gamma.HasValue) {
                        greeks.Gamma += position.Gamma.Value * position.Size;
                    }
                    if (position.Theta.HasValue) {
                        greeks.Theta += position.Theta.Value * position.Size * position.Multiplier;
                    }
                    if (position.Vega.HasValue) {
                        greeks.Vega += position.Vega.Value * position.Size;
                    }
                }
            }
        }

        return greeks;
    }
    #endregion
}
