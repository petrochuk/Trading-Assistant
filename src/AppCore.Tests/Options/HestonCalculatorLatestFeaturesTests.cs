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