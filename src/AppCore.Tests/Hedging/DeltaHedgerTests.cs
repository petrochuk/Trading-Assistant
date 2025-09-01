using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Hedging;
using AppCore.Models;
using AppCore.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AppCore.Tests.Hedging;

[TestClass]
public sealed class DeltaHedgerTests
{
    [TestMethod]
    [DataRow(5000.0f, 05, 96f, 1, -1)]
    [DataRow(5000.0f, 10, 96f, 1, -3)]
    [DataRow(4900.0f, 05, 51f, 0, 0)]
    [DataRow(4900.0f, 10, 51f, 1, -2)]
    public void DeltaHedger_DeltaHedgeCalls(float esMarketPrice, float callSize, float callOptionMarketPrice, 
        int expectedHedgeOrderCount, int expectedHedgeSize)
    {
        /*
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, time, new ExpirationCalendar());
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var underlyingPosition = new UnderlyingPosition("ES", AssetClass.Future);
        //underlyingPosition.Contract.MarketPrice = esMarketPrice;
        //underlyingPosition.Size = 1;

        // Add 1 ES future position
        //positions.TryAdd(underlyingPosition.Contract.Id, underlyingPosition);

        // Add ES OTM call option positions which should be delta hedged
        var callOptionPosition = new Position(new Contract {
            Symbol = "ES",
            AssetClass = AssetClass.Option,
            Id = 2,
            Multiplier = 50,
            IsCall = true,
            Strike = 5010f,
            Expiration = new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
        }, testRealizedVol);
        callOptionPosition.Contract.MarketPrice = callOptionMarketPrice;
        callOptionPosition.Size = callSize;
        positions.TryAdd(callOptionPosition.Contract.Id, callOptionPosition);

        var deltaHedgerFactory = new DeltaHedgerFactory(NullLogger<DeltaHedger>.Instance, time);
        var deltaHedgerConfiguration = new DeltaHedgerConfiguration {
            Configs = new ()
            {
                { "ES", new () { Delta = 2.0f } }
            }
        };
        var testBroker = new TestBroker();
        using var deltaHedger = deltaHedgerFactory.Create(testBroker, "FakeAccountId", underlyingPosition, positions, deltaHedgerConfiguration);

        // Act
        deltaHedger.Hedge();

        // Assert
        Assert.AreEqual(expectedHedgeOrderCount, testBroker.PlacedOrders.Count, $"Expected {expectedHedgeOrderCount} order to be placed for delta hedging.");

        if (0 == expectedHedgeOrderCount) {
            return; // No orders expected, nothing to assert
        }

        var order = testBroker.PlacedOrders[0];
        Assert.AreEqual(underlyingPosition.Symbol, order.Contract.Symbol, "Order should be for the underlying contract.");
        Assert.AreEqual(expectedHedgeSize, order.Size, "Order quantity should match the delta hedge requirement.");
        */
    }

    [TestMethod]
    [DataRow(5000.0f, 05, 92f, 1, 1)]
    [DataRow(5000.0f, 10, 92f, 1, 3)]
    [DataRow(5100.0f, 05, 52f, 0, 0)]
    public void DeltaHedger_DeltaHedgePuts(float esMarketPrice, float putSize, float putOptionMarketPrice,
        int expectedHedgeOrderCount, int expectedHedgeSize)
    {
        /*
        // Arrange
        var time = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, time, new ExpirationCalendar());
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var underlyingPosition = new UnderlyingPosition("ES", AssetClass.Future);
//            Expiration = new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
        //underlyingPosition.Contract.MarketPrice = esMarketPrice;
        //underlyingPosition.Size = -1; // Short position
        // Add 1 ES future position
        //positions.TryAdd(underlyingPosition.Contract.Id, underlyingPosition);
        // Add 5 ES OTM put option positions which should be delta hedged
        var putOptionPosition = new Position(new Contract {
            Symbol = "ES",
            AssetClass = AssetClass.Option,
            Id = 2,
            Multiplier = 50,
            IsCall = false,
            Strike = 4990f,
            Expiration = new DateTimeOffset(2025, 6, 20, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset)
        }, testRealizedVol);
        putOptionPosition.Contract.MarketPrice = putOptionMarketPrice;
        putOptionPosition.Size = putSize;
        positions.TryAdd(putOptionPosition.Contract.Id, putOptionPosition);
        var deltaHedgerFactory = new DeltaHedgerFactory(NullLogger<DeltaHedger>.Instance, time);
        var deltaHedgerConfiguration = new DeltaHedgerConfiguration {
            Configs = new ()
            {
                { "ES", new () { Delta = 2.0f } }
            }
        };
        var testBroker = new TestBroker();
        using var deltaHedger = deltaHedgerFactory.Create(testBroker, "FakeAccountId", underlyingPosition, positions, deltaHedgerConfiguration);

        // Act
        deltaHedger.Hedge();

        // Assert
        Assert.AreEqual(expectedHedgeOrderCount, testBroker.PlacedOrders.Count, $"Expected {expectedHedgeOrderCount} order to be placed for delta hedging.");
       
        if (0 == expectedHedgeOrderCount) {
            return; // No orders expected, nothing to assert
        }

        var order = testBroker.PlacedOrders[0];
        Assert.AreEqual(underlyingPosition.Symbol, order.Contract.Symbol, "Order should be for the underlying contract.");
        Assert.AreEqual(expectedHedgeSize, order.Size, "Order quantity should match the delta hedge requirement.");
        */
    }
}
