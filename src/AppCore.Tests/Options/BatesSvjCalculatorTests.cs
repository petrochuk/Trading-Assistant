using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public sealed class BatesSvjCalculatorTests
{
    private BatesSvjCalculator CreateStandard(float daysLeft = 30f, float stockPrice = 100f, float strike = 100f)
    {
        return new BatesSvjCalculator
        {
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.0f,
            DaysLeft = daysLeft,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.30f,
            VolatilityMeanReversion = 4.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.7f,
            JumpIntensity = 1f,
            JumpMean = -0.10f,
            JumpVolatility = 0.15f,
            UseCosMethodForDelta = true,
        };
    }

    [TestMethod]
    public void Price_And_Delta_Increase_With_StockPrice()
    {
        var batesLow = CreateStandard(stockPrice: 90f);
        var startPrice = 4000f;
        var endPrice = 6000f;
        var steps = (endPrice - startPrice) / 200f;
        batesLow.Strike = (startPrice + endPrice) / 2f; // ATM in middle
        var bsCalc = new BlackNScholesCalculator {
            Strike = batesLow.Strike,
            DaysLeft = batesLow.DaysLeft,
            ImpliedVolatility = batesLow.CurrentVolatility,
        };

        float previousCallPrice = -1f;
        float previousCallDelta = -1f;
        for (float price = startPrice; price <= endPrice; price += steps)
        {
            bsCalc.StockPrice = batesLow.StockPrice = price;
            batesLow.CalculateAll();
            bsCalc.CalculateAll();
            Assert.IsGreaterThanOrEqualTo(previousCallPrice, batesLow.CallValue, $"Call price should increase with stock price. Stock {price}, Call {batesLow.CallValue}");
            Assert.IsGreaterThanOrEqualTo(previousCallDelta, batesLow.DeltaCall, $"Call delta should increase with stock price. Stock {price}, Delta {batesLow.DeltaCall}");
            previousCallPrice = batesLow.CallValue;
            previousCallDelta = batesLow.DeltaCall;
        }
    }

    [TestMethod]
    public void TestBasicPricing_ATM()
    {
        var bates = CreateStandard();
        bates.CalculateCallPut();
        Assert.IsGreaterThan(0, bates.CallValue, "Call value should be positive for ATM option");
        Assert.IsGreaterThan(0, bates.PutValue, "Put value should be positive for ATM option");
        float discountedStrike = bates.Strike * MathF.Exp(-bates.RiskFreeInterestRate * bates.ExpiryTime);
        float parity = bates.CallValue - bates.PutValue - (bates.StockPrice - discountedStrike);
        Assert.AreEqual(0f, parity, 0.5f, "Put-call parity should hold within tolerance");
    }

    [TestMethod]
    public void TestExpiryBehavior()
    {
        var bates = CreateStandard(daysLeft:0f);
        bates.CalculateCallPut();
        Assert.AreEqual(MathF.Max(bates.StockPrice - bates.Strike, 0f), bates.CallValue, 1e-6f);
        Assert.AreEqual(MathF.Max(bates.Strike - bates.StockPrice, 0f), bates.PutValue, 1e-6f);
    }

    [TestMethod]
    public void TestGreeks_Computed()
    {
        var bates = CreateStandard();
        bates.CalculateAll();
        Assert.IsTrue(bates.DeltaCall >= 0 && bates.DeltaCall <= 1, $"Call delta in [0,1], got {bates.DeltaCall}");
        Assert.IsTrue(bates.DeltaPut >= -1 && bates.DeltaPut <= 0, $"Put delta in [-1,0], got {bates.DeltaPut}");
        Assert.IsGreaterThan(0, bates.VegaCall, "Call vega should be positive");
        Assert.IsGreaterThan(0, bates.VegaPut, "Put vega should be positive");
    }

    [TestMethod]
    public void TestJumpIntensityImpact()
    {
        var lowJump = CreateStandard();
        lowJump.JumpIntensity = 0.0f; // Heston only
        lowJump.CalculateCallPut();
        var hestonCall = lowJump.CallValue;
        var hestonPut = lowJump.PutValue;

        var highJump = CreateStandard();
        highJump.JumpIntensity = 2.0f; // Many jumps
        highJump.CalculateCallPut();
        var jumpCall = highJump.CallValue;
        var jumpPut = highJump.PutValue;

        // Downside jumps (negative mean) typically increase put values and may reduce call slightly
        Assert.IsGreaterThan(hestonPut, jumpPut, $"Put value should increase with negative jump drift. Heston {hestonPut}, Jump {jumpPut}");
    }

    [TestMethod]
    public void TestMonteCarloVsCF()
    {
        var cf = CreateStandard(daysLeft:10f);
        cf.CalculateCallPut();
        var cfCall = cf.CallValue;

        var mc = CreateStandard(daysLeft:10f);
        mc.UseMonteCarlo = true;
        mc.MonteCarloPaths = 2000; // reduce for test speed
        mc.CalculateCallPut();
        var mcCall = mc.CallValue;

        // Expect rough agreement (within 40%) due to fewer paths
        if (cfCall > 0 && mcCall > 0)
        {
            var relErr = MathF.Abs(cfCall - mcCall) / cfCall;
            Assert.IsLessThan(0.4f, relErr, $"Monte Carlo price should roughly match CF. CF {cfCall}, MC {mcCall}, relErr {relErr}");
        }
    }
}
