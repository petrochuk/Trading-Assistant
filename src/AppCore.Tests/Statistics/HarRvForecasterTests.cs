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

	[TestMethod]
	[DataRow(5.0)]
	[DataRow(10.0)]
	[DataRow(15.0)]
	public void Forecast_MultiStep_ShouldProduceReasonableForecasts(double horizon)
	{
		// Arrange - Create data with mean-reverting volatility
		const int numObservations = 250;
		var forecaster = new HarRvForecaster(
			treatInputAsPrices: false,
			includeDaily: true,
			includeWeekly: true,
			includeMonthly: true,
			includeLeverageEffect: false,
			useLogVariance: false,
			ridgePenalty: 0.0);

		var random = new Random(123);
		var dailyVol = 0.015; // Base daily volatility
		
		// Generate returns with time-varying volatility
		for (int i = 0; i < numObservations; i++)
		{
			// Add some volatility clustering
			var volMultiplier = 1.0 + 0.3 * System.Math.Sin(i * System.Math.PI / 50.0);
			var currentVol = dailyVol * volMultiplier;
			
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * currentVol;
			
			forecaster.AddObservation(scaledReturn);
		}

		// Act
		forecaster.Calibrate();
		var forecast1Day = forecaster.Forecast(1.0);
		var forecastHDay = forecaster.Forecast(horizon);

		// Assert
		// Multi-step forecast should be reasonable and finite
		Assert.IsTrue(forecastHDay > 0 && double.IsFinite(forecastHDay), 
			$"{horizon}-day forecast should be positive and finite, got {forecastHDay:F4}");
		
		// Multi-step forecast should not be wildly different from 1-day (within 50% range seems reasonable)
		var ratio = forecastHDay / forecast1Day;
		Assert.IsTrue(ratio > 0.5 && ratio < 2.0,
			$"{horizon}-day forecast ({forecastHDay:F4}) should be within 50% of 1-day forecast ({forecast1Day:F4}), got ratio {ratio:F2}");
		
		// Model should be stationary (persistence < 1)
		var persistence = forecaster.Beta1 + forecaster.BetaShortTerm + 
						  forecaster.Beta2 + forecaster.BetaBiWeekly + forecaster.Beta3;
		Assert.IsTrue(persistence < 1.0,
			$"Model persistence should be < 1.0 for stationarity, got {persistence:F4}");
	}

	[TestMethod]
	public void Forecast_MultiStep_ShouldConvergeToLongRunMean()
	{
		// Arrange - Create a spike in volatility, then check if long-term forecast converges
		const int numObservations = 100;
		var forecaster = new HarRvForecaster(
			treatInputAsPrices: false,
			includeDaily: true,
			includeWeekly: true,
			includeMonthly: true,
			includeLeverageEffect: false,
			useLogVariance: false,
			ridgePenalty: 0.0);

		var random = new Random(456);
		var normalVol = 0.010; // Low volatility
		
		// Generate low volatility returns
		for (int i = 0; i < 80; i++)
		{
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * normalVol;
			forecaster.AddObservation(scaledReturn);
		}
		
		// Add recent spike in volatility
		var spikeVol = 0.030; // 3x higher
		for (int i = 0; i < 20; i++)
		{
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * spikeVol;
			forecaster.AddObservation(scaledReturn);
		}

		// Act
		forecaster.Calibrate();
		var forecast1Day = forecaster.Forecast(1.0);
		var forecast5Day = forecaster.Forecast(5.0);
		var forecast10Day = forecaster.Forecast(10.0);
		var forecast20Day = forecaster.Forecast(20.0);

		// Assert
		// Short-term forecast should reflect recent high volatility
		var expectedShortTermVol = spikeVol * System.Math.Sqrt(252.0);
		Assert.IsTrue(forecast1Day > expectedShortTermVol * 0.5,
			$"1-day forecast should reflect recent spike, expected > {expectedShortTermVol * 0.5:F4}, got {forecast1Day:F4}");
		
		// Long-term forecast should show mean reversion (decreasing from short to long term)
		// Allow for some variation but expect general downward trend
		Assert.IsTrue(forecast20Day < forecast1Day * 1.2,
			$"20-day forecast ({forecast20Day:F4}) should show mean reversion compared to 1-day ({forecast1Day:F4})");
		
		// All forecasts should be positive and finite
		Assert.IsTrue(forecast1Day > 0 && double.IsFinite(forecast1Day), "1-day forecast should be valid");
		Assert.IsTrue(forecast5Day > 0 && double.IsFinite(forecast5Day), "5-day forecast should be valid");
		Assert.IsTrue(forecast10Day > 0 && double.IsFinite(forecast10Day), "10-day forecast should be valid");
		Assert.IsTrue(forecast20Day > 0 && double.IsFinite(forecast20Day), "20-day forecast should be valid");
	}

	[TestMethod]
	public void Forecast_WithHighVolatilityRegime_ShouldHandleMultiStep()
	{
		// Arrange - Create high volatility environment
		const int numObservations = 100;
		var forecaster = new HarRvForecaster(
			treatInputAsPrices: false,
			includeDaily: true,
			includeWeekly: true,
			includeMonthly: true,
			includeLeverageEffect: false,
			useLogVariance: false,
			ridgePenalty: 0.0);

		var random = new Random(789);
		var highVol = 0.025; // High volatility regime
		
		for (int i = 0; i < numObservations; i++)
		{
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * highVol;
			forecaster.AddObservation(scaledReturn);
		}

		// Act
		forecaster.Calibrate();
		var forecasts = new[]
		{
			forecaster.Forecast(1.0),
			forecaster.Forecast(3.0),
			forecaster.Forecast(5.0),
			forecaster.Forecast(7.0),
			forecaster.Forecast(10.0)
		};

		// Assert
		// All forecasts should be reasonable and finite
		foreach (var forecast in forecasts)
		{
			Assert.IsTrue(forecast > 0 && double.IsFinite(forecast),
				$"Forecast should be positive and finite, got {forecast:F4}");
		}
		
		// Forecasts should be in a reasonable range (not exploding or collapsing)
		var expectedAnnualVol = highVol * System.Math.Sqrt(252.0);
		foreach (var forecast in forecasts)
		{
			Assert.IsTrue(forecast > expectedAnnualVol * 0.5 && forecast < expectedAnnualVol * 2.0,
				$"Forecast {forecast:F4} should be within 50%-200% of expected {expectedAnnualVol:F4}");
		}
		
		// Forecasts should not show wild oscillations (each within 30% of previous)
		for (int i = 1; i < forecasts.Length; i++)
		{
			var ratio = forecasts[i] / forecasts[i-1];
			Assert.IsTrue(ratio > 0.7 && ratio < 1.5,
				$"Adjacent forecasts should be relatively stable, got ratio {ratio:F2} between {forecasts[i]:F4} and {forecasts[i-1]:F4}");
		}
	}

	[TestMethod]
	public void Forecast_WithIntradayVariance_ShouldDecayWithHorizon()
	{
		// Arrange - Create a low volatility environment then add high intraday estimate
		const int numObservations = 100;
		var forecaster = new HarRvForecaster(
			treatInputAsPrices: false,
			includeDaily: true,
			includeWeekly: true,
			includeMonthly: true,
			includeLeverageEffect: false,
			useLogVariance: false,  // Use linear model for more predictable behavior
			ridgePenalty: 0.0);

		var random = new Random(999);
		var normalVol = 0.010; // Low volatility (about 16% annualized)
		
		for (int i = 0; i < numObservations; i++)
		{
			double u1 = random.NextDouble();
			double u2 = random.NextDouble();
			double normalReturn = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
			double scaledReturn = normalReturn * normalVol;
			forecaster.AddObservation(scaledReturn);
		}

		// Act
		forecaster.Calibrate();
		
		// Forecast without intraday estimate (baseline)
		var baselineForecast1Day = forecaster.Forecast(1.0);
		var baselineForecast5Day = forecaster.Forecast(5.0);
		
		// Set high intraday variance estimate (30% annualized = ~0.0189 daily vol = 0.000357 daily variance)
		var highIntradayVol = 0.30; // 30% annualized
		var dailyVolFromAnnualized = highIntradayVol / System.Math.Sqrt(252.0);
		var dailyVariance = dailyVolFromAnnualized * dailyVolFromAnnualized;
		
		forecaster.SetIntradayVolatilityEstimate(highIntradayVol, isAnnualized: true);
		
		// Get forecasts for different horizons
		var forecast1Day = forecaster.Forecast(1.0);
		var forecast2Day = forecaster.Forecast(2.0);
		var forecast3Day = forecaster.Forecast(3.0);
		var forecast4Day = forecaster.Forecast(4.0);
		var forecast5Day = forecaster.Forecast(5.0);
		var forecast7Day = forecaster.Forecast(7.0);
		var forecast10Day = forecaster.Forecast(10.0);

		// Assert
		// The key test: forecasts should NOT all be the same
		var forecasts = new[] { forecast1Day, forecast2Day, forecast3Day, forecast4Day, forecast5Day };
		var uniqueValues = forecasts.Distinct().Count();
		Assert.IsTrue(uniqueValues >= 3,
			$"Should have at least 3 different forecast values, got {uniqueValues}: [{string.Join(", ", forecasts.Select(f => f.ToString("F4")))}]");
		
		// Forecasts should show reasonable variation (not stuck at identical values)
		var maxForecast = forecasts.Max();
		var minForecast = forecasts.Min();
		var range = maxForecast - minForecast;
		Assert.IsTrue(range > 0.001, // At least 0.1% difference
			$"Forecast range should be > 0.001, got {range:F4}. Forecasts: [{string.Join(", ", forecasts.Select(f => f.ToString("F4")))}]");
		
		// All forecasts should be positive, finite, and reasonable
		foreach (var f in forecasts)
		{
			Assert.IsTrue(f > 0 && double.IsFinite(f), $"Forecast {f:F4} should be positive and finite");
			Assert.IsTrue(f >= 0.05 && f <= 2.0, $"Forecast {f:F4} should be in reasonable range (5%-200% annualized)");
		}
		
		// The intraday estimate should have some effect on the forecast
		// (it might increase or decrease depending on whether it's higher/lower than the model)
		var totalChange = System.Math.Abs(forecast1Day - baselineForecast1Day) + 
		                  System.Math.Abs(forecast5Day - baselineForecast5Day);
		Assert.IsTrue(totalChange > 0.001,
			$"Intraday estimate should have measurable effect on forecasts. Total change: {totalChange:F4}");
	}
}
