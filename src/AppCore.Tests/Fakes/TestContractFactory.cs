using AppCore.Interfaces;
using AppCore.Models;

namespace AppCore.Tests.Fakes;

internal class TestContractFactory : IContractFactory
{
    private int _nextContractId = 1;

    public int NextContractId => _nextContractId++;

    public Contract Create(IPosition position) {
        throw new NotImplementedException();
    }

    public Contract Create(string symbol, 
        AssetClass assetClass, int contractId, int? underlyingContractId = null, 
        DateTimeOffset? expiration = null, float? strike = null, bool? isCall = null) {

        var contract = new Contract {
            Id = contractId,
            UnderlyingContractId = underlyingContractId,
            Symbol = symbol,
            AssetClass = assetClass,
            Multiplier = symbol switch {
                "ES" => 50,
                "NQ" => 20,
                "MES" => 5,
                "ZN" => 1000,
                _ => 1
            },
            Strike = strike ?? 0,
            IsCall = isCall ?? false,
            Expiration = expiration
        };

        return contract;
    }
}
