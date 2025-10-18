using AppCore.Statistics;

namespace AppCore.Tests.Statistics;

[TestClass]
public class HarRvForecasterTests
{
	[TestMethod]
    [DataRow(0.015)]
    [DataRow(0.010)]
    [DataRow(0.005)]
    public void Forecast_WithConstantVolatility_ShouldForecastCorrectly(double dailyVol)
	{
		// Arrange
		var expectedAnnualizedVol = dailyVol * System.Math.Sqrt(252.0);
		const int numObservations = 100;

		var forecaster = new HarRvForecaster(
			treatInputAsPrices: false,
			includeDaily: true,
			includeWeekly: true,
			includeMonthly: true,
			includeLeverageEffect: false,
			useLogVariance: false,
			ridgePenalty: 0.0);

		// Generate returns with constant volatility
		var random = new Random(42);
		for (int i = 0; i < numObservations; i++)
		{
			// Generate normally distributed returns with constant volatility (Box-Muller transform)
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * dailyVol;
			
			forecaster.AddObservation(scaledReturn);
		}

		// Act
		forecaster.Calibrate();
		var forecast = forecaster.Forecast(1.0);

		// Assert
		// With constant volatility, the forecast should be reasonably close to the expected annualized volatility
		// Allow for tolerance due to sampling variation and model estimation
		const double tolerance = 0.15; // 15% relative tolerance
		var expectedMin = expectedAnnualizedVol * (1.0 - tolerance);
		var expectedMax = expectedAnnualizedVol * (1.0 + tolerance);

		Assert.IsTrue(forecast >= expectedMin && forecast <= expectedMax,
			$"Expected forecast to be close to {expectedAnnualizedVol:F4}, but got {forecast:F4}. " +
			$"Expected range: [{expectedMin:F4}, {expectedMax:F4}]");

		// Additional checks
		Assert.IsTrue(forecaster.IsCalibrated, "Forecaster should be calibrated.");
        Assert.AreEqual(numObservations, forecaster.Count, $"Expected {numObservations} observations, but got {forecaster.Count}.");
		
		// Forecast should be positive and finite
		Assert.IsTrue(forecast > 0 && double.IsFinite(forecast), "Forecast should be positive and finite.");
	}
}
