using AppCore.Extenstions;
using AppCore.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Generic;

namespace AppCore.Tests;

[TestClass]
public sealed class PositionsCollectionTests
{
    [TestMethod]
    public void TotalGreeks_Otm_Call() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider());

        var underlyingPosition = new Position {
            ContractId = 1,
            Size = 0,
            Symbol = "ES",
            AssetClass = AssetClass.Future
        };
        var position1 = new Position(1, underlyingPosition.Symbol, AssetClass.FutureOption, isCall: true, 5000, 50);
        position1.Size = 1;

        // 0 DTE theta = -market price
        position1.MarketPrice = 10.0f;
        position1.UpdateGreeks(delta: 0.1f, gamma: 0.0f, theta: -position1.MarketPrice, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.DefaultUnderlying = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(0.1f, greeks.Delta);
        Assert.AreEqual(-position1.MarketPrice * position1.Multiplier, greeks.Theta);
        Assert.AreEqual(-0.1f, greeks.Charm);
    }

    [TestMethod]
    public void TotalGreeks_Itm_Call() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider());

        var underlyingPosition = new Position {
            ContractId = 1,
            Size = 0,
            Symbol = "ES",
            AssetClass = AssetClass.Future
        };
        underlyingPosition.MarketPrice = 5050f;

        var position1 = new Position(1, underlyingPosition.Symbol, AssetClass.FutureOption, isCall: true, 5000, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.Size = 1;
        position1.UpdateGreeks(delta: 0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.DefaultUnderlying = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.Size, greeks.Delta);
        Assert.IsTrue(MathF.Abs(0.2f - greeks.Charm) < 0.00001f);
    }

    [TestMethod]
    public void TotalGreeks_Itm_Put() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider());

        var underlyingPosition = new Position {
            ContractId = 1,
            Size = 0,
            Symbol = "ES",
            AssetClass = AssetClass.Future
        };
        underlyingPosition.MarketPrice = 4950f;

        var position1 = new Position(1, underlyingPosition.Symbol, AssetClass.FutureOption, isCall: false, 5000, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.Size = 1;
        position1.UpdateGreeks(delta: -0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.DefaultUnderlying = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.Size, greeks.Delta);
        Assert.IsTrue(MathF.Abs(-0.2f - greeks.Charm) < 0.00001f);
    }

    [TestMethod]
    public void RiskCurve_NoPositions() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, new FakeTimeProvider());
        
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
            new FakeTimeProvider(new DateTimeOffset(2025, 4, 23, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)));
        
        var underlyingPosition = new Position {
            ContractId = 1, Size = 1, Symbol = "ES", Multiplier = 50,
            AssetClass = AssetClass.Future
        };
        underlyingPosition.MarketPrice = 5401.25f;
        positions.TryAdd(underlyingPosition.ContractId, underlyingPosition);

        var put = new Position(2, underlyingPosition.Symbol, AssetClass.FutureOption, isCall: false, 5350, 50)
        {
            Expiration = new DateTimeOffset(2025, 5, 2, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
        };
        put.MarketPrice = 65.0f;
        put.Size = 1;
        put.UpdateGreeks(delta: -0.7f, gamma: 0.0f, theta: -put.MarketPrice, vega: 0.0f);
        positions.TryAdd(put.ContractId, put);

        // Act
        var riskCurve = positions.CalculateRiskCurve("ES", TimeSpan.FromMinutes(5), 5300, 5401.25f, 5500, 10);

        // Assert
        Assert.AreEqual(200, riskCurve.Points.Count);
    }
}
