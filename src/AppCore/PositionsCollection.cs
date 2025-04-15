﻿using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AppCore;

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

    #endregion

    #region Properties

    public SortedList<string, (Contract Contract, Position? Position)> Underlyings { get; set; } = new();

    public Position? DefaultUnderlying { get; set; } = null;

    #endregion

    public void Reconcile(Dictionary<int, Position> positions) {
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
                    if (positionKV.Value.PositionSize == 0) {
                        _logger.LogTrace($"Skipping position {positionKV.Value.ContractDesciption} with size 0");
                        continue;
                    }
                    if (TryAdd(positionKV.Key, positionKV.Value)) {
                        _logger.LogInformation($"Added {positionKV.Value.PositionSize} position {positionKV.Value.ContractDesciption}");
                        OnPositionAdded?.Invoke(this, positionKV.Value);
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
        TryRemove(contractId, out var removedPosition);
        _logger.LogInformation($"Removed position {removedPosition!.ContractDesciption}");
    }

    private void UpdateUnderlyings() {
        Underlyings.Clear();

        foreach (var position in Values) {
            switch (position.AssetClass) {
                case AssetClass.Stock:
                    Underlyings.Add(position.UnderlyingSymbol, new() {
                        Contract = new Contract() {
                            Symbol = position.UnderlyingSymbol,
                            AssetClass = position.AssetClass,
                            ContractId = position.ContractId
                        },
                        Position = position });
                    break;
                case AssetClass.Future:
                    if (Underlyings.TryGetValue(position.UnderlyingSymbol, out var existingUnderlying)) {
                        if (existingUnderlying.Position == null) {
                            Underlyings[position.UnderlyingSymbol] = new() {
                                Contract = new Contract() {
                                    Symbol = position.UnderlyingSymbol,
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
                                Underlyings[position.UnderlyingSymbol] = new() {
                                    Contract = new Contract() {
                                        Symbol = position.UnderlyingSymbol,
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
                    if (!Underlyings.TryGetValue(position.UnderlyingSymbol, out var existingPosition2)) {
                        // Add placeholder for position
                        Underlyings.Add(position.UnderlyingSymbol, new() {
                            Contract = new Contract() {
                                Symbol = position.UnderlyingSymbol,
                                AssetClass = AssetClass.Future
                            },
                            Position = null
                        });
                    }
                    break;
            }
        }
    }
}
