using AppCore.Extenstions;
using AppCore.Models;
using AppCore.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AppCore.Tests;

[TestClass]
public sealed class PositionsCollectionTests
{
    private readonly Contract _esContract = new Contract {
        Symbol = "ES",
        AssetClass = AssetClass.Future,
        Id = 1,
        Multiplier = 50,
        Expiration = new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
    };

    [TestMethod]
    public void TotalGreeks_Otm_Call() {
        /*
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, time, new ExpirationCalendar());
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var underlyingPosition = new Position(_esContract, testRealizedVol);
        underlyingPosition.Contract.MarketPrice = 4950f; // Underlying price below strike

        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, 
            expiration: new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            5000, isCall: true, 50);
        position1.Size = 1;

        // 0 DTE theta = -market price
        position1.Contract.MarketPrice = 10.0f;

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();
        Assert.IsNotNull(greeks, "Greeks should not be null");

        // Assert
        Assert.IsLessThan(0.5, greeks.Value.Delta, "out of the money call");
        Assert.IsGreaterThan(0, greeks.Value.Delta, "out of the money call has positive delta");
        Assert.IsLessThan(0, greeks.Value.Charm, "out of the money call has negative charm");
        */
    }

    [TestMethod]
    public void TotalGreeks_Itm_Call() {
        /*
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, time, new ExpirationCalendar());
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var underlyingPosition = new Position(_esContract, testRealizedVol);
        underlyingPosition.Contract.MarketPrice = 5050f;

        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, 
            expiration: new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset), 
            5000, isCall: true, multiplier: 50);

        // 0 DTE theta = -market price
        position1.Contract.MarketPrice = 60.0f;
        position1.Size = 1;

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();
        Assert.IsNotNull(greeks, "Greeks should not be null");

        // Assert
        Assert.IsGreaterThan(0.5, greeks.Value.Delta, "in the money call");
        Assert.IsGreaterThan(0, greeks.Value.Charm, "in the money call has positive charm");
        */
    }

    [TestMethod]
    public void TotalGreeks_Itm_Put() {
        /*
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, time, new ExpirationCalendar());
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var underlyingPosition = new Position(contractId: 1, underlyingSymbol: "ES", assetClass: AssetClass.Future, 
            realizedVolatility: testRealizedVol);
        underlyingPosition.Contract.MarketPrice = 4950f;

        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, 
            expiration: new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            5000, isCall: false, 50);

        // 0 DTE theta = -market price
        position1.Contract.MarketPrice = 60.0f;
        position1.Size = 1;

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();
        Assert.IsNotNull(greeks, "Greeks should not be null");

        // Assert
        Assert.IsLessThan(-0.5, greeks.Value.Delta, "in the money put");
        Assert.IsLessThan(0, greeks.Value.Charm, "in the money put has negative charm");
        */
    }

    [TestMethod]
    public void RiskCurve_NoPositions() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider(), new ExpirationCalendar(), new TestContractFactory());
        
        // Act
        var riskCurve = positions.CalculateRiskCurve("ES", TimeSpan.FromMinutes(5), 4000, 5000, 6000, 10);
        Assert.IsNotNull(riskCurve, "Risk curve should not be null");

        // Assert
        Assert.AreEqual(200, riskCurve.Points.Count);
        // All points should be 0
        foreach (var point in riskCurve.Points) {
            Assert.AreEqual(0, point.Value);
        }
    }

    [TestMethod]
    public void RiskCurve_OneHedgedPosition() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, 
            new FakeTimeProvider(new DateTimeOffset(2025, 4, 23, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)),
            new ExpirationCalendar(), new TestContractFactory());

        var contract1 = new Contract {
            Symbol = "ES",
            AssetClass = AssetClass.Future,
            Id = 1,
            Multiplier = 50,
            Expiration = new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
            };
        var underlyingPosition = new Position(contract1);
        underlyingPosition.Contract.MarketPrice = 5401.25f;
        positions.TryAdd(underlyingPosition.Contract.Id, underlyingPosition);

        var contract2 = new Contract {
            Symbol = "ES",
            AssetClass = AssetClass.FutureOption,
            Id = 2,
            Multiplier = 50,
            Expiration = new DateTimeOffset(2025, 5, 2, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            IsCall = false,
            Strike = 5350,
        };
        var put = new Position(contract2);
        put.Contract.MarketPrice = 65.0f;
        put.Size = 1;
        put.UpdateGreeks(delta: -0.7f, gamma: 0.0f, theta: -put.Contract.MarketPrice, vega: 0.0f);
        positions.TryAdd(put.Contract.Id, put);

        // Act
        var riskCurve = positions.CalculateRiskCurve("ES", TimeSpan.FromMinutes(5), 5300, 5401.25f, 5500, 10);
        Assert.IsNotNull(riskCurve, "Risk curve should not be null");

        // Assert
        Assert.AreEqual(20, riskCurve.Points.Count);
    }
}
