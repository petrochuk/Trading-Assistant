using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class HestonCalculatorTests
{
    private HestonCalculator CreateStandardHeston(
        float stockPrice = 100.0f, 
        float strike = 100.0f) {
        return new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = -0.7f,
        };
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f)]
    [DataRow(500.0f, 500.0f)]
    [DataRow(1000.0f, 1000.0f)]
    [DataRow(5000.0f, 5000.0f)]
    public void TestHeston_BasicCallPutCalculation(float stockPrice, float strike) {
        var heston = CreateStandardHeston(stockPrice, strike);
        heston.CalculateCallPut();

        // Basic sanity checks
        Assert.IsTrue(heston.CallValue > 0, "Call value should be positive for ATM option");
        Assert.IsTrue(heston.PutValue > 0, "Put value should be positive for ATM option");
        
        // Put-call parity check: C - P = S - K*e^(-r*T)
        float discountedStrike = heston.Strike * MathF.Exp(-heston.RiskFreeInterestRate * heston.ExpiryTime);
        float putCallParity = heston.CallValue - heston.PutValue - (heston.StockPrice - discountedStrike);
        Assert.AreEqual(0.0f, putCallParity, 0.1f, "Put-call parity should hold within tolerance");
    }

    [TestMethod]
    [DataRow(100.0f, 110.0f)]
    [DataRow(100.0f, 90.0f)]
    [DataRow(1000.0f, 1050.0f)]
    [DataRow(5000.0f, 4000.0f)]
    public void TestHeston_AtExpiration(float stockPrice, float strike)
    {
        var heston = CreateStandardHeston(stockPrice, strike);
        heston.DaysLeft = 0.0f; // At expiration

        heston.CalculateCallPut();

        var expectedCallValue = MathF.Max(heston.StockPrice - heston.Strike, 0);
        var expectedPutValue = MathF.Max(heston.Strike - heston.StockPrice, 0);
        Assert.AreEqual(expectedCallValue, heston.CallValue, 0.001f, "Call value at expiration should be max(S-K, 0)");
        Assert.AreEqual(expectedPutValue, heston.PutValue, 0.001f, "Put value at expiration should be max(K-S, 0)");
    }

    [TestMethod]
    public void TestHeston_MoneynessBehavior()
    {
        var heston = CreateStandardHeston();
        
        // Deep ITM call
        heston.StockPrice = 120.0f;
        heston.Strike = 100.0f;
        heston.CalculateCallPut();
        float itmCallValue = heston.CallValue;
        
        // ATM call
        heston.StockPrice = 100.0f;
        heston.Strike = 100.0f;
        heston.CalculateCallPut();
        float atmCallValue = heston.CallValue;
        
        // OTM call
        heston.StockPrice = 80.0f;
        heston.Strike = 100.0f;
        heston.CalculateCallPut();
        float otmCallValue = heston.CallValue;

        Assert.IsTrue(itmCallValue > atmCallValue, "ITM call should be more valuable than ATM call");
        Assert.IsTrue(atmCallValue > otmCallValue, "ATM call should be more valuable than OTM call");
    }

    [TestMethod]
    public void TestHeston_VolatilityImpact()
    {
        var heston = CreateStandardHeston();
        
        // Low volatility
        heston.CurrentVolatility = 0.1f;
        heston.LongTermVolatility = 0.1f;
        heston.CalculateCallPut();
        float lowVolCallValue = heston.CallValue;
        
        // High volatility
        heston.CurrentVolatility = 0.4f;
        heston.LongTermVolatility = 0.4f;
        heston.CalculateCallPut();
        float highVolCallValue = heston.CallValue;

        Assert.IsTrue(highVolCallValue > lowVolCallValue, "Higher volatility should increase option value");
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f, 0.5f, -0.5f)]
    [DataRow(5000.0f, 5100.0f, 0.4f, -0.6f)]
    [DataRow(5000.0f, 4900.0f, 0.6f, -0.4f)]
    public void TestHeston_GreeksCalculation(float stockPrice, float strike, float expectedDeltaCall, float expectedDeltaPut)
    {
        var heston = CreateStandardHeston(stockPrice, strike);
        heston.CalculateAll();

        // Basic sanity checks for Greeks
        Assert.AreEqual(expectedDeltaCall, heston.DeltaCall, 0.1f, "Call delta should be close to expected");
        Assert.AreEqual(expectedDeltaPut, heston.DeltaPut, 0.1f, "Put delta should be close to expected");
        Assert.AreEqual(0, heston.Gamma, 0.1f, "Gamma should be small for ATM options");
        Assert.IsTrue(heston.VegaCall > 0, $"Call vega should be positive, got {heston.VegaCall}");
        Assert.IsTrue(heston.VegaPut > 0, $"Put vega should be positive, got {heston.VegaPut}");
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f)]
    [DataRow(5000.0f, 5100.0f)]
    [DataRow(5000.0f, 4900.0f)]
    [DataRow(5000.0f, 3000.0f)]
    [DataRow(5000.0f, 2000.0f)]
    [DataRow(5000.0f, 1000.0f)]
    [DataRow(5000.0f, 100.0f)]
    public void TestHeston_DeltaNeutralityProperty(float stockPrice, float strike)
    {
        var heston = CreateStandardHeston(stockPrice, strike);
        heston.CalculateAll();

        float combinedDelta = heston.DeltaCall - heston.DeltaPut;
        
        Assert.IsTrue(heston.DeltaCall >= 0 && heston.DeltaCall <= 1, $"Call delta should be between 0 and 1, got {heston.DeltaCall}");
        Assert.IsTrue(heston.DeltaPut >= -1 && heston.DeltaPut <= 0, $"Put delta should be between -1 and 0, got {heston.DeltaPut}");
        Assert.AreEqual(1f, combinedDelta, 0.1f, $"Combined delta (Call - Put) should be close to 1, got {combinedDelta}");
    }

    [TestMethod]
    public void TestHeston_ThetaDecay()
    {
        var heston = CreateStandardHeston();
        heston.DaysLeft = 30.0f;
        heston.CalculateAll();
        float theta30Days = heston.ThetaCall;

        heston.DaysLeft = 10.0f;
        heston.CalculateAll();
        float theta10Days = heston.ThetaCall;

        // For options with time value, theta should generally be negative
        Assert.IsTrue(theta30Days < 0 || theta10Days < 0, "At least one theta should be negative (time decay)");
    }

    [TestMethod]
    public void TestHeston_CorrelationImpact()
    {
        var heston = CreateStandardHeston();
        
        // Make parameters more extreme to see correlation effect
        heston.VolatilityOfVolatility = 0.8f; // Higher vol of vol
        heston.ExpiryTime = 0.25f; // Longer time to expiration
        
        // Negative correlation (typical for equity options)
        heston.Correlation = -0.9f;
        heston.CalculateCallPut();
        var negCorrCallValue = heston.CallValue;
        var negCorrPutValue = heston.PutValue;

        // No correlation
        heston.Correlation = 0f;
        heston.CalculateCallPut();
        var noCorrCallValue = heston.CallValue;
        var noCorrPutValue = heston.PutValue;

        // Positive correlation
        heston.Correlation = 0.9f;
        heston.CalculateCallPut();
        var posCorrCallValue = heston.CallValue;
        var posCorrPutValue = heston.PutValue;

        // Negative correlation should decrease call values (less upside volatility)
        Assert.IsTrue(negCorrCallValue < noCorrCallValue, 
            $"Negative correlation should decrease call value compared to no correlation. Neg: {negCorrCallValue}, No: {noCorrCallValue}");
        
        // Negative correlation should increase put values (more downside volatility)  
        Assert.IsTrue(negCorrPutValue > noCorrPutValue, 
            $"Negative correlation should increase put value compared to no correlation. Neg: {negCorrPutValue}, No: {noCorrPutValue}");

        // Positive correlation should have opposite effects
        Assert.IsTrue(posCorrCallValue > noCorrCallValue, 
            $"Positive correlation should increase call value compared to no correlation. Pos: {posCorrCallValue}, No: {noCorrCallValue}");
        Assert.IsTrue(posCorrPutValue < noCorrPutValue, 
            $"Positive correlation should decrease put value compared to no correlation. Pos: {posCorrPutValue}, No: {noCorrPutValue}");
    }

    [TestMethod]
    public void TestHeston_MeanReversionImpact()
    {
        var heston = CreateStandardHeston();
        
        // Make parameters more extreme to see mean reversion effect
        heston.CurrentVolatility = 0.4f; // High current vol
        heston.LongTermVolatility = 0.1f; // Low long-term vol
        heston.ExpiryTime = 0.25f; // Longer time to expiration
        
        // Slow mean reversion
        heston.VolatilityMeanReversion = 0.1f;
        heston.CalculateCallPut();
        float slowMRCallValue = heston.CallValue;
        
        // Fast mean reversion
        heston.VolatilityMeanReversion = 10.0f;
        heston.CalculateCallPut();
        float fastMRCallValue = heston.CallValue;

        // Should see significant difference in option values with extreme mean reversion difference
        Assert.AreNotEqual(slowMRCallValue, fastMRCallValue, 0.1f, "Mean reversion speed should impact option values with extreme parameters");
    }

    [TestMethod]
    public void TestHeston_VolOfVolImpact()
    {
        var heston = CreateStandardHeston();
        
        // Low vol of vol
        heston.VolatilityOfVolatility = 0.1f;
        heston.CalculateCallPut();
        float lowVolVolCallValue = heston.CallValue;
        
        // High vol of vol
        heston.VolatilityOfVolatility = 0.8f;
        heston.CalculateCallPut();
        float highVolVolCallValue = heston.CallValue;

        Assert.IsTrue(highVolVolCallValue > lowVolVolCallValue, "Higher vol of vol should increase option value");
    }

    [TestMethod]
    public void TestHeston_GetGreeksStructures()
    {
        var heston = CreateStandardHeston();
        
        var callGreeks = heston.GetCallGreeks();
        var putGreeks = heston.GetPutGreeks();

        // Verify Greeks structures are populated
        Assert.AreEqual(heston.DeltaCall, callGreeks.DeltaHeston);
        Assert.AreEqual(heston.Gamma, callGreeks.Gamma);
        Assert.AreEqual(heston.ThetaCall, callGreeks.ThetaHeston);
        Assert.AreEqual(heston.VegaCall, callGreeks.Vega);
        Assert.AreEqual(heston.VannaCall, callGreeks.Vanna);
        Assert.AreEqual(heston.CharmCall, callGreeks.Charm);

        Assert.AreEqual(heston.DeltaPut, putGreeks.DeltaHeston);
        Assert.AreEqual(heston.Gamma, putGreeks.Gamma);
        Assert.AreEqual(heston.ThetaPut, putGreeks.DeltaHeston);
        Assert.AreEqual(heston.VegaPut, putGreeks.Vega);
        Assert.AreEqual(heston.VannaPut, putGreeks.Vanna);
        Assert.AreEqual(heston.CharmPut, putGreeks.Charm);
    }

    [TestMethod]
    public void TestHeston_ExtremeParameters()
    {
        var heston = CreateStandardHeston();
        
        // Test with extreme but valid parameters
        heston.CurrentVolatility = 0.01f; // Very low vol
        heston.LongTermVolatility = 0.01f;
        heston.VolatilityMeanReversion = 10.0f; // Very fast mean reversion
        heston.VolatilityOfVolatility = 0.01f; // Very low vol of vol
        heston.Correlation = -0.99f; // Almost perfect negative correlation

        try
        {
            heston.CalculateAll();
            Assert.IsTrue(heston.CallValue > 0, "Should still produce positive call value");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should handle extreme but valid parameters without exception: {ex.Message}");
        }
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(100.0f, 100.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(500.0f, 500.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(500.0f, 500.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(1000.0f, 1000.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(1000.0f, 1000.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(5000.0f, 5000.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(5000.0f, 5000.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(1000.0f, 1100.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(1000.0f, 1100.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(5000.0f, 4900.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(5000.0f, 4900.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(5000.0f, 3000.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(5000.0f, 3000.0f, HestonIntegrationMethod.Adaptive)]
    [DataRow(5000.0f, 7000.0f, HestonIntegrationMethod.Approximation)]
    [DataRow(5000.0f, 7000.0f, HestonIntegrationMethod.Adaptive)]
    public void TestHeston_CompareWithBlackScholes(float stockPrice, float strike, HestonIntegrationMethod hestonIntegrationMethod) {
        // When Heston parameters reduce to constant volatility, 
        // it should approximate Black-Scholes
        var heston = new HestonCalculator
        {
            IntegrationMethod = hestonIntegrationMethod,
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 100.0f, // Very fast mean reversion
            VolatilityOfVolatility = 0.001f, // Very low vol of vol
            Correlation = 0f // No correlation
        };

        var blackScholes = new BlackNScholesCaculator
        {
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = 30.0f,
            ImpliedVolatility = 0.2f
        };

        heston.CalculateAll();
        blackScholes.CalculateAll();

        // Should be reasonably close
        float relativeError = heston.CallValue == blackScholes.CallValue ? 0 : MathF.Abs(heston.CallValue - blackScholes.CallValue) / blackScholes.CallValue;
        Assert.IsTrue(relativeError < 0.04f, 
            $"Heston should approximate Black-Scholes when vol is constant. Heston: {heston.CallValue}, BS: {blackScholes.CallValue}, Error: {relativeError:P2}");
        relativeError = heston.PutValue == blackScholes.PutValue ? 0 : MathF.Abs(heston.PutValue - blackScholes.PutValue) / blackScholes.PutValue;
        Assert.IsTrue(relativeError < 0.04f, 
            $"Heston should approximate Black-Scholes when vol is constant. Heston: {heston.PutValue}, BS: {blackScholes.PutValue}, Error: {relativeError:P2}");
    }

    [TestMethod]
    public void TestHeston_PutCallParity_MultiStrikeMaturity()
    {
        var strikes = new float[] { 80f, 90f, 100f, 110f, 120f };
        var maturities = new float[] { 10f/365f, 30f/365f, 90f/365f };
        foreach (var T in maturities)
        {
            foreach (var K in strikes)
            {
                var heston = new HestonCalculator
                {
                    StockPrice = 100f,
                    Strike = K,
                    RiskFreeInterestRate = 0.03f,
                    ExpiryTime = T,
                    CurrentVolatility = 0.25f,
                    LongTermVolatility = 0.25f,
                    VolatilityMeanReversion = 5.0f,
                    VolatilityOfVolatility = 0.05f,
                    Correlation = 0.0f
                };
                heston.CalculateCallPut();
                float discountedStrike = K * MathF.Exp(-0.03f * T);
                float parityDiff = (heston.CallValue - heston.PutValue) - (heston.StockPrice - discountedStrike);
                Assert.AreEqual(0f, parityDiff, 0.4f, $"Put-call parity drift too large for K={K}, T={T}, diff={parityDiff}");
            }
        }
    }

    [TestMethod]
    public void TestHeston_CallPriceMonotonicDecreasingInStrike()
    {
        var strikes = new float[] { 80f, 90f, 100f, 110f, 120f };
        float prev = float.MaxValue;
        foreach (var K in strikes)
        {
            var heston = new HestonCalculator
            {
                StockPrice = 100f,
                Strike = K,
                RiskFreeInterestRate = 0.01f,
                ExpiryTime = 60f/365f,
                CurrentVolatility = 0.2f,
                LongTermVolatility = 0.2f,
                VolatilityMeanReversion = 10.0f,
                VolatilityOfVolatility = 0.001f,
                Correlation = 0.0f
            };
            heston.CalculateCallPut();
            Assert.IsTrue(heston.CallValue <= prev + 0.05f, $"Call price should not increase materially with higher strike. K={K}, C={heston.CallValue}, prev={prev}");
            prev = heston.CallValue;
        }
    }

    [TestMethod]
    public void TestHeston_CallConvexityInStrike()
    {
        // Check simple convexity: C(K-) - 2C(K) + C(K+) >= 0 (within tolerance)
        float T = 45f/365f;
        float r = 0.02f;
        float[] K = { 95f, 100f, 105f };
        float[] C = new float[3];
        for (int i = 0; i < 3; i++)
        {
            var heston = new HestonCalculator
            {
                StockPrice = 100f,
                Strike = K[i],
                RiskFreeInterestRate = r,
                ExpiryTime = T,
                CurrentVolatility = 0.22f,
                LongTermVolatility = 0.22f,
                VolatilityMeanReversion = 25.0f,
                VolatilityOfVolatility = 0.0001f,
                Correlation = 0.0f
            };
            heston.CalculateCallPut();
            C[i] = heston.CallValue;
        }
        float secondDiff = C[0] - 2*C[1] + C[2];
        Assert.IsTrue(secondDiff >= -0.25f, $"Call price should be convex in strike (within tolerance). Second diff={secondDiff}");
    }
}