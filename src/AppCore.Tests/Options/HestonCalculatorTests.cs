using AppCore.Extenstions;
using AppCore.Options;
using System.Diagnostics;

namespace AppCore.Tests.Options;

[TestClass]
public class HestonCalculatorTests
{
    #region HelperMethods

    private HestonCalculator CreateStandardHeston(
        float daysLeft = 30.0f,
        float stockPrice = 100.0f, 
        float strike = 100.0f,
        float correlation = -0.7f) {
        return new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0f,
            DaysLeft = daysLeft,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 2.0f,
            VolatilityOfVolatility = 0.3f,
            Correlation = correlation,
        };
    }

    #endregion

    [TestMethod]
    [DataRow(100.0f, 0)]
    [DataRow(500.0f, 0.5f)]
    [DataRow(1000.0f, -0.5f)]
    [DataRow(5000.0f, -0.9f)]
    public void TestHeston_ATM_BasicCalculation(float stockPrice, float correlation) {
        // Stock price equals strike price (ATM)
        var heston = CreateStandardHeston(stockPrice: stockPrice, 
            strike: stockPrice, correlation: correlation);
        heston.CalculateAll();

        // Basic sanity checks
        Assert.IsGreaterThan(0, heston.CallValue, "Call value should be positive for ATM option");
        Assert.IsGreaterThan(0, heston.PutValue, "Put value should be positive for ATM option");
        Assert.AreEqual(0.5f, heston.DeltaCall, 0.1f, "Call delta should be approximately 0.5 for ATM option");
        Assert.AreEqual(-0.5f, heston.DeltaPut, 0.1f, "Put delta should be approximately -0.5 for ATM option");

        // Put-call parity check: C - P = S - K*e^(-r*T)
        float discountedStrike = heston.Strike * MathF.Exp(-heston.RiskFreeInterestRate * heston.ExpiryTime);
        float putCallParity = heston.CallValue - heston.PutValue - (heston.StockPrice - discountedStrike);
        Assert.AreEqual(0.0f, putCallParity, 0.1f, "Put-call parity should hold within tolerance");

        Assert.AreEqual(heston.PutValue, heston.CallValue, 0.1f, "Call and put values should be approximately equal for ATM options");
    }

    [TestMethod]
    [DataRow(100.0f, 110.0f)]
    [DataRow(100.0f, 90.0f)]
    [DataRow(1000.0f, 1050.0f)]
    [DataRow(5000.0f, 4000.0f)]
    public void TestHeston_AtExpiration(float stockPrice, float strike)
    {
        var heston = CreateStandardHeston(stockPrice: stockPrice, strike: strike);
        heston.DaysLeft = 0.0f; // At expiration

        heston.CalculateAll();

        var expectedCallValue = MathF.Max(heston.StockPrice - heston.Strike, 0);
        var expectedPutValue = MathF.Max(heston.Strike - heston.StockPrice, 0);
        Assert.AreEqual(expectedCallValue, heston.CallValue, 0.001f, "Call value at expiration should be max(S-K, 0)");
        Assert.AreEqual(expectedPutValue, heston.PutValue, 0.001f, "Put value at expiration should be max(K-S, 0)");

        // Assert delta behavior at expiration
        if (stockPrice < strike) {
            Assert.AreEqual(0.0f, heston.DeltaCall, 0.001f, "OTM call delta at expiration should be 0");
            Assert.AreEqual(-1.0f, heston.DeltaPut, 0.001f, "ITM put delta at expiration should be -1");
        } else {
            Assert.AreEqual(1.0f, heston.DeltaCall, 0.001f, "ITM call delta at expiration should be 1");
            Assert.AreEqual(0.0f, heston.DeltaPut, 0.001f, "OTM put delta at expiration should be 0");
        }
    }

    [TestMethod]
    public void TestHeston_MoneynessImpact() {
        var heston = CreateStandardHeston();
        
        // ITM
        heston.StockPrice = 120.0f;
        heston.Strike = 100.0f;
        heston.CalculateAll();
        var itmCallValue = heston.CallValue;
        Assert.IsGreaterThan(0.5, heston.DeltaCall, "ITM call delta should be greater than 0.5");
        Assert.IsGreaterThan(-0.5, heston.DeltaPut, "ITM put delta should be greater than -0.5");

        // ATM
        heston.StockPrice = 100.0f;
        heston.Strike = 100.0f;
        heston.CalculateAll();
        var atmCallValue = heston.CallValue;
        Assert.AreEqual(0.5f, heston.DeltaCall, 0.1f, "ATM call delta should be approximately 0.5");
        Assert.AreEqual(-0.5f, heston.DeltaPut, 0.1f, "ATM put delta should be approximately -0.5");

        // OTM
        heston.StockPrice = 80.0f;
        heston.Strike = 100.0f;
        heston.CalculateAll();
        var otmCallValue = heston.CallValue;
        Assert.IsLessThan(0.5, heston.DeltaCall, "OTM call delta should be less than 0.5");
        Assert.IsLessThan(-0.5, heston.DeltaPut, "OTM put delta should be greater than -0.5");

        Assert.IsGreaterThan(atmCallValue, itmCallValue, "ITM call should be more valuable than ATM call");
        Assert.IsGreaterThan(otmCallValue, atmCallValue, "ATM call should be more valuable than OTM call");
    }

    [TestMethod]
    public void TestHeston_VolatilityImpact()
    {
        var hestonLowIV = CreateStandardHeston(correlation: 0);
        hestonLowIV.CurrentVolatility = 0.1f;
        hestonLowIV.LongTermVolatility = 0.1f;
        hestonLowIV.CalculateAll();

        var hestonHighIV = CreateStandardHeston(correlation: 0);
        hestonHighIV.CurrentVolatility = 0.4f;
        hestonHighIV.LongTermVolatility = 0.4f;
        hestonHighIV.CalculateAll();

        Assert.IsGreaterThan(hestonLowIV.CallValue, hestonHighIV.CallValue, "Higher volatility should increase option value");
        Assert.IsGreaterThan(hestonLowIV.PutValue, hestonHighIV.PutValue, "Higher volatility should increase option value");

        Assert.AreEqual(hestonLowIV.DeltaCall, hestonHighIV.DeltaCall, 0.1f, "Delta should not vary drastically with volatility changes for ATM options");
        Assert.AreEqual(hestonLowIV.DeltaPut, hestonHighIV.DeltaPut, 0.1f, "Delta should not vary drastically with volatility changes for ATM options");
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f, 0.5f, -0.5f)]
    [DataRow(5000.0f, 5100.0f, 0.4f, -0.6f)]
    [DataRow(5000.0f, 4900.0f, 0.71f, -0.29f)]
    public void TestHeston_Greeks_TestCases(float stockPrice, float strike, float expectedDeltaCall, float expectedDeltaPut)
    {
        var heston = CreateStandardHeston(stockPrice: stockPrice, strike: strike);
        heston.CalculateAll();

        // Basic sanity checks for Greeks
        Assert.AreEqual(expectedDeltaCall, heston.DeltaCall, 0.1f, "Call delta should be close to expected");
        Assert.AreEqual(expectedDeltaPut, heston.DeltaPut, 0.1f, "Put delta should be close to expected");
        Assert.AreEqual(0, heston.Gamma, 0.1f, "Gamma should be small for ATM options");
        Assert.IsGreaterThan(0, heston.VegaCall, $"Call vega should be positive, got {heston.VegaCall}");
        Assert.IsGreaterThan(0, heston.VegaPut, $"Put vega should be positive, got {heston.VegaPut}");
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
        var heston30Days = CreateStandardHeston();
        heston30Days.DaysLeft = 30.0f;
        heston30Days.CalculateAll();

        var heston10Days = CreateStandardHeston();
        heston10Days.DaysLeft = 10.0f;
        heston10Days.CalculateAll();

        Assert.IsLessThan(heston30Days.PutValue, heston10Days.PutValue, "Put value should decrease as time to expiration decreases");
        Assert.IsLessThan(heston30Days.DeltaPut, heston10Days.DeltaPut, "Put delta should decrease as time to expiration decreases");

        Assert.IsLessThan(heston30Days.CallValue, heston10Days.CallValue, "Call value should decrease as time to expiration decreases");
        Assert.IsLessThan(heston30Days.DeltaCall, heston10Days.DeltaCall, "Call delta should decrease as time to expiration decreases");
    }

    [TestMethod]
    [DataRow(4000f)]
    [DataRow(5000f)]
    [DataRow(6000f)]
    public void TestHeston_CorrelationImpact(float strike)
    {
        // 5% move
        var hestonNegCorr = CreateStandardHeston(stockPrice: strike * 1.05f, strike: strike, correlation: -0.9f);
        hestonNegCorr.CalculateAll();
        var hestonNoCorr = CreateStandardHeston(stockPrice: strike * 1.05f, strike: strike, correlation: 0f);
        hestonNoCorr.CalculateAll();
        var hestonPosCorr = CreateStandardHeston(stockPrice: strike * 1.05f, strike: strike, correlation: 0.9f);
        hestonPosCorr.CalculateAll();
        
        // Negative correlation should increase put values and increase put deltas (more downside volatility)  
        Assert.IsGreaterThan(hestonNoCorr.PutValue, hestonNegCorr.PutValue, $"Negative correlation should increase put value compared to no correlation. Neg: {hestonNegCorr.PutValue}, No: {hestonNoCorr.PutValue}");
        Assert.IsLessThan(hestonNegCorr.CallValue, hestonNoCorr.CallValue, $"Positive correlation should decrease call value compared to no correlation. Pos: {hestonPosCorr.CallValue}, No: {hestonNoCorr.CallValue}");
        Assert.IsLessThan(hestonNegCorr.DeltaPut, hestonNoCorr.DeltaPut, $"Negative correlation should increase put delta compared to no correlation. Neg: {hestonNegCorr.DeltaPut}, No: {hestonNoCorr.DeltaPut}");
        Assert.IsLessThan(hestonNegCorr.DeltaCall, hestonNoCorr.DeltaCall, $"Negative correlation should increase call delta compared to no correlation. Neg: {hestonNegCorr.DeltaCall}, No: {hestonNoCorr.DeltaCall}");

        // Positive correlation should decrease put values and decrease put deltas (less downside volatility)
        Assert.IsGreaterThan(hestonPosCorr.PutValue, hestonNoCorr.PutValue, $"Positive correlation should decrease put value compared to no correlation. Pos: {hestonPosCorr.PutValue}, No: {hestonNoCorr.PutValue}");
        Assert.IsLessThan(hestonNoCorr.CallValue, hestonPosCorr.CallValue, $"Positive correlation should increase call value compared to no correlation. Pos: {hestonPosCorr.CallValue}, No: {hestonNoCorr.CallValue}");
        Assert.IsLessThan(hestonNoCorr.DeltaPut, hestonPosCorr.DeltaPut, $"Positive correlation should decrease put delta compared to no correlation. Pos: {hestonPosCorr.DeltaPut}, No: {hestonNoCorr.DeltaPut}");
        Assert.IsLessThan(hestonNoCorr.DeltaCall, hestonPosCorr.DeltaCall, $"Positive correlation should decrease call delta compared to no correlation. Pos: {hestonPosCorr.DeltaCall}, No: {hestonNoCorr.DeltaCall}");
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
        heston.CalculateAll();
        var slowMRCallValue = heston.CallValue;
        
        // Fast mean reversion
        heston.VolatilityMeanReversion = 10.0f;
        heston.CalculateAll();
        var fastMRCallValue = heston.CallValue;

        Assert.IsLessThan(slowMRCallValue, fastMRCallValue, "Faster mean reversion should decrease option value when current vol is above long-term vol. Slow: {slowMRCallValue}, Fast: {fastMRCallValue}");
    }

    [TestMethod]
    public void TestHeston_VolOfVolImpact()
    {
        // 10% OTM call option
        var hestonLowVolVol = CreateStandardHeston(stockPrice: 1000, strike: 1100, correlation: 0);
        hestonLowVolVol.VolatilityOfVolatility = 0.1f;
        hestonLowVolVol.CalculateAll();

        var hestonHighVolVol = CreateStandardHeston(stockPrice: 1000, strike: 1100, correlation: 0);
        hestonHighVolVol.VolatilityOfVolatility = 0.8f;
        hestonHighVolVol.CalculateAll();

        Assert.IsGreaterThan(hestonLowVolVol.CallValue, hestonHighVolVol.CallValue, $"Higher vol of vol should increase option value. Low: {hestonLowVolVol.CallValue}, High: {hestonHighVolVol.CallValue}");
        Assert.IsGreaterThan(hestonLowVolVol.DeltaCall, hestonHighVolVol.DeltaCall, $"Higher vol of vol should increase option delta. Low: {hestonLowVolVol.DeltaCall}, High: {hestonHighVolVol.DeltaCall}");
    }

    [TestMethod]
    [DataRow(0f, 0f)]
    [DataRow(0f, 1f)]
    [DataRow(0f, 2f)]
    [DataRow(+1f, 1f)]
    [DataRow(+1f, 2.5f)]
    [DataRow(-1f, 1f)]
    [DataRow(-1f, 2.5f)]
    public void Test_Compare_RoughHeston_To_StandardHeston(float correlation, float volOfVol)
    {
        // 5% OTM call option
        var heston = CreateStandardHeston(daysLeft:10, stockPrice: 1000, strike: 1050, correlation: correlation);
        heston.VolatilityMeanReversion = 20f;
        heston.VolatilityOfVolatility = volOfVol;
        heston.CalculateAll();
        var standardCallValue = heston.CallValue;

        // 5% OTM put option
        heston.Strike = 950;
        heston.CalculateAll();
        var standardPutValue = heston.PutValue;

        heston.UseRoughHeston = true;
        heston.Strike = 1050;
        heston.CalculateAll();
        var roughCallValue = heston.CallValue;

        heston.Strike = 950;
        heston.CalculateAll();
        var roughPutValue = heston.PutValue;
        
        Assert.IsGreaterThan(standardPutValue, roughPutValue, $"Rough Heston put value should increase. Standard: {standardPutValue}, Rough: {roughPutValue}");

        if (0 < volOfVol) {
            if (correlation < 0) {
                Assert.IsGreaterThan(standardCallValue, standardPutValue, $"Calls should be worth less in negative correlation. Standard Call: {standardCallValue}, Put: {standardPutValue}");
                Assert.IsGreaterThan(roughCallValue, roughPutValue, $"Calls should be worth less in negative correlation. Standard Call: {standardCallValue}, Put: {standardPutValue}");
            }
            else if (correlation > 0) {
                Assert.IsGreaterThan(standardCallValue, roughCallValue, $"Rough Heston call value should increase. Standard: {standardCallValue}, Rough: {roughCallValue}");
                Assert.IsGreaterThan(standardPutValue, standardCallValue, $"Puts should be worth less in positive correlation. Standard Call: {standardCallValue}, Put: {standardPutValue}");
                Assert.IsGreaterThan(roughPutValue, roughCallValue, $"Puts should be worth less in positive correlation. Standard Call: {standardCallValue}, Put: {standardPutValue}");
            }
        }
    }

    [TestMethod]
    public void Test_RoughHeston_CompareToMarket() {
        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 6770f,
            Strike = 6740f,
            RiskFreeInterestRate = 0.0f,
            DaysLeft = 3,
            CurrentVolatility = 0.1375f,
            LongTermVolatility = 0.20f,
            VolatilityMeanReversion = 20f,
            VolatilityOfVolatility = 1.7f,
            Correlation = -0.8f,
            UseRoughHeston = true
        };

        heston.CalculateAll();
        var hestonCall = heston.CallValue;

        // Use BLS to get IV
        var blackScholes = new BlackNScholesCalculator {
            StockPrice = heston.StockPrice,
            Strike = heston.Strike,
            RiskFreeInterestRate = heston.RiskFreeInterestRate,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility // Use same constant vol as Heston
        };

        var blsIv = blackScholes.GetCallIVBisections(hestonCall); // Market price
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
            Assert.IsGreaterThan(0, heston.CallValue, "Should still produce positive call value");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should handle extreme but valid parameters without exception: {ex.Message}");
        }
    }

    [TestMethod]
    [DataRow(6000f, 0.30f, 0.16f, 0.01f)]
    public void TestHeston_Curve(float stockPrice, float vol, float longTermVol, float stockMove) {

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = stockPrice,
            Strike = stockPrice * (1 + stockMove),
            RiskFreeInterestRate = 0.0f,
            DaysLeft = 5,
            CurrentVolatility = vol,
            LongTermVolatility = longTermVol,
            VolatilityMeanReversion = 0f,
            VolatilityOfVolatility = 0.001f, // Very low vol of vol
            Correlation = 0f // No correlation
        };
        heston.CalculateAll();
        var hestonUpCall = heston.CallValue;
        var hestonUpPut = heston.PutValue;

        var blackScholes = new BlackNScholesCalculator {
            StockPrice = heston.StockPrice,
            Strike = heston.Strike,
            RiskFreeInterestRate = heston.RiskFreeInterestRate,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility // Use same constant vol as Heston
        };
        blackScholes.CalculateAll();
        var blsUpCall = blackScholes.CallValue;
        var blsUpPut = blackScholes.PutValue;

        Assert.IsLessThan(0.01, MathF.Abs(MathF.Log(blsUpCall / hestonUpCall)), "Call value should be close to Black-Scholes");
        Assert.IsLessThan(0.01, MathF.Abs(MathF.Log(blsUpPut / hestonUpPut)), "Put value should be close to Black-Scholes");

        // Now test downward move
        heston.Strike = stockPrice * (1 - stockMove);
        heston.CalculateAll();
        var hestonDownCall = heston.CallValue;
        var hestonDownPut = heston.PutValue;
        Assert.IsLessThan(0.02, MathF.Abs(MathF.Log(hestonDownPut / hestonUpCall)), "Put value for downward move should be close to call");

        blackScholes.Strike = heston.Strike;
        blackScholes.CalculateAll();
        var blsDownCall = blackScholes.CallValue;
        var blsDownPut = blackScholes.PutValue;
        Assert.IsLessThan(0.01, MathF.Abs(MathF.Log(blsDownCall / hestonDownCall)), "Call value should be close to Black-Scholes");
        Assert.IsLessThan(0.01, MathF.Abs(MathF.Log(blsDownPut / hestonDownPut)), "Put value should be close to Black-Scholes");

        // Add negative correlation
        heston.Correlation = -0f;
        heston.VolatilityOfVolatility = 0f;
        heston.VolatilityMeanReversion = 10f;
        heston.CalculateAll();
        var hestonDownNegCall = heston.CallValue;
        var hestonDownNegPut = heston.PutValue;

        // Negative correlation and up move
        heston.Strike = stockPrice * (1 + stockMove);
        heston.CalculateAll();
        var hestonUpNegCall = heston.CallValue;
        var hestonUpNegPut = heston.PutValue;
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_DeltaParityAlwaysHolds() {
        var strikes = new float[] { 80f, 90f, 95f, 100f, 105f, 110f, 120f };

        var heston = new HestonCalculator {
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

        foreach (var strike in strikes) {
            heston.Strike = strike;
            heston.CalculateAll();

            float parityDiff = heston.DeltaCall - heston.DeltaPut - 1.0f;
            Assert.AreEqual(0.0f, parityDiff, 0.01f,
                $"Analytical delta should satisfy put-call parity exactly for strike={strike}, got diff={parityDiff}");
        }
    }

    [TestMethod]
    [DataRow(6000f, 0.30f, 0.15f, 0.01f)]
    public void TestHeston_VolatilityMeanReversion(float stockPrice, float vol, float longTermVol, float stockMove) {

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = stockPrice,
            Strike = stockPrice * (1 + stockMove),
            RiskFreeInterestRate = 0.0f,
            CurrentVolatility = vol,
            LongTermVolatility = longTermVol,
            VolatilityMeanReversion = 30f,
            VolatilityOfVolatility = 0.001f, // Very low vol of vol
            Correlation = 0f // No correlation
        };
        var blackScholes = new BlackNScholesCalculator {
            StockPrice = heston.StockPrice,
            Strike = heston.Strike,
            RiskFreeInterestRate = heston.RiskFreeInterestRate,
            ImpliedVolatility = longTermVol // Use same constant vol as Heston
        };

        for (int daysLeft = 1; daysLeft <= 5; daysLeft++) {
            heston.DaysLeft = daysLeft;
            heston.CalculateAll();
            var hestonUpCall = heston.CallValue;

            blackScholes.DaysLeft = daysLeft;
            blackScholes.CalculateAll();
            var blsUpCall = blackScholes.CallValue;
        }
    }

    [TestMethod]
    [DataRow(6000f, 0.30f, 0.16f, 0.01f)]
    public void TestHeston_Zero_Correlation(float stockPrice, float vol, float longTermVol, float stockMove) {

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = stockPrice,
            Strike = stockPrice * (1 + stockMove),
            RiskFreeInterestRate = 0.0f,
            DaysLeft = 5,
            CurrentVolatility = vol,
            LongTermVolatility = longTermVol,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 2f,
            Correlation = 0f // No correlation
        };
        heston.CalculateAll();
        var hestonUpCall = heston.CallValue;
        var hestonUpPut = heston.PutValue;

        // Now test downward move
        heston.Strike = stockPrice * (1 - stockMove);
        heston.CalculateAll();
        var hestonDownCall = heston.CallValue;
        var hestonDownPut = heston.PutValue;

        Assert.IsLessThan(0.02, MathF.Abs(MathF.Log(hestonDownPut / hestonUpCall)), "Put value for downward move should be close to call");
        Assert.IsLessThan(0.02, MathF.Abs(MathF.Log(hestonUpCall / hestonDownPut)), "Call value for downward move should be close to put");
    }

    [TestMethod]
    [DataRow(100.0f, 100.0f, HestonIntegrationMethod.Approximation, 0.01f, 30f, 0.2f)]
    [DataRow(100.0f, 100.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(500.0f, 500.0f, HestonIntegrationMethod.Approximation, 0.01f, 30f, 0.2f)]
    [DataRow(500.0f, 500.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(1000.0f, 1000.0f, HestonIntegrationMethod.Approximation, 0.01f, 30f, 0.2f)]
    [DataRow(1000.0f, 1000.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(5000.0f, 5000.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(1000.0f, 1100.0f, HestonIntegrationMethod.Adaptive, 0.21f, 30f, 0.2f)]
    [DataRow(5000.0f, 4900.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(5000.0f, 3000.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(5000.0f, 7000.0f, HestonIntegrationMethod.Adaptive, 0.01f, 30f, 0.2f)]
    [DataRow(6715.75f, 6715.0f, HestonIntegrationMethod.Adaptive, 0.01f, 2.75f, 0.1f)]
    public void TestHeston_CompareWithBlackScholes(float stockPrice, float strike, HestonIntegrationMethod hestonIntegrationMethod,
        float expectedCallError, float daysLeft, float currentVolatility) {
        // When Heston parameters reduce to constant volatility, 
        // it should approximate Black-Scholes
        var heston = new HestonCalculator
        {
            IntegrationMethod = hestonIntegrationMethod,
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = daysLeft,
            CurrentVolatility = currentVolatility,
            LongTermVolatility = currentVolatility,
            VolatilityMeanReversion = 0f, // Very fast mean reversion
            VolatilityOfVolatility = 0.001f, // Very low vol of vol
            Correlation = 0f // No correlation
        };

        var blackScholes = new BlackNScholesCalculator
        {
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.05f,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility // Use same constant vol as Heston
        };

        heston.CalculateAll();
        blackScholes.CalculateAll();

        // Should be reasonably close
        float relativeError = heston.CallValue == blackScholes.CallValue ? 0 : MathF.Abs(heston.CallValue - blackScholes.CallValue) / blackScholes.CallValue;
        Assert.IsLessThan(expectedCallError, relativeError, 
            $"Heston should approximate Black-Scholes when vol is constant. Heston: {heston.CallValue}, BS: {blackScholes.CallValue}, Error: {relativeError:P2}");
        relativeError = heston.PutValue == blackScholes.PutValue ? 0 : MathF.Abs(heston.PutValue - blackScholes.PutValue) / blackScholes.PutValue;
        Assert.IsLessThan(0.04f, relativeError, 
            $"Heston should approximate Black-Scholes when vol is constant. Heston: {heston.PutValue}, BS: {blackScholes.PutValue}, Error: {relativeError:P2}");
    }

    [TestMethod]
    public void TestHeston_Compare_Deltas_WithBlackScholes(){
        var startPrice = 4200.0f;
        var endPrice = 5800.0f;
        var step = 10.0f;

        var heston = new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = 5000f,
            DaysLeft = 10f,
            CurrentVolatility = 0.2f,
            LongTermVolatility = 0.2f,
            VolatilityMeanReversion = 20f,
            VolatilityOfVolatility = 3f,
            Correlation = 0.6f,
        };
        var blackScholes = new BlackNScholesCalculator
        {
            StockPrice = heston.StockPrice,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility, // Use same constant vol as Heston
        };

        for (var stockPrice = startPrice; stockPrice <= endPrice; stockPrice += step) {
            heston.Strike = blackScholes.Strike = stockPrice;
            heston.CalculateAll(skipVanna: true, skipCharm: true);
            blackScholes.CalculateAll();
            Debug.WriteLine($"{stockPrice}, {blackScholes.DeltaCall}, {heston.DeltaCall}, {blackScholes.DeltaPut}, {heston.DeltaPut}");
        }
    }

    [TestMethod]
    public void TestHeston_PutCallParity_SingleStrikeMaturity()
    {
        var heston = new HestonCalculator
        {
            StockPrice = 100f,
            Strike = 100f,
            RiskFreeInterestRate = 0.03f,
            ExpiryTime = 30f/365f,
            CurrentVolatility = 0.25f,
            LongTermVolatility = 0.25f,
            VolatilityMeanReversion = 5.0f,
            VolatilityOfVolatility = 0.05f,
            Correlation = 0.0f
        };
        heston.CalculateAll();
        float discountedStrike = 100f * MathF.Exp(-0.03f * (30f/365f));
        float parityDiff = (heston.CallValue - heston.PutValue) - (heston.StockPrice - discountedStrike);
        Assert.AreEqual(0f, parityDiff, 0.01f, $"Put-call parity drift too large, diff={parityDiff}");
    }

    [TestMethod]
    [DataRow(6770f, true, 0.6f, 5f)]
    [DataRow(6770f, true, 0.5f, 5f)]
    [DataRow(6770f, true, 0.4f, 5f)]
    [DataRow(6770f, true, 0.3f, 7f)]
    [DataRow(6770f, true, 0.2f, 5f)]
    [DataRow(6770f, true, 0.1f, 5f)]
    [DataRow(6770f, true, 0.0f, 5f)]
    public void TestHeston_DeltaHedgeEffectiveness(float strike, bool isCall, float volOfVol, float volMeanRev) {
        // One day of price movements every 15 minutes
        var pricePath = new float[] {6672.14f, 6684.25f, 6704.34f, 6692.12f, 6715.72f, 6729.24f, 6739.54f, 6738.95f, 6761.74f, 6757.02f,
            6764.81f, 6770.82f, 6768.05f, 6765.44f, 6758.25f, 6758.17f, 6760.29f, 6748.79f, 6734.33f, 6750.10f, 6747.89f, 6757.31f,
            6758.81f, 6764.36f, 6754.54f, 6744.22f, 6738.4f, 6735.10f};

        var heston = new HestonCalculator {
            Strike = strike,
            StockPrice = pricePath[0],
            CurrentVolatility = 0.15f,
            LongTermVolatility = 0.20f,
            VolatilityMeanReversion = volMeanRev,
            VolatilityOfVolatility = volOfVol,
            Correlation = -0.7f,
            EnforceFellerByCappingSigma = true,
            AdaptiveUpperBoundMultiplier = 10.0f,
        };
        // Trading day with 6.5 hours
        heston.DaysLeft = 6.5f / 24f + 1f / 96f;
        var blackScholes = new BlackNScholesCalculator {
            Strike = heston.Strike,
            StockPrice = heston.StockPrice,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility, // Use same constant vol as Heston
        };
        heston.CalculateAll();

        // Buy option
        var cash = isCall ? -heston.CallValue : -heston.PutValue;
        var cashBLS = isCall ? -blackScholes.CallValue : -blackScholes.PutValue;

        // Delta hedge
        var positionInStock = -(isCall ? heston.DeltaCall : heston.DeltaPut);
        var positionInStockBLS = -(isCall ? blackScholes.DeltaCall : blackScholes.DeltaPut);
        cash += -positionInStock * heston.StockPrice;
        cashBLS += -positionInStockBLS * blackScholes.StockPrice;

        heston.VolatilityOfVolatility = volOfVol;
        //heston.CurrentVolatility = 0.3f; // Increase vol to simulate market changes
        for (int i = 1; i < pricePath.Length - 1; i++) {
            heston.StockPrice = blackScholes.StockPrice = pricePath[i];
            heston.DaysLeft -= 1f / 96f; // 15 minutes
            blackScholes.DaysLeft = heston.DaysLeft;
            heston.CalculateAll();
            blackScholes.CalculateAll();

            // Rebalance delta hedge
            var changeInStock = -(isCall ? heston.DeltaCall : heston.DeltaPut) - positionInStock;
            var changeInStockBLS = -(isCall ? blackScholes.DeltaCall : blackScholes.DeltaPut) - positionInStockBLS;

            cash -= changeInStock * heston.StockPrice;
            cashBLS -= changeInStockBLS * blackScholes.StockPrice;
            positionInStock += changeInStock;
            positionInStockBLS += changeInStockBLS;

            // Debug.WriteLine($"Time {(i * 15)} min: Stock={heston.StockPrice}, Option Value={(isCall ? heston.CallValue : heston.PutValue)}, Delta={(isCall ? heston.DeltaCall : heston.DeltaPut)}, Cash={cash}, StockPos={positionInStock} P/L={cash + positionInStock * heston.StockPrice}");
            // Debug.WriteLine($"BLS Time {(i * 15)} min: Stock={blackScholes.StockPrice}, Option Value={(isCall ? blackScholes.CallValue : blackScholes.PutValue)}, Delta={(isCall ? blackScholes.DeltaCall : blackScholes.DeltaPut)}, Cash={cashBLS}, StockPos={positionInStockBLS} P/L={cashBLS + positionInStockBLS * blackScholes.StockPrice}");
        }

        // Final settlement
        cash += positionInStock * pricePath[^1];
        cashBLS += positionInStockBLS * pricePath[^1];

        Debug.WriteLine($"Final P/L={cash:f4} BSM={cashBLS:f4} VolOfVol:{volOfVol:f2}");
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
            heston.CalculateAll();
            C[i] = heston.CallValue;
        }
        float secondDiff = C[0] - 2*C[1] + C[2];
        Assert.IsGreaterThanOrEqualTo(-0.25f, secondDiff, $"Call price should be convex in strike (within tolerance). Second diff={secondDiff}");
    }

    [TestMethod]
    public void TestHeston_AnalyticalDelta_Monotonicity() {
        // Delta should increase monotonically with stock price for fixed strike
        var stockPrices = new float[] { 80f, 90f, 95f, 100f, 105f, 110f, 120f };

        var heston = new HestonCalculator {
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
        foreach (var stockPrice in stockPrices) {
            heston.StockPrice = stockPrice;
            heston.CalculateAll();

            if (previousCallDelta > 0) {
                Assert.IsGreaterThanOrEqualTo(previousCallDelta - 0.001f, heston.DeltaCall,
                    $"Call delta should increase with stock price: {previousCallDelta} -> {heston.DeltaCall}");
            }
            previousCallDelta = heston.DeltaCall;
        }
    }
}