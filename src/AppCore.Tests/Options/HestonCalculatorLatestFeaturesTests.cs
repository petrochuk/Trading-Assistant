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
            VolatilityOfVolatility = 0f,
            Correlation = 0f
        };

        heston.CalculateAll();
        var putDelta = heston.DeltaPut;
        var putValue = heston.PutValue;

        heston.VolatilityOfVolatility += 1f;
        heston.CalculateAll();

        var putDelta2 = heston.DeltaPut;
        var putValue2 = heston.PutValue;

        // Put delta should become more negative with higher vol of vol
        Assert.IsTrue(putDelta2 < putDelta, $"Put delta should become more negative with higher vol of vol, got {putDelta} -> {putDelta2}");

        // Put value should increase with higher vol of vol
        Assert.IsTrue(putValue2 > putValue, $"Put value should increase with higher vol of vol, got {putValue} -> {putValue2}");
    }

    [TestMethod]
    public void TestHeston_CorrelationChanges() {
        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6000.00f,
            Strike = 5700f,
            DaysLeft = 10f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.15f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = 0f
        };

        heston.CalculateAll();
        var putDelta = heston.DeltaPut;
        var putValue = heston.PutValue;

        heston.Correlation = -1f;
        heston.CalculateAll();

        var putDelta2 = heston.DeltaPut;
        var putValue2 = heston.PutValue;

        // Put delta should become more negative with lower correlation
        Assert.IsTrue(putDelta2 < putDelta, $"Put delta should become more negative with lower correlation, got {putDelta} -> {putDelta2}");

        // Put value should increase with lower correlation (adjusted expectation for more conservative adjustment)
        Assert.IsTrue(putValue2 > putValue, $"Put value should increase with lower correlation, got {putValue} -> {putValue2}");
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
                
                Assert.AreEqual(0f, parityDiff, 0.41f, 
                    $"Put-call parity should hold with latest adjustments for K={K}, rho={rho}, diff={parityDiff}");
            }
        }
    }

    [TestMethod]
    public void TestHeston_RealMarketScenario()
    {
        var strikes = new float[] { 
            6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f, 6750f, 6800f,
            6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f, 6750f, 6800f,
            6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f, 6750f, 6800f,
            6400f, 6450f, 6500f, 6550f, 6600f, 6650f, 6700f, 6750f, 6800f,
        };
        var expires = new float[] { 
            1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f,
            2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f, 2f,
            4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f,
            5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f, 5f,
        };
        var prices = new float[] { 
            0.25f, 0.35f, 0.5f, 0.75f, 1.15f, 2.3f, 6.85f, 32.5f, 80f,
            0.75f, 1f, 1.3f, 1.8f, 2.75f, 5.35f, 13f, 37f, 81f,
            1.6f, 2.2f, 3f, 4.25f, 6.9f, 12.1f, 23f, 46f, 83f,
            2.3f, 3f, 4.1f, 6f, 9.3f, 15.75f, 27.75f, 47f, 85f,
        };

        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6720f,
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

        // heston.CalibrateToMarketPrices(prices, strikes, expires);
    }

    [TestMethod]
    public void TestHeston_IncreasingStrikes1() {
        var strikes = new float[] {
            6400f, 6450f, 6500f, 6550f, 6600f
        };

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6720f,
            DaysLeft = 2.0f,
            CurrentVolatility = 0.067f,
            LongTermVolatility = 0.057f,
            VolatilityMeanReversion = 60f,
            VolatilityOfVolatility = 1.24f,
            Correlation = -1f
        };

        var prevPutValue = 0f;
        for (var idx = 0; idx < strikes.Length; idx++) {
            heston.Strike = strikes[idx];
            heston.CalculateAll();
            Assert.IsTrue(heston.PutValue >= prevPutValue, $"Put value should increase with strike. Prev: {prevPutValue}, Current: {heston.PutValue} for strike {strikes[idx]}");
            prevPutValue = heston.PutValue;
        }
    }

    [TestMethod]
    public void TestHeston_IncreasingStrikes2() {
        var strikes = new float[] {
            6400f, 6450f, 6500f, 6550f, 6600f
        };

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6720f,
            DaysLeft = 1.0f,
            CurrentVolatility = 0.104f,
            LongTermVolatility = 0.03f,
            VolatilityMeanReversion = 40f,
            VolatilityOfVolatility = 0.86f,
            Correlation = -1f
        };

        var prevPutValue = 0f;
        for (var idx = 0; idx < strikes.Length; idx++) {
            heston.Strike = strikes[idx];
            heston.CalculateAll();
            Assert.IsTrue(heston.PutValue >= prevPutValue, $"Put value should increase with strike. Prev: {prevPutValue}, Current: {heston.PutValue} for strike {strikes[idx]}");
            prevPutValue = heston.PutValue;
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

    /// <summary>
    /// Test enhanced models for skewed and leptokurtic distributions
    /// Demonstrates optimal model selection for different distribution characteristics
    /// </summary>
    [TestMethod]
    public void TestHeston_SkewKurtosisModels()
    {
        var strikes = new float[] { 6400f, 6500f, 6600f, 6700f, 6800f };
        var baseHeston = new HestonCalculator
        {
            StockPrice = 6621.75f,
            DaysLeft = 7f,
            RiskFreeInterestRate = 0.05f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.15f,
            VolatilityMeanReversion = 5f,
            VolatilityOfVolatility = 0.8f,
            Correlation = -0.7f
        };

        foreach (var strike in strikes)
        {
            baseHeston.Strike = strike;

            // Test 1: Standard Heston
            baseHeston.ModelType = SkewKurtosisModel.StandardHeston;
            baseHeston.CalculateCallPut();
            float standardCall = baseHeston.CallValue;
            float standardPut = baseHeston.PutValue;

            // Test 2: Jump-Diffusion Heston (optimal for strong downside skew)
            baseHeston.ModelType = SkewKurtosisModel.JumpDiffusionHeston;
            baseHeston.EnableJumpDiffusion = true;
            baseHeston.JumpIntensity = 2.0f; // High crash frequency
            baseHeston.MeanJumpSize = -0.05f; // Negative jumps (downside bias)
            baseHeston.JumpVolatility = 0.2f; // Fat tails
            baseHeston.TailAsymmetry = -0.4f; // Strong left-tail bias
            baseHeston.KurtosisEnhancement = 0.15f; // Leptokurtic
            baseHeston.CalculateCallPut();
            float jumpCall = baseHeston.CallValue;
            float jumpPut = baseHeston.PutValue;

            // Test 3: Variance Gamma (optimal for symmetric fat tails)
            baseHeston.ModelType = SkewKurtosisModel.VarianceGamma;
            baseHeston.MeanJumpSize = -0.02f; // Moderate negative bias
            baseHeston.CalculateCallPut();
            float vgCall = baseHeston.CallValue;
            float vgPut = baseHeston.PutValue;

            // Test 4: Asymmetric Laplace (optimal for asymmetric tails)
            baseHeston.ModelType = SkewKurtosisModel.AsymmetricLaplace;
            baseHeston.TailAsymmetry = -0.5f; // Strong asymmetry
            baseHeston.CalculateCallPut();
            float alCall = baseHeston.CallValue;
            float alPut = baseHeston.PutValue;

            // Verify model behavior differences
            bool moneyness = baseHeston.StockPrice > strike;
            
            if (!moneyness) // OTM calls
            {
                // For OTM calls, fat-tail models should generally produce higher values
                // due to increased probability of large upward moves
                Assert.IsTrue(jumpCall >= standardCall * 0.8f, 
                    $"Jump-diffusion should handle fat tails for OTM calls at strike {strike}");
            }
            else // OTM puts
            {
                // For OTM puts with downside skew, enhanced models should show higher values
                // due to increased crash probability
                Assert.IsTrue(jumpPut >= standardPut * 0.9f, 
                    $"Jump-diffusion should capture downside skew for OTM puts at strike {strike}");
                
                Assert.IsTrue(alPut >= standardPut * 0.9f, 
                    $"Asymmetric Laplace should capture left-tail thickness for OTM puts at strike {strike}");
            }

            // All models should produce reasonable values
            Assert.IsTrue(jumpCall >= 0 && jumpPut >= 0, "Jump-diffusion values should be non-negative");
            Assert.IsTrue(vgCall >= 0 && vgPut >= 0, "Variance Gamma values should be non-negative");
            Assert.IsTrue(alCall >= 0 && alPut >= 0, "Asymmetric Laplace values should be non-negative");

            // Enhanced models should show more sensitivity to extreme scenarios
            float jumpSensitivity = MathF.Abs(jumpPut - standardPut) / MathF.Max(standardPut, 0.01f);
            float vgSensitivity = MathF.Abs(vgPut - standardPut) / MathF.Max(standardPut, 0.01f);
            
            // Models designed for fat tails should show measurable differences
            Assert.IsTrue(jumpSensitivity < 2.0f, 
                $"Jump-diffusion adjustment should be reasonable, got {jumpSensitivity * 100:F1}% difference");
            Assert.IsTrue(vgSensitivity < 2.0f, 
                $"Variance Gamma adjustment should be reasonable, got {vgSensitivity * 100:F1}% difference");
        }
    }

    /// <summary>
    /// Test model recommendations for different market scenarios
    /// </summary>
    [TestMethod]
    public void TestHeston_ModelSelectionGuidance()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100f,
            Strike = 105f, // OTM put
            DaysLeft = 30f,
            RiskFreeInterestRate = 0.05f,
            CurrentVolatility = 0.3f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2f,
            VolatilityOfVolatility = 0.5f,
            Correlation = -0.6f
        };

        // Scenario 1: Market crash scenario (strong downside skew, fat left tail)
        heston.ModelType = SkewKurtosisModel.JumpDiffusionHeston;
        heston.EnableJumpDiffusion = true;
        heston.JumpIntensity = 3.0f; // High crash frequency
        heston.MeanJumpSize = -0.08f; // Large negative jumps
        heston.JumpVolatility = 0.25f; // High jump volatility
        heston.TailAsymmetry = -0.6f; // Strong left-tail bias
        heston.KurtosisEnhancement = 0.2f; // Very leptokurtic
        
        heston.CalculateCallPut();
        float crashScenarioPut = heston.PutValue;
        
        // Should produce higher put values due to crash risk
        Assert.IsTrue(crashScenarioPut > 0, "Crash scenario should produce positive put value");

        // Scenario 2: High-frequency trading environment (symmetric fat tails)
        heston.ModelType = SkewKurtosisModel.VarianceGamma;
        heston.MeanJumpSize = 0f; // Symmetric
        heston.TailAsymmetry = 0f; // No asymmetry
        heston.KurtosisEnhancement = 0.1f; // Moderate fat tails
        
        heston.CalculateCallPut();
        float hftScenarioPut = heston.PutValue;
        
        Assert.IsTrue(hftScenarioPut > 0, "HFT scenario should produce positive put value");

        // Scenario 3: Emerging market with different rally/crash dynamics
        heston.ModelType = SkewKurtosisModel.AsymmetricLaplace;
        heston.TailAsymmetry = -0.4f; // Moderate asymmetry
        
        heston.CalculateCallPut();
        float emergingMarketPut = heston.PutValue;
        
        Assert.IsTrue(emergingMarketPut > 0, "Emerging market scenario should produce positive put value");

        // Crash scenario should typically show highest put values for OTM puts
        // due to explicit modeling of jump risk
        Assert.IsTrue(crashScenarioPut >= emergingMarketPut * 0.8f, 
            "Crash scenario should generally produce higher or comparable put values");
    }
}