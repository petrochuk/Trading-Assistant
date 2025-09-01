using AppCore.Statistics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

[DebuggerDisplay("{Symbol}")]
public class UnderlyingPosition
{
    public required string Symbol { get; init; }

    public required AssetClass AssetClass { get; init; }

    public List<Position> Positions { get; init; } = new List<Position>();

    public Contract? FrontContract { get; private set; }

    public IRealizedVolatility? RealizedVol { get; private set; }

    private Dictionary<int, Contract> _contracts = new Dictionary<int, Contract>();
    public IReadOnlyDictionary<int, Contract> Contracts {
        get => _contracts;
    }

    [SetsRequiredMembers]
    public UnderlyingPosition(string symbol, AssetClass assetClass, List<Position>? positions = null, IRealizedVolatility? realizedVolatility = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        if (assetClass != AssetClass.Stock && assetClass != AssetClass.Future)
            throw new ArgumentException($"Asset class {assetClass} is not valid for underlying position", nameof(assetClass));

        Symbol = symbol;
        AssetClass = assetClass;
        RealizedVol = realizedVolatility != null ? realizedVolatility : new RVwithSubsampling(PositionsCollection.RealizedVolPeriod, PositionsCollection.RealizedVolSamples);

        if (positions != null)
            Positions.AddRange(positions);
    }

    [SetsRequiredMembers]
    public UnderlyingPosition(string symbol, AssetClass assetClass, Position position, IRealizedVolatility? realizedVolatility = null) {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        if (assetClass != AssetClass.Stock && assetClass != AssetClass.Future)
            throw new ArgumentException($"Asset class {assetClass} is not valid for underlying position", nameof(assetClass));

        Symbol = symbol;
        AssetClass = assetClass;
        RealizedVol = realizedVolatility != null ? realizedVolatility : new RVwithSubsampling(PositionsCollection.RealizedVolPeriod, PositionsCollection.RealizedVolSamples);

        if (position != null)
            Positions.Add(position);
    }

    public void UpdateStdDev() {
        if (FrontContract == null || !FrontContract.MarketPrice.HasValue || FrontContract.MarketPrice <= 0)
            return;
        RealizedVol?.AddValue(FrontContract.MarketPrice.Value);
    }

    public void UpdateMarketPrice(int contractId, float markPrice) {
        if (Contracts.TryGetValue(contractId, out var contract)) {
            contract.MarketPrice = markPrice;
        }
    }

    public void AddContracts(List<Contract> contracts) {

        var frontMonth = DateTimeOffset.MaxValue;
        foreach (var contract in contracts) {
            _contracts.Add(contract.Id, contract);
            if (contract.Expiration!.Value < frontMonth) {
                frontMonth = contract.Expiration.Value;
                FrontContract = contract;
            }
        }
    }
}
