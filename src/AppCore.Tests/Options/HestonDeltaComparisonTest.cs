using AppCore.Options;
using System.Diagnostics;

namespace AppCore.Tests.Options;

/// <summary>
/// Tests demonstrating the analytical delta calculation improvement over finite differences
/// </summary>
[TestClass]
public class HestonDeltaComparisonTests
{
    [TestMethod]
    public void TestHeston_AnalyticalDelta_ATM()
    {
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 100f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.7f
        };

        heston.CalculateAll();

        // For ATM options, call delta should be around 0.5
        Assert.IsTrue(heston.DeltaCall > 0.4f && heston.DeltaCall < 0.6f, 
            $"ATM call delta should be near 0.5, got {heston.DeltaCall}");
        
        // Put delta should be call delta - 1
        Assert.AreEqual(heston.DeltaCall - 1.0f, heston.DeltaPut, 0.001f,
            "Put-call delta parity should hold exactly with analytical method");
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_ITM()
    {
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 110f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.7f
        };

        heston.CalculateAll();

        // For ITM call options (S=110, K=100), delta should be > 0.5
        Assert.IsTrue(heston.DeltaCall > 0.5f && heston.DeltaCall <= 1.0f, 
            $"ITM call delta should be > 0.5, got {heston.DeltaCall}");
        
        // For OTM put options (S=110, K=100), put delta should be > -0.5 (closer to 0)
        Assert.IsTrue(heston.DeltaPut > -0.5f && heston.DeltaPut <= 0.0f,
            $"OTM put delta should be > -0.5, got {heston.DeltaPut}");
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_OTM()
    {
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 90f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.7f
        };

        heston.CalculateAll();

        // For OTM call options (S=90, K=100), delta should be < 0.5
        Assert.IsTrue(heston.DeltaCall < 0.5f && heston.DeltaCall >= 0.0f, 
            $"OTM call delta should be < 0.5, got {heston.DeltaCall}");
        
        // For ITM put options (S=90, K=100), put delta should be < -0.5 (closer to -1)
        Assert.IsTrue(heston.DeltaPut < -0.5f && heston.DeltaPut >= -1.0f,
            $"ITM put delta should be < -0.5, got {heston.DeltaPut}");
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_DeltaParityAlwaysHolds()
    {
        var strikes = new float[] { 80f, 90f, 95f, 100f, 105f, 110f, 120f };
        
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 3.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.5f
        };

        foreach (var strike in strikes)
        {
            heston.Strike = strike;
            heston.CalculateAll();
            
            float parityDiff = heston.DeltaCall - heston.DeltaPut - 1.0f;
            Assert.AreEqual(0.0f, parityDiff, 0.01f,
                $"Analytical delta should satisfy put-call parity exactly for strike={strike}, got diff={parityDiff}");
        }
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_WithCorrelation()
    {
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 100f,
            Strike = 95f, // OTM put / ITM call
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 3.0f,
            VolatilityOfVolatility = 0.4f,
            Correlation = -0.8f // Strong negative correlation (leverage effect)
        };

        heston.CalculateAll();
        float negCorrCallDelta = heston.DeltaCall;
        float negCorrPutDelta = heston.DeltaPut;

        // Change to positive correlation
        heston.Correlation = 0.8f;
        heston.CalculateAll();
        float posCorrCallDelta = heston.DeltaCall;
        float posCorrPutDelta = heston.DeltaPut;

        // Delta values should be different with different correlation
        Assert.AreNotEqual(negCorrCallDelta, posCorrCallDelta, 0.001f,
            "Correlation should affect delta values");
        
        // Parity should still hold in both cases
        Assert.AreEqual(1.0f, negCorrCallDelta - negCorrPutDelta, 0.01f,
            "Parity should hold with negative correlation");
        Assert.AreEqual(1.0f, posCorrCallDelta - posCorrPutDelta, 0.01f,
            "Parity should hold with positive correlation");
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_Bounds()
    {
        var strikes = new float[] { 50f, 75f, 100f, 125f, 150f };
        
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.3f,
            LongTermVolatility = 0.25f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.5f,
            Correlation = -0.6f
        };

        foreach (var strike in strikes)
        {
            heston.Strike = strike;
            heston.CalculateAll();
            
            // Call delta must be in [0, 1]
            Assert.IsTrue(heston.DeltaCall >= 0.0f && heston.DeltaCall <= 1.0f,
                $"Call delta must be in [0,1] for strike={strike}, got {heston.DeltaCall}");
            
            // Put delta must be in [-1, 0]
            Assert.IsTrue(heston.DeltaPut >= -1.0f && heston.DeltaPut <= 0.0f,
                $"Put delta must be in [-1,0] for strike={strike}, got {heston.DeltaPut}");
        }
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_Monotonicity()
    {
        // Delta should increase monotonically with stock price for fixed strike
        var stockPrices = new float[] { 80f, 90f, 95f, 100f, 105f, 110f, 120f };
        
        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.5f
        };

        float previousCallDelta = -1f;
        foreach (var stockPrice in stockPrices)
        {
            heston.StockPrice = stockPrice;
            heston.CalculateAll();
            
            if (previousCallDelta > 0)
            {
                Assert.IsTrue(heston.DeltaCall >= previousCallDelta - 0.001f,
                    $"Call delta should increase with stock price: {previousCallDelta} -> {heston.DeltaCall}");
            }
            previousCallDelta = heston.DeltaCall;
        }
    }
}
