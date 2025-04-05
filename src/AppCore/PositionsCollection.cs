﻿

namespace AppCore;

public class PositionsCollection : Dictionary<int, Position>
{
    public SortedList<string, AssetClass> Underlyings { get; set; } = new();

    public string DefaultUnderlying { get; set; } = string.Empty;

    public void Reconcile(Dictionary<int, Position> positions) {
        // Remove positions that are not in the new list
        foreach (var key in Keys.ToList()) {
            if (!positions.ContainsKey(key)) {
                Remove(key);
            }
        }

        // Add or update positions from the new list
        foreach (var position in positions) {
            if (TryGetValue(position.Key, out var existingPosition)) {
                existingPosition.UpdateFrom(position.Value);
            }
            else {
                Add(position.Key, position.Value);
            }
        }

        // Update the list of underlyings
        UpdateUnderlyings();
    }

    private void UpdateUnderlyings() {
        Underlyings.Clear();

        foreach (var position in Values) {
            if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Future ||
                position.AssetClass == AssetClass.Option || position.AssetClass == AssetClass.FutureOption) { 
                if (Underlyings.ContainsKey(position.UnderlyingSymbol)) {
                    continue;
                }

                if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Option) {
                    Underlyings.Add(position.UnderlyingSymbol, AssetClass.Stock);
                }
                else if (position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.FutureOption) {
                    Underlyings.Add(position.UnderlyingSymbol, AssetClass.Future);
                    DefaultUnderlying = position.UnderlyingSymbol;
                }
            }
        }
    }
}
