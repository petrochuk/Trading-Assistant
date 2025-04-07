

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AppCore;

public class PositionsCollection : ConcurrentDictionary<int, Position>
{
    private readonly ILogger<PositionsCollection> _logger;

    public PositionsCollection(ILogger<PositionsCollection> logger) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SortedList<string, AssetClass> Underlyings { get; set; } = new();

    public Position? DefaultUnderlying { get; set; } = null;

    public void Reconcile(Dictionary<int, Position> positions) {
        // Remove positions that are not in the new list
        foreach (var contractId in Keys.ToList()) {
            if (!positions.ContainsKey(contractId)) {
                RemovePosition(contractId);
            }
        }

        // Add or update positions from the new list
        foreach (var positionKV in positions) {
            if (TryGetValue(positionKV.Key, out var existingPosition)) {
                if (positionKV.Value.PositionSize == 0) {
                    RemovePosition(positionKV.Key);
                    continue;
                }
                else
                    existingPosition.UpdateFrom(positionKV.Value);
            }
            else {
                if (positionKV.Value.PositionSize == 0) {
                    _logger.LogTrace($"Skipping position {positionKV.Value.ContractDesciption} with size 0");
                    continue;
                }
                TryAdd(positionKV.Key, positionKV.Value);
                _logger.LogInformation($"Added {positionKV.Value.PositionSize} position {positionKV.Value.ContractDesciption}");
            }
        }

        // Update the list of underlyings
        UpdateUnderlyings();
    }

    private void RemovePosition(int contractId) {
        TryRemove(contractId, out var removedPosition);
        _logger.LogInformation($"Removed position {removedPosition!.ContractDesciption}");
    }

    private void UpdateUnderlyings() {
        Underlyings.Clear();

        foreach (var position in Values) {
            if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Future ||
                position.AssetClass == AssetClass.Option || position.AssetClass == AssetClass.FutureOption) {
                if (position.AssetClass == AssetClass.Future) {
                    DefaultUnderlying = position;
                }
                if (Underlyings.ContainsKey(position.UnderlyingSymbol)) {
                    continue;
                }

                if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Option) {
                    Underlyings.Add(position.UnderlyingSymbol, AssetClass.Stock);
                }
                else if (position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.FutureOption) {
                    Underlyings.Add(position.UnderlyingSymbol, AssetClass.Future);
                }
            }
        }
    }
}
