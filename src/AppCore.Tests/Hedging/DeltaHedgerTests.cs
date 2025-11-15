using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Hedging;
using AppCore.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AppCore.Tests.Hedging;

[TestClass]
public sealed class DeltaHedgerTests
{
    [TestMethod]
    [DataRow(5300.0f, true, -5, 1, 1f, -1)]
    [DataRow(5300.0f, true, 5, 1, 1f, -1)]
    [DataRow(5300.0f, true, 5, -1, 1f, 1)]
    [DataRow(5005.0f, true, 5, 1, 1f, -3)]
    [DataRow(5005.0f, true, 1, 1, 1f, -1)]
    [DataRow(5005.0f, true, -1, 1, 1f, 0)]
    [DataRow(4995.0f, false, 1, 1, 1f, 0)]
    [DataRow(4995.0f, false, 2, 1, 1f, 0)]
    // In-the-money tests
    [DataRow(4850.0f, true, 1, 0, 1f, -1)]
    [DataRow(4900.0f, true, -1, 1, 1f, 0)]
    [DataRow(4900.0f, true, -10, 1, 1f, 6)]
    [DataRow(4900.0f, true, -2, 1, 1f, 0)]
    [DataRow(4900.0f, true, -3, 1, 1f, 1)]
    [DataRow(4900.0f, true, 5, 1, 1f, -4)]
    public void DeltaHedger_DeltaHedge(float strike, bool isCall, float optionSize, int positionSize, float delta, int expectedHedgeSize)
    {
        // Arrange
        var contractFactory = new TestContractFactory();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 1, 18, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset));
        var positions = new PositionsCollection(NullLogger<PositionsCollection>.Instance, timeProvider, new ExpirationCalendar(), contractFactory);
        var testRealizedVol = new TestRealizedVol { TestValue = 0.2 };

        var esMarketPrice = 5000.0f;
        var underlyingPosition = TestHelpers.CreateUnderlyingPosition("ES", contractFactory, timeProvider,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            esMarketPrice, realizedVol: testRealizedVol);

        // Add 1 ES future position
        var esPosition = TestHelpers.CreatePosition(contractFactory, timeProvider, AssetClass.Future,
            underlyingPosition.FrontContract!.Id, underlyingPosition.FrontContract!.Id, esMarketPrice,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            size: positionSize);
        if (positionSize != 0)
            positions.TryAdd(esPosition.Contract.Id, esPosition);

        // Option position
        var optionPosition = TestHelpers.CreatePosition(contractFactory, timeProvider, AssetClass.FutureOption,
            contractFactory.NextContractId, underlyingPosition.FrontContract!.Id, 0,
            new DateTimeOffset(2025, 6, 20, 9, 30, 0, TimeExtensions.EasternStandardTimeZone.BaseUtcOffset),
            strike, isCall, size: (int)optionSize);
        optionPosition.Underlying = underlyingPosition;
        positions.TryAdd(optionPosition.Contract.Id, optionPosition);

        var deltaHedgerFactory = new DeltaHedgerFactory(NullLogger<DeltaHedger>.Instance, timeProvider, null);
        var deltaHedgerConfiguration = new DeltaHedgerConfiguration {
            Configs = new ()
            {
                { "ES", new () { 
                        Delta = delta,
                        MinDeltaAdjustment = 1f,
                    } 
                }
            }
        };

        // Act
        var testBroker = new TestBroker();
        using var deltaHedger = deltaHedgerFactory.Create(testBroker, "FakeAccountId", underlyingPosition, positions, deltaHedgerConfiguration);
        deltaHedger.Hedge();

        // Assert
        if (expectedHedgeSize == 0) {
            Assert.HasCount(0, testBroker.PlacedOrders, $"Expected no orders to be placed for delta hedging.");
            return; // No orders expected, nothing to assert
        } 

        Assert.HasCount(1, testBroker.PlacedOrders, $"Expected 1 order to be placed for delta hedging.");
        var order = testBroker.PlacedOrders[0];
        Assert.AreEqual(underlyingPosition.Symbol, order.Contract.Symbol, "Order should be for the underlying contract.");
        Assert.AreEqual(expectedHedgeSize, order.TotalSize, "Order quantity should match the delta hedge requirement.");
    }
}
