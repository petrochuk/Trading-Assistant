using AppCore.Models;

namespace AppCore.Interfaces;

public interface IContractFactory
{
    Contract Create(IPosition position);

    Contract Create(string symbol, AssetClass assetClass, int contractId, int? underlyingContractId = null, DateTimeOffset? expiration = null);
}
