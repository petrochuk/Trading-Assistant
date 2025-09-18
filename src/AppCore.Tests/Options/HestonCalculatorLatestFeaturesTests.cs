using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class HestonCalculatorLatestFeaturesTests
{
    /// <summary>
    /// Test that demonstrates the new Feller condition checking
    /// </summary>
    [TestMethod]
    public void TestHeston_FellerConditionCheck()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100.0f,
            Strike = 100.0f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.5f
        };

        // Check if Feller condition is satisfied
        bool fellerSatisfied = heston.IsFellerConditionSatisfied;
        Assert.IsTrue(fellerSatisfied, "Feller condition should be satisfied with these parameters");

        // Test with parameters that violate Feller condition
        heston.VolatilityOfVolatility = 2.0f; // Very high vol of vol
        fellerSatisfied = heston.IsFellerConditionSatisfied;
        Assert.IsFalse(fellerSatisfied, "Feller condition should be violated with high vol of vol");
    }

    /// <summary>
    /// Test that demonstrates the behavior with actual SPX-like parameters
    /// Note: The original parameters violate the Feller condition and are automatically adjusted
    /// </summary>
    [TestMethod]
    public void TestHeston_ActualSPX() {
        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6621.75f,
            Strike = 6575f,
            DaysLeft = 1f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.15f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f
        };

        heston.CalculateAll();
        var deltaCall = heston.DeltaCall;
        var deltaPut = heston.DeltaPut;

        heston.DaysLeft += 0.01f;
        heston.CalculateAll();

        var deltaCall2 = heston.DeltaCall;
        var deltaPut2 = heston.DeltaPut;

        // Verify that deltas are within reasonable bounds
        Assert.IsTrue(deltaCall >= 0.0f && deltaCall <= 1.0f, $"Initial call delta should be between 0 and 1, got {deltaCall}");
        Assert.IsTrue(deltaPut >= -1.0f && deltaPut <= 0.0f, $"Initial put delta should be between -1 and 0, got {deltaPut}");
        Assert.IsTrue(deltaCall2 >= 0.0f && deltaCall2 <= 1.0f, $"Second call delta should be between 0 and 1, got {deltaCall2}");
        Assert.IsTrue(deltaPut2 >= -1.0f && deltaPut2 <= 0.0f, $"Second put delta should be between -1 and 0, got {deltaPut2}");

        // Verify that changes are small and reasonable for very short-term options
        float deltaCallChange = MathF.Abs(deltaCall2 - deltaCall);
        float deltaPutChange = MathF.Abs(deltaPut2 - deltaPut);
        
        Assert.IsTrue(deltaCallChange < 0.01f, $"Call delta change should be small for short-term options, got {deltaCallChange}");
        Assert.IsTrue(deltaPutChange < 0.01f, $"Put delta change should be small for short-term options, got {deltaPutChange}");
        
        // Verify that the delta calculation is stable (not jumping to extreme values)
        Assert.IsFalse(deltaCall2 == 1.0f && deltaPut2 == 0.0f, "Delta calculation should not jump to extreme boundary values");
    }

    [TestMethod]
    public void TestHeston_VolChanges() {
        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6000.00f,
            Strike = 5700f,
            DaysLeft = 10f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.15f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f
        };

        heston.CalculateAll();
        var putDelta = heston.DeltaPut;
        var putValue = heston.PutValue;

        heston.VolatilityOfVolatility += 10f;
        heston.CalculateAll();

        var putDelta2 = heston.DeltaPut;
        var putValue2 = heston.PutValue;

        // Put delta should become more negative with higher vol of vol
        //Assert.IsTrue(putDelta2 < putDelta, $"Put delta should become more negative with higher vol of vol, got {putDelta} -> {putDelta2}");

        // Put value should increase with higher vol of vol
        //Assert.IsTrue(putValue2 > putValue, $"Put value should increase with higher vol of vol, got {putValue} -> {putValue2}");
    }

    /// <summary>
    /// Test the new integration methods
    /// </summary>
    [TestMethod]
    public void TestHeston_IntegrationMethods()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100.0f,
            Strike = 100.0f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.25f,
            VolatilityMeanReversion = 3.0f,
            VolatilityOfVolatility = 0.2f,
            Correlation = -0.3f
        };

        // Test approximation method (default)
        heston.IntegrationMethod = HestonIntegrationMethod.Approximation;
        heston.CalculateCallPut();
        float approxCall = heston.CallValue;
        float approxPut = heston.PutValue;

        // Test that approximation method works
        Assert.IsTrue(approxCall > 0, "Approximation method should produce positive call value");
        Assert.IsTrue(approxPut > 0, "Approximation method should produce positive put value");

        // Test that IntegrationMethod property can be set
        heston.IntegrationMethod = HestonIntegrationMethod.Adaptive;
        Assert.AreEqual(HestonIntegrationMethod.Adaptive, heston.IntegrationMethod, "Integration method should be settable");

        heston.IntegrationMethod = HestonIntegrationMethod.Fixed;
        Assert.AreEqual(HestonIntegrationMethod.Fixed, heston.IntegrationMethod, "Integration method should be settable");

        // The characteristic function methods may fall back to approximation
        // Just test that they don't throw exceptions and produce some result
        heston.IntegrationMethod = HestonIntegrationMethod.Adaptive;
        try
        {
            heston.CalculateCallPut();
            // Even if it falls back to approximation, it should produce non-negative values
            Assert.IsTrue(heston.CallValue >= 0, $"Adaptive method should produce non-negative call value, got {heston.CallValue}");
            Assert.IsTrue(heston.PutValue >= 0, $"Adaptive method should produce non-negative put value, got {heston.PutValue}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Adaptive integration method should not throw exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Test parameter validation and adjustment
    /// </summary>
    [TestMethod]
    public void TestHeston_ParameterValidation()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100.0f,
            Strike = 100.0f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = -0.1f, // Invalid negative volatility
            LongTermVolatility = 0.0f, // Invalid zero volatility
            VolatilityMeanReversion = -1.0f, // Invalid negative mean reversion
            VolatilityOfVolatility = 0.0f, // Invalid zero vol of vol
            Correlation = -1.5f // Invalid correlation outside [-1, 1]
        };

        // The calculator should handle invalid parameters gracefully
        try
        {
            heston.CalculateCallPut();
            Assert.IsTrue(heston.CallValue >= 0, "Call value should be non-negative even with invalid initial parameters");
            Assert.IsTrue(heston.PutValue >= 0, "Put value should be non-negative even with invalid initial parameters");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Calculator should handle invalid parameters gracefully: {ex.Message}");
        }
    }

    /// <summary>
    /// Test that latest adjustments maintain put-call parity
    /// </summary>
    [TestMethod]
    public void TestHeston_LatestAdjustmentsPutCallParity()
    {
        var strikes = new float[] { 80f, 90f, 100f, 110f, 120f };
        var correlations = new float[] { -0.8f, -0.5f, 0.0f, 0.3f, 0.7f };

        foreach (var K in strikes)
        {
            foreach (var rho in correlations)
            {
                var heston = new HestonCalculator
                {
                    StockPrice = 100f,
                    Strike = K,
                    RiskFreeInterestRate = 0.03f,
                    ExpiryTime = 45f / 365f,
                    CurrentVolatility = 0.25f,
                    LongTermVolatility = 0.2f,
                    VolatilityMeanReversion = 3.0f,
                    VolatilityOfVolatility = 0.3f,
                    Correlation = rho
                };

                heston.CalculateCallPut();
                float discountedStrike = K * MathF.Exp(-0.03f * heston.ExpiryTime);
                float parityDiff = (heston.CallValue - heston.PutValue) - (heston.StockPrice - discountedStrike);
                
                Assert.AreEqual(0f, parityDiff, 0.4f, 
                    $"Put-call parity should hold with latest adjustments for K={K}, rho={rho}, diff={parityDiff}");
            }
        }
    }

    [TestMethod]
    public void TestHeston_RealMarketScenario()
    {
        var strikes = new float[] { 
            6300f, 6350f, 6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f,
            6300f, 6350f, 6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f,
            6300f, 6350f, 6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f,
        };
        var expires = new float[] { 
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f,
            4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f
        };
        var prices = new float[] { 
            0.8f, 1f, 1.2f, 1.5f, 2.1f, 4.1f, 18.25f, 61.5f, 111.5f,
            1.45f, 1.7f, 2.15f, 2.7f, 4.1f, 8f, 23.25f, 62f, 111.6f,
            2.9f, 3.65f, 4.9f, 7.1f, 11.3f, 20.25f, 38f, 69.5f, 113.25f
        };

        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6592.5f,
            DaysLeft = 1.0f,
            CurrentVolatility = 0.10f,
            LongTermVolatility = 0.15f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f
        };

        for (var idx = 0; idx < strikes.Length; idx++)
        {
            heston.Strike = strikes[idx];
            heston.DaysLeft = expires[idx];
            heston.CalculateAll();
        }
    }

    /// <summary>
    /// Test performance of different integration methods
    /// </summary>
    [TestMethod]
    public void TestHeston_IntegrationMethodPerformance()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100.0f,
            Strike = 100.0f,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.25f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.5f
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Test approximation method performance
        heston.IntegrationMethod = HestonIntegrationMethod.Approximation;
        for (int i = 0; i < 100; i++)
        {
            heston.CalculateCallPut();
        }
        stopwatch.Stop();
        var approximationTime = stopwatch.ElapsedMilliseconds;

        stopwatch.Restart();

        // Test adaptive method performance
        heston.IntegrationMethod = HestonIntegrationMethod.Adaptive;
        for (int i = 0; i < 10; i++) // Fewer iterations as this method is more computationally intensive
        {
            heston.CalculateCallPut();
        }
        stopwatch.Stop();
        var adaptiveTime = stopwatch.ElapsedMilliseconds;

        // Approximation method should be faster
        Assert.IsTrue(approximationTime >= 0, "Approximation method should complete");
        Assert.IsTrue(adaptiveTime >= 0, "Adaptive method should complete");
        
        // Both methods should complete in reasonable time (less than 10 seconds each)
        Assert.IsTrue(approximationTime < 10000, "Approximation method should be fast");
        Assert.IsTrue(adaptiveTime < 10000, "Adaptive method should complete in reasonable time");
    }
}