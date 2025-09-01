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

    private Dictionary<int, Contract> _contractsById = new Dictionary<int, Contract>();
    private SortedList<DateTimeOffset, Contract> _contractsByExpiration = new SortedList<DateTimeOffset, Contract>();

    public IReadOnlyDictionary<int, Contract> ContractsById {
        get => _contractsById;
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
        if (ContractsById.TryGetValue(contractId, out var contract)) {
            contract.MarketPrice = markPrice;
        }
    }

    public void AddContracts(List<Contract> contracts) {
        if (AssetClass == AssetClass.Stock) { 
            if (contracts.Count != 1 || contracts[0].Symbol != Symbol || contracts[0].AssetClass != AssetClass.Stock) {
                throw new ArgumentException("Invalid contract for stock underlying position", nameof(contracts));
            }

            if (_contractsById.ContainsKey(contracts[0].Id))
                return;
            _contractsById[contracts[0].Id] = contracts[0];
            FrontContract = contracts[0];

            return;
        }

        var frontMonth = DateTimeOffset.MaxValue;
        foreach (var contract in contracts) {
            if (_contractsById.ContainsKey(contract.Id))
                continue;
            _contractsById.Add(contract.Id, contract);
            _contractsByExpiration.Add(contract.Expiration!.Value, contract);
            if (contract.Expiration!.Value < frontMonth) {
                frontMonth = contract.Expiration.Value;
                FrontContract = contract;
            }
        }
    }

    /// <summary>
    /// Finds contract ID by expiration date.
    /// </summary>
    /// <param name="expiration"></param>
    /// <returns></returns>
    public bool FindContractId(DateTimeOffset expiration, out int contractId) {
        contractId = 0;
        for (var idx = 0; idx < _contractsByExpiration.Count; idx++) {
            if (_contractsByExpiration.Keys[idx] < expiration) {
                continue;
            }

            contractId = _contractsByExpiration.Values[idx].Id;
            return true;
        }

        return false;
    }
}
