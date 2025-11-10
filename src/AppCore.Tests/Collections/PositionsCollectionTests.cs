using AppCore.Extenstions;
using AppCore.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AppCore.Tests.Collections;

[TestClass]
public class PositionsCollectionTests
{
    [TestMethod]
    [DataRow(5150.0f, true, 5)]
    public void Calculate_Greeks(float strike, bool isCall, float optionSize) {
        // Arrange
        var contractFactory = new TestContractFactory();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, timeProvider, new ExpirationCalendar(), contractFactory);
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var esMarketPrice = 5000.0f;
        var underlyingPosition = TestHelpers.CreateUnderlyingPosition("ES", contractFactory, timeProvider,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            esMarketPrice, realizedVol: testRealizedVol);
        underlyingPosition.FrontContract!.VolatilityMeanReversion = 20;
        underlyingPosition.FrontContract!.VolatilityOfVolatility = 2.0f;
        underlyingPosition.FrontContract!.LongTermVolatility = 0.22f;
        underlyingPosition.FrontContract!.Correlation = -0.6f;

        // Add 1 ES future position
        var esPosition = TestHelpers.CreatePosition(contractFactory, timeProvider, AssetClass.Future,
            underlyingPosition.FrontContract!.Id, underlyingPosition.FrontContract!.Id, esMarketPrice,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            size: 1);
        positions.TryAdd(esPosition.Contract.Id, esPosition);

        // Option position
        var optionPosition = TestHelpers.CreatePosition(contractFactory, timeProvider, AssetClass.FutureOption,
            contractFactory.NextContractId, underlyingPosition.FrontContract!.Id, 0,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            strike, isCall, size: (int)optionSize);
        optionPosition.Underlying = underlyingPosition;
        optionPosition.Contract.MarketPrice = 24.791f;
        positions.TryAdd(optionPosition.Contract.Id, optionPosition);

        // Act
        var greeks = positions.CalculateGreeks(minIV: 0, underlyingPosition);

        Assert.IsNotNull(greeks);
        Assert.AreEqual(0.1989f, greeks.VarianceWeightedIV, 0.01f);
    }
}
