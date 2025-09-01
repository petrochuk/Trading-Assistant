using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

[DebuggerDisplay("{Symbol}")]
public class UnderlyingPosition
{
    public required string Symbol { get; init; }

    public required AssetClass AssetClass { get; init; }

    public List<Position> Positions { get; init; } = new List<Position>();

    public Dictionary<int, Contract> Contracts { get; init; } = new Dictionary<int, Contract>();

    [SetsRequiredMembers]
    public UnderlyingPosition(string symbol, AssetClass assetClass, List<Position>? positions = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        if (assetClass != AssetClass.Stock && assetClass != AssetClass.Future)
            throw new ArgumentException($"Asset class {assetClass} is not valid for underlying position", nameof(assetClass));

        Symbol = symbol;
        AssetClass = assetClass;

        if (positions != null)
            Positions.AddRange(positions);
    }

    [SetsRequiredMembers]
    public UnderlyingPosition(string symbol, AssetClass assetClass, Position position) {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        if (assetClass != AssetClass.Stock && assetClass != AssetClass.Future)
            throw new ArgumentException($"Asset class {assetClass} is not valid for underlying position", nameof(assetClass));

        Symbol = symbol;
        AssetClass = assetClass;

        if (position != null)
            Positions.Add(position);
    }

    public void UpdateStdDev() {
        foreach (var position in Positions) {
            position.UpdateStdDev();
        }
    }

    public void UpdateMarketPrice(int contractId, float markPrice) {
        if (Contracts.TryGetValue(contractId, out var contract)) {
            contract.MarketPrice = markPrice;
        }
    }
}
