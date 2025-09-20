using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class VarianceGammaCalculatorTests
{
    [TestMethod]
    [DataRow(100.0f, 100.0f)]
    [DataRow(500.0f, 500.0f)]
    [DataRow(1000.0f, 1000.0f)]
    [DataRow(5000.0f, 5000.0f)]
    [DataRow(5000.0f, 4900.0f)]
    [DataRow(5000.0f, 3000.0f)]
    [DataRow(5000.0f, 7000.0f)]
    public void VarianceGammaCalculator_Should_Approximate_BlackScholes(float stockPrice, float strike) {
        var blackScholes = new BlackNScholesCaculator {
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = 0.0f,
            DaysLeft = 30.0f,
            ImpliedVolatility = 0.2f
        };

        var vg = new VarianceGammaCalculator {
            StockPrice = stockPrice,
            Strike = strike,
            RiskFreeInterestRate = blackScholes.RiskFreeInterestRate,
            DaysLeft = blackScholes.DaysLeft,
            Volatility = blackScholes.ImpliedVolatility,
            VarianceRate = 0.0f, // Set variance rate to 0 to approximate BS
            DriftParameter = 0f // Set drift parameter to 0 to approximate BS
        };

        blackScholes.CalculateAll();
        vg.CalculateAll();

        Assert.AreEqual(blackScholes.CallValue, vg.CallValue, blackScholes.CallValue * 0.05f, $"Call values should be approximately equal for stockPrice={stockPrice}, strike={strike}");
        Assert.AreEqual(blackScholes.PutValue, vg.PutValue, blackScholes.PutValue * 0.05f, $"Put values should be approximately equal for stockPrice={stockPrice}, strike={strike}");

        Assert.AreEqual(blackScholes.DeltaCall, vg.DeltaCall, 0.05f, $"Call deltas should be approximately equal for stockPrice={stockPrice}, strike={strike}");
        Assert.AreEqual(blackScholes.DeltaPut, vg.DeltaPut, 0.05f, $"Put deltas should be approximately equal for stockPrice={stockPrice}, strike={strike}");
    }

    [TestMethod]
    public void VarianceGammaCalculator_BasicCalculation_ShouldProduceReasonableResults()
    {
        // Arrange
        var vgCalculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            ExpiryTime = 1.0f, // 1 year
            Volatility = 0.2f,
            VarianceRate = 0.5f,
            DriftParameter = -0.1f
        };

        // Act
        vgCalculator.CalculateAll();

        // Assert
        Assert.IsTrue(vgCalculator.CallValue > 0, "Call value should be positive");
        Assert.IsTrue(vgCalculator.PutValue > 0, "Put value should be positive");
        Assert.IsTrue(vgCalculator.CallValue > vgCalculator.PutValue, "Call value should be higher than put value for ATM option with negative drift");
        
        // Verify Greeks are calculated
        Assert.IsTrue(vgCalculator.DeltaCall > 0 && vgCalculator.DeltaCall < 1, "Call delta should be between 0 and 1");
        Assert.IsTrue(vgCalculator.DeltaPut < 0 && vgCalculator.DeltaPut > -1, "Put delta should be between -1 and 0");
        Assert.IsTrue(vgCalculator.Gamma > 0, "Gamma should be positive");
        Assert.IsTrue(vgCalculator.VegaCall > 0, "Vega should be positive");
    }

    [TestMethod]
    public void VarianceGammaCalculator_HighVarianceRate_ShouldIncreaseOptionValues()
    {
        // Arrange
        var baseCalculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            ExpiryTime = 0.25f, // 3 months
            Volatility = 0.15f,
            VarianceRate = 0.2f,
            DriftParameter = -0.02f
        };

        var highNuCalculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            Strike = 100f,
            RiskFreeInterestRate = 0.05f,
            ExpiryTime = 0.25f, // 3 months
            Volatility = 0.15f,
            VarianceRate = 2.0f, // Much higher variance rate
            DriftParameter = -0.02f
        };

        // Act
        baseCalculator.CalculateCallPut();
        highNuCalculator.CalculateCallPut();

        // Assert
        Assert.IsTrue(highNuCalculator.CallValue > baseCalculator.CallValue, 
            "Higher variance rate should increase call option values");
        Assert.IsTrue(highNuCalculator.PutValue > baseCalculator.PutValue, 
            "Higher variance rate should increase put option values");
    }

    [TestMethod]
    public void VarianceGammaCalculator_NegativeDrift_ShouldFavorPuts()
    {
        // Arrange
        var positiveDriftCalculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            Strike = 105f, // OTM put
            RiskFreeInterestRate = 0.05f,
            ExpiryTime = 0.1f, // Short term
            Volatility = 0.2f,
            VarianceRate = 1.0f,
            DriftParameter = 0.05f // Positive drift
        };

        var negativeDriftCalculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            Strike = 105f, // OTM put
            RiskFreeInterestRate = 0.05f,
            ExpiryTime = 0.1f, // Short term
            Volatility = 0.2f,
            VarianceRate = 1.0f,
            DriftParameter = -0.05f // Negative drift
        };

        // Act
        positiveDriftCalculator.CalculateCallPut();
        negativeDriftCalculator.CalculateCallPut();

        // Assert
        Assert.IsTrue(negativeDriftCalculator.PutValue > positiveDriftCalculator.PutValue, 
            "Negative drift should increase put option values relative to positive drift");
    }

    [TestMethod]
    public void VarianceGammaCalculator_DaysLeftProperty_ShouldSetExpiryTime()
    {
        // Arrange
        var calculator = new VarianceGammaCalculator();
        
        // Act
        calculator.DaysLeft = 30f;

        // Assert
        Assert.AreEqual(30f / 365f, calculator.ExpiryTime, 0.001f, "DaysLeft should correctly set ExpiryTime");
    }

    [TestMethod]
    public void VarianceGammaCalculator_Calibration_ShouldCompleteWithoutErrors()
    {
        // Arrange
        var calculator = new VarianceGammaCalculator
        {
            StockPrice = 100f,
            RiskFreeInterestRate = 0.05f,
            Volatility = 0.2f,
            VarianceRate = 1.0f,
            DriftParameter = -0.02f
        };

        var strikes = new float[] { 95f, 100f, 105f };
        var expiries = new float[] { 30f, 30f, 30f };
        var marketPrices = new float[] { 2.5f, 5.0f, 8.5f };

        // Act & Assert - Should not throw
        calculator.CalibrateToMarketPrices(marketPrices, strikes, expiries);
        
        // Verify parameters are still reasonable
        Assert.IsTrue(calculator.Volatility > 0, "Volatility should remain positive after calibration");
        Assert.IsTrue(calculator.VarianceRate > 0, "Variance rate should remain positive after calibration");
    }
}