using Microsoft.Extensions.Logging.Abstractions;

namespace AppCore.Tests;

[TestClass]
public sealed class PositionsCollectionTests
{
    [TestMethod]
    public void TotalGreeks_Otm_Call() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance);

        var underlyingPosition = new Position {
            ContractId = 1,
            PositionSize = 0,
            UnderlyingSymbol = "ES",
            AssetClass = AssetClass.Future
        };
        var position1 = new Position(underlyingPosition.UnderlyingSymbol, AssetClass.FutureOption, isCall: true, 5000, 50);
        position1.PositionSize = 1;

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
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance);

        var underlyingPosition = new Position {
            ContractId = 1,
            PositionSize = 0,
            UnderlyingSymbol = "ES",
            AssetClass = AssetClass.Future
        };
        underlyingPosition.MarketPrice = 5050f;

        var position1 = new Position(underlyingPosition.UnderlyingSymbol, AssetClass.FutureOption, isCall: true, 5000, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.PositionSize = 1;
        position1.UpdateGreeks(delta: 0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.DefaultUnderlying = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.PositionSize, greeks.Delta);
        Assert.IsTrue(MathF.Abs(0.2f - greeks.Charm) < 0.00001f);
    }


    [TestMethod]
    public void TotalGreeks_Itm_Put() {
        // Arrange
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance);

        var underlyingPosition = new Position {
            ContractId = 1,
            PositionSize = 0,
            UnderlyingSymbol = "ES",
            AssetClass = AssetClass.Future
        };
        underlyingPosition.MarketPrice = 4950f;

        var position1 = new Position(underlyingPosition.UnderlyingSymbol, AssetClass.FutureOption, isCall: false, 5000, 50);

        // 0 DTE theta = -market price
        position1.MarketPrice = 60.0f;
        position1.PositionSize = 1;
        position1.UpdateGreeks(delta: -0.8f, gamma: 0.0f, theta: -10f, vega: 0.0f);

        positions.TryAdd(1, position1);
        positions.DefaultUnderlying = underlyingPosition;

        // Act
        var greeks = positions.CalculateGreeks();

        // Assert
        Assert.AreEqual(position1.Delta * position1.PositionSize, greeks.Delta);
        Assert.IsTrue(MathF.Abs(-0.2f - greeks.Charm) < 0.00001f);
    }
}
