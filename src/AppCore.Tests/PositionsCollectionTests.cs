using AppCore.Extenstions;
using AppCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AppCore.Tests;

[TestClass]
public sealed class PositionsCollectionTests
{
    [TestMethod]
    public void TotalGreeks_Otm_Call() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider(), new ExpirationCalendar());

        var underlyingPosition = new Position(contractId: 1, underlyingSymbol: "ES", assetClass: AssetClass.Future);
        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, null, 5000, isCall: true, 50);
        position1.Size = 1;

        // 0 DTE theta = -market price
        position1.MarketPrice = 10.0f;
        position1.UpdateGreeks(delta: 0.1f, gamma: 0.0f, theta: -position1.MarketPrice, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(0.1f, greeks.Delta);
        Assert.AreEqual(-position1.MarketPrice * position1.Contract.Multiplier, greeks.Theta);
        Assert.AreEqual(-0.1f, greeks.Charm);
    }

    [TestMethod]
    public void TotalGreeks_Itm_Call() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider(), new ExpirationCalendar());

        var underlyingPosition = new Position(contractId: 1, underlyingSymbol: "ES", assetClass: AssetClass.Future);
        underlyingPosition.MarketPrice = 5050f;

        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, null, 5000, isCall: true, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.Size = 1;
        position1.UpdateGreeks(delta: 0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.Size, greeks.Delta);
        Assert.IsTrue(MathF.Abs(0.2f - greeks.Charm) < 0.00001f);
    }

    [TestMethod]
    public void TotalGreeks_Itm_Put() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider(), new ExpirationCalendar());

        var underlyingPosition = new Position(contractId: 1, underlyingSymbol: "ES", assetClass: AssetClass.Future);
        underlyingPosition.MarketPrice = 4950f;

        var position1 = new Position(1, underlyingPosition.Contract.Symbol, AssetClass.FutureOption, null, 5000, isCall: false, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.Size = 1;
        position1.UpdateGreeks(delta: -0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.SelectedPosition = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.Size, greeks.Delta);
        Assert.IsTrue(MathF.Abs(-0.2f - greeks.Charm) < 0.00001f);
    }

    [TestMethod]
    public void RiskCurve_NoPositions() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider(), new ExpirationCalendar());
        
        // Act
        var riskCurve = positions.CalculateRiskCurve("ES", TimeSpan.FromMinutes(5), 4000, 5000, 6000, 10);

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
            new ExpirationCalendar());

        var underlyingPosition = new Position(contractId: 1, underlyingSymbol: "ES", assetClass: AssetClass.Future,
            new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            multiplier: 50) {
            Size = 1,
        };
        underlyingPosition.MarketPrice = 5401.25f;
        positions.TryAdd(underlyingPosition.Contract.Id, underlyingPosition);

        var put = new Position(2, underlyingPosition.Contract.Symbol, AssetClass.FutureOption,
            new DateTimeOffset(2025, 5, 2, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            5350, isCall: false, 50);
        put.MarketPrice = 65.0f;
        put.Size = 1;
        put.UpdateGreeks(delta: -0.7f, gamma: 0.0f, theta: -put.MarketPrice, vega: 0.0f);
        positions.TryAdd(put.Contract.Id, put);

        // Act
        var riskCurve = positions.CalculateRiskCurve("ES", TimeSpan.FromMinutes(5), 5300, 5401.25f, 5500, 10);

        // Assert
        Assert.AreEqual(20, riskCurve.Points.Count);
    }
}
