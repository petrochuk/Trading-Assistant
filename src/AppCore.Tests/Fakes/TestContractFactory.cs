using AppCore.Interfaces;
using AppCore.Models;

namespace AppCore.Tests.Fakes;

internal class TestContractFactory : IContractFactory
{
    public Contract Create(IPosition position) {
        throw new NotImplementedException();
    }

    public Contract Create(string symbol, AssetClass assetClass, int contractId, int? underlyingContractId = null, DateTimeOffset? expiration = null) {
        throw new NotImplementedException();
    }
}
