using AppCore.Models;
using AppCore.Statistics;
using AppCore.Tests.Fakes;

namespace AppCore.Tests;

internal class TestHelpers
{
    public static UnderlyingPosition CreateUnderlyingPosition(
        string symbol,
        TestContractFactory testContractFactory, TimeProvider timeProvider,
        DateTimeOffset expiration,
        float marketPrice, int size = 1, IRealizedVolatility? realizedVol = null) {
        var underlyingPosition = new UnderlyingPosition(symbol, AssetClass.Future, realizedVolatility: realizedVol);

        var undContract = testContractFactory.Create(
            symbol, AssetClass.Future, testContractFactory.NextContractId, expiration: expiration);
        undContract.MarketPrice = marketPrice;

        underlyingPosition.AddContracts(new List<Contract> { undContract }, timeProvider);

        return underlyingPosition;
    }

    public static Position CreatePosition(
        TestContractFactory testContractFactory, TimeProvider timeProvider,
        AssetClass assetClass, int contractId,
        int underlyingContractId, float marketPrice,
        DateTimeOffset expiration,
        float? strike = null, bool? isCall = null,
        int size = 1)
    {
        var contract = testContractFactory.Create(
            "ES", assetClass, contractId,
            underlyingContractId: underlyingContractId, expiration: expiration, strike: strike, isCall: isCall);
        contract.MarketPrice = marketPrice;
        var position = new Position(contract) { 
            UnderlyingContractId = underlyingContractId,
            Size = size
        };

        return position;
    }
}
