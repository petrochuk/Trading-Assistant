using AppCore.Interfaces;
using System.Globalization;

namespace AppCore.Statistics;

/// <summary>
/// HAR-RV (Heterogeneous AutoRegressive Realized Volatility) forecaster.
/// The model is calibrated with daily log returns that can be populated manually or
/// read from a file. The implementation purposefully keeps the numerical linear algebra
/// lightweight (normal equations + Gaussian elimination) to avoid external dependencies.
/// </summary>
public sealed class HarRvForecaster : IVolForecaster
{
	private const int WeeklyWindow = 5;
	private const int MonthlyWindow = 22;
	private const int ShortWindow = 3;
	private const int BiWeeklyWindow = 10;
	private const double DefaultYangZhangK = 0.34 / (1.34 + (79.0 / 77.0));
	private readonly bool _treatInputAsPrices;
	private readonly double _minVariance;
	private readonly bool _includeDaily;
	private readonly bool _includeShortTerm;
	private readonly bool _includeWeekly;
	private readonly bool _includeBiWeekly;
	private readonly bool _includeMonthly;
	private readonly bool _includeLeverageEffect;
	private readonly bool _useLogVariance;
	private readonly double _ridgePenalty;
	private double? _intradayVarianceOverride;
	private double? _intradayReturnOverride;
	private double _logResidualVariance;

	private readonly List<double> _returns = new();
	private readonly List<double> _realizedVariances = new();
	private List<double[]>? _designMatrix;
	private List<double>? _targets;
	private double[]? _coefficients;
	private bool _isCalibrated;

	public HarRvForecaster(
		bool treatInputAsPrices = true,
		double minimumVariance = 1e-10,
		bool includeDaily = true,
		bool includeWeekly = true,
		bool includeMonthly = true,
		bool includeLeverageEffect = false,
		bool useLogVariance = false,
		double ridgePenalty = 0.0,
		bool includeShortTerm = true,
		bool includeBiWeekly = true)
	{
		if (minimumVariance < 0)
			throw new ArgumentOutOfRangeException(nameof(minimumVariance), "Minimum variance must be non-negative.");
		
		if (ridgePenalty < 0)
			throw new ArgumentOutOfRangeException(nameof(ridgePenalty), "Ridge penalty must be non-negative.");

		_treatInputAsPrices = treatInputAsPrices;
		_minVariance = minimumVariance;
		_includeDaily = includeDaily;
		_includeWeekly = includeWeekly;
		_includeMonthly = includeMonthly;
		_includeLeverageEffect = includeLeverageEffect;
		_useLogVariance = useLogVariance;
		_ridgePenalty = ridgePenalty;
		_includeShortTerm = includeShortTerm;
		_includeBiWeekly = includeBiWeekly;
	}

	public string Symbol { get; set; } = string.Empty;

    /// <summary>Number of daily returns currently stored.</summary>
    public int Count => _returns.Count;

	/// <summary>True once <see cref="Calibrate"/> has been called successfully.</summary>
	public bool IsCalibrated => _isCalibrated;

	/// <summary>Intercept coefficient.</summary>
	public double Beta0 { get; private set; }

	/// <summary>Daily realized variance coefficient.</summary>
	public double Beta1 { get; private set; }

	/// <summary>3-day average realized variance coefficient.</summary>
	public double BetaShortTerm { get; private set; }

	/// <summary>Weekly average realized variance coefficient.</summary>
	public double Beta2 { get; private set; }

	/// <summary>10-day average realized variance coefficient.</summary>
	public double BetaBiWeekly { get; private set; }

	/// <summary>Monthly average realized variance coefficient.</summary>
	public double Beta3 { get; private set; }

	/// <summary>Optional leverage term coefficient.</summary>
	public double BetaLeverage { get; private set; }

	/// <summary>
	/// Adds a single daily log return observation to the internal sample.
	/// </summary>
	public void AddObservation(double dailyLogReturn, double? realizedVariance = null)
	{
		if (!double.IsFinite(dailyLogReturn))
			throw new ArgumentOutOfRangeException(nameof(dailyLogReturn), "Return must be a finite number.");

		_returns.Add(dailyLogReturn);
		var variance = realizedVariance ?? dailyLogReturn * dailyLogReturn;
		if (!double.IsFinite(variance) || variance < 0)
			throw new ArgumentOutOfRangeException(nameof(realizedVariance), "Realized variance must be non-negative and finite when provided.");

		_realizedVariances.Add(variance);
		_isCalibrated = false;
	}

	/// <summary>
	/// Calibrates the HAR-RV coefficients using the currently stored returns.
	/// </summary>
	public void Calibrate()
	{
		var longestWindow = 1;
		if (_includeShortTerm)
			longestWindow = System.Math.Max(longestWindow, ShortWindow);
		if (_includeWeekly)
			longestWindow = System.Math.Max(longestWindow, WeeklyWindow);
		if (_includeBiWeekly)
			longestWindow = System.Math.Max(longestWindow, BiWeeklyWindow);
		if (_includeMonthly)
			longestWindow = System.Math.Max(longestWindow, MonthlyWindow);

		if (_returns.Count < longestWindow + 1)
			throw new InvalidOperationException($"Not enough observations to calibrate HAR-RV. Need at least {longestWindow + 1} returns.");

		ClearIntradayEstimate();

		Beta0 = 0.0;
		Beta1 = 0.0;
		BetaShortTerm = 0.0;
		Beta2 = 0.0;
		BetaBiWeekly = 0.0;
		Beta3 = 0.0;
		BetaLeverage = 0.0;
		_logResidualVariance = 0.0;

		EnsureRealizedVarianceConsistency();
		var realizedVariance = _realizedVariances.ToList();

		var shortQueue = new Queue<double>();
		var weeklyQueue = new Queue<double>();
		var biWeeklyQueue = new Queue<double>();
		var monthlyQueue = new Queue<double>();
		double shortSum = 0;
		double weeklySum = 0;
		double biWeeklySum = 0;
		double monthlySum = 0;

		_designMatrix = new List<double[]>();
		_targets = new List<double>();

		for (int i = 0; i < realizedVariance.Count - 1; i++)
		{
			var currentRv = realizedVariance[i];

			shortQueue.Enqueue(currentRv);
			shortSum += currentRv;
			if (shortQueue.Count > ShortWindow)
				shortSum -= shortQueue.Dequeue();

			weeklyQueue.Enqueue(currentRv);
			weeklySum += currentRv;
			if (weeklyQueue.Count > WeeklyWindow)
				weeklySum -= weeklyQueue.Dequeue();

			biWeeklyQueue.Enqueue(currentRv);
			biWeeklySum += currentRv;
			if (biWeeklyQueue.Count > BiWeeklyWindow)
				biWeeklySum -= biWeeklyQueue.Dequeue();

			monthlyQueue.Enqueue(currentRv);
			monthlySum += currentRv;
			if (monthlyQueue.Count > MonthlyWindow)
				monthlySum -= monthlyQueue.Dequeue();

			var hasShort = !_includeShortTerm || shortQueue.Count == ShortWindow;
			var hasWeekly = !_includeWeekly || weeklyQueue.Count == WeeklyWindow;
			var hasBiWeekly = !_includeBiWeekly || biWeeklyQueue.Count == BiWeeklyWindow;
			var hasMonthly = !_includeMonthly || monthlyQueue.Count == MonthlyWindow;

			if (hasShort && hasWeekly && hasBiWeekly && hasMonthly)
			{
				var nextIndex = i + 1;
				if (nextIndex >= realizedVariance.Count)
					break;

				var shortAverage = _includeShortTerm ? shortSum / ShortWindow : 0.0;
				var weeklyAverage = _includeWeekly ? weeklySum / WeeklyWindow : 0.0;
				var biWeeklyAverage = _includeBiWeekly ? biWeeklySum / BiWeeklyWindow : 0.0;
				var monthlyAverage = _includeMonthly ? monthlySum / MonthlyWindow : 0.0;
				var featureVector = CreateFeatureVector(currentRv, shortAverage, weeklyAverage, biWeeklyAverage, monthlyAverage, _returns[i]);

				_designMatrix.Add(featureVector);
				
				// Transform target to log-space if enabled
				var target = realizedVariance[nextIndex];
				if (_useLogVariance)
					target = System.Math.Log(System.Math.Max(target, _minVariance));
				
				_targets.Add(target);
			}
		}

		if (_designMatrix.Count == 0)
			throw new InvalidOperationException("Not enough overlapping windows to estimate the HAR-RV model.");

		var coefficients = SolveNormalEquations(_designMatrix, _targets);
		_coefficients = coefficients;
		AssignCoefficients(coefficients);

		if (_useLogVariance)
		{
			double sse = 0.0;
			for (int i = 0; i < _targets.Count; i++)
			{
				var predicted = Dot(coefficients, _designMatrix[i]);
				var residual = _targets[i] - predicted;
				sse += residual * residual;
			}

			var dof = _targets.Count - FeatureCount;
			if (dof > 0)
				_logResidualVariance = sse / dof;
			else if (_targets.Count > 0)
				_logResidualVariance = sse / _targets.Count;
			else
				_logResidualVariance = 0.0;

			if (!double.IsFinite(_logResidualVariance) || _logResidualVariance < 0)
				_logResidualVariance = 0.0;
		}
		else
		{
			_logResidualVariance = 0.0;
		}
		_isCalibrated = true;
	}

	/// <summary>
	/// Loads prices or returns from a file, computes daily log returns when needed and calibrates the model.
	/// </summary>
	public void CalibrateFromFile(string filePath, int skipLines = 0)
	{
		LoadReturnsFromFile(filePath, skipLines);
		Calibrate();
	}

	/// <summary>
	/// Loads prices from a file and calibrates the HAR-RV model using the same logic as <see cref="CalibrateFromFile"/>.
	/// </summary>
	public void LoadFromFileWithRollingRV(string filePath, int skipLines = 0)
	{
		LoadReturnsFromFile(filePath, skipLines);
		Calibrate();
	}

	/// <summary>
	/// Generates a volatility forecast for a specified horizon (in days) and returns annualized volatility.
	/// When intraday overrides are available and the horizon is less than one day, the realized intraday estimate is
	/// blended with the model-implied variance so partial-day requests respond to the intraday signal. For horizons > 1 day,
	/// uses direct multi-step forecasting based on HAR-RV structure.
	/// </summary>
	/// <param name="forecastHorizonDays">Forecast horizon in trading days (e.g., 1.0, 3.5, 4.16)</param>
	/// <returns>Annualized volatility forecast</returns>
	public double Forecast(double forecastHorizonDays)
	{
		if (!_isCalibrated || _coefficients == null)
			throw new InvalidOperationException("Model must be calibrated before forecasting.");

		if (forecastHorizonDays <= 0)
			throw new ArgumentOutOfRangeException(nameof(forecastHorizonDays), "Forecast horizon must be positive.");

		EnsureRealizedVarianceConsistency();
		var realizedVariance = _realizedVariances.ToList();

		// For multi-day forecasts with intraday override, use historical data for features
		// but incorporate the intraday info in the forecast calculation
		var effectiveVarianceSeries = new List<double>(realizedVariance);
		double leverageReturn = 0.0;
		
		// Only add intraday variance to series for single-day forecasts or intraday-blended forecasts
		bool useIntradayInFeatures = _intradayVarianceOverride.HasValue && forecastHorizonDays <= 1.0;
		
		if (useIntradayInFeatures)
		{
			var clamped = ClampVariance(_intradayVarianceOverride.Value);
			effectiveVarianceSeries.Add(clamped);
			leverageReturn = _includeLeverageEffect ? (_intradayReturnOverride ?? 0.0) : 0.0;
		}
		else if (_includeLeverageEffect && _returns.Count > 0)
		{
			leverageReturn = _returns[^1];
		}

		var requiredObservations = 1;
		if (_includeShortTerm)
			requiredObservations = System.Math.Max(requiredObservations, ShortWindow);
		if (_includeWeekly)
			requiredObservations = System.Math.Max(requiredObservations, WeeklyWindow);
		if (_includeBiWeekly)
			requiredObservations = System.Math.Max(requiredObservations, BiWeeklyWindow);
		if (_includeMonthly)
			requiredObservations = System.Math.Max(requiredObservations, MonthlyWindow);

		var availableObservations = effectiveVarianceSeries.Count;
		if (availableObservations < requiredObservations)
			throw new InvalidOperationException($"Not enough data to compute HAR-RV features for forecasting. Need at least {requiredObservations} returns.");

		// For horizon <= 1 day, use single-step prediction (with optional intraday blending)
		if (forecastHorizonDays <= 1.0)
		{
			var dailyRv = effectiveVarianceSeries[^1];
			var shortRv = _includeShortTerm ? effectiveVarianceSeries.TakeLast(ShortWindow).Average() : 0.0;
			var weeklyRv = _includeWeekly ? effectiveVarianceSeries.TakeLast(WeeklyWindow).Average() : 0.0;
			var biWeeklyRv = _includeBiWeekly ? effectiveVarianceSeries.TakeLast(BiWeeklyWindow).Average() : 0.0;
			var monthlyRv = _includeMonthly ? effectiveVarianceSeries.TakeLast(MonthlyWindow).Average() : 0.0;
			var features = CreateFeatureVector(dailyRv, shortRv, weeklyRv, biWeeklyRv, monthlyRv, leverageReturn);

			var predictedVariance = Dot(_coefficients, features);
			
			// Transform back from log-space if needed
			if (_useLogVariance)
				predictedVariance = System.Math.Exp(predictedVariance + 0.5 * _logResidualVariance);
			
			if (predictedVariance < _minVariance)
				predictedVariance = _minVariance;

			if (_intradayVarianceOverride.HasValue && forecastHorizonDays < 1.0)
			{
				var horizon = System.Math.Max(0.0, System.Math.Min(forecastHorizonDays, 1.0));
				var realizedFraction = 1.0 - horizon;
				var combinedVariance = realizedFraction * _intradayVarianceOverride.Value + horizon * predictedVariance;
				predictedVariance = combinedVariance < _minVariance ? _minVariance : combinedVariance;
			}

			return System.Math.Sqrt(predictedVariance * 252.0);
		}
		
		// For multi-day forecasts, use historical variance series (without intraday override)
		// but adjust the 1-day ahead baseline if intraday info is available
		return ForecastMultiStep(realizedVariance, forecastHorizonDays, leverageReturn, _intradayVarianceOverride);
	}

	/// <summary>
	/// Performs multi-step ahead forecast using direct h-step formulas based on HAR-RV structure.
	/// For h-day ahead forecasts, uses a combination of short-term dynamics and long-run convergence.
	/// Returns the average annualized volatility over the forecast horizon.
	/// </summary>
	private double ForecastMultiStep(List<double> initialRv, double horizonDays, double leverageReturn, double? intradayVariance = null)
	{
		// For multi-step forecasts, we use direct h-step ahead predictions
		// The HAR-RV model naturally provides different dynamics based on horizon:
		// - Short horizons (1-5 days): Use recent variance components with full model
		// - Medium horizons (5-20 days): Blend between recent dynamics and long-run mean
		// - Long horizons (>20 days): Converge to unconditional mean implied by model
		
		var dailyRv = initialRv[^1];
		var shortRv = _includeShortTerm ? initialRv.TakeLast(ShortWindow).Average() : 0.0;
		var weeklyRv = _includeWeekly ? initialRv.TakeLast(WeeklyWindow).Average() : 0.0;
		var biWeeklyRv = _includeBiWeekly ? initialRv.TakeLast(BiWeeklyWindow).Average() : 0.0;
		var monthlyRv = _includeMonthly ? initialRv.TakeLast(MonthlyWindow).Average() : 0.0;
		
		// Calculate 1-step ahead baseline using historical data
		var features = CreateFeatureVector(dailyRv, shortRv, weeklyRv, biWeeklyRv, monthlyRv, leverageReturn);
		var oneDayAhead = Dot(_coefficients, features);
		
		if (_useLogVariance)
			oneDayAhead = System.Math.Exp(oneDayAhead + 0.5 * _logResidualVariance);
		
		oneDayAhead = System.Math.Max(oneDayAhead, _minVariance);
		
		// If we have intraday variance info, adjust the starting point for short-term forecasts
		// The intraday signal should decay over the forecast horizon
		if (intradayVariance.HasValue)
		{
			// Blend intraday estimate with model forecast for the first day
			// This gives us a better starting point that incorporates current market conditions
			var intradayWeight = System.Math.Exp(-horizonDays / 2.0); // Decays with horizon
			oneDayAhead = oneDayAhead * (1.0 - intradayWeight) + intradayVariance.Value * intradayWeight;
		}
		
		// Calculate unconditional mean variance (long-run equilibrium)
		// E[RV] = β₀ / (1 - β₁ - β₂ - β₃ - ...)
		var persistence = 0.0;
		if (_includeDaily) persistence += Beta1;
		if (_includeShortTerm) persistence += BetaShortTerm;
		if (_includeWeekly) persistence += Beta2;
		if (_includeBiWeekly) persistence += BetaBiWeekly;
		if (_includeMonthly) persistence += Beta3;
		
		// Ensure stationarity and handle edge cases
		persistence = System.Math.Max(0.0, System.Math.Min(persistence, 0.999));
		
		double unconditionalMean;
		if (_useLogVariance)
		{
			// For log-variance models, the unconditional mean requires special handling
			// E[log(RV)] = β₀ / (1 - persistence) in log-space
			// Then transform to variance space
			var logUnconditionalMean = Beta0 / (1.0 - persistence);
			unconditionalMean = System.Math.Exp(logUnconditionalMean + 0.5 * _logResidualVariance);
		}
		else
		{
			// For linear models
			if (persistence < 0.999)
			{
				unconditionalMean = Beta0 / (1.0 - persistence);
			}
			else
			{
				// If persistence is very high, use current level as proxy
				unconditionalMean = oneDayAhead;
			}
		}
		
		// Ensure unconditional mean is valid
		if (!double.IsFinite(unconditionalMean) || unconditionalMean <= 0)
		{
			unconditionalMean = oneDayAhead;
		}
		
		unconditionalMean = System.Math.Max(unconditionalMean, _minVariance);
		
		// Use geometric decay to blend between current forecast and long-run mean
		// The decay rate depends on the persistence of the model
		double avgVariance;
		
		if (horizonDays <= BiWeeklyWindow) // Extend short horizon treatment to 10 days
		{
			// Short horizon: Use weighted average of component-specific forecasts
			avgVariance = ComputeShortHorizonForecast(
				oneDayAhead, dailyRv, shortRv, weeklyRv, biWeeklyRv, monthlyRv, 
				unconditionalMean, persistence, horizonDays);
		}
		else if (horizonDays <= MonthlyWindow)
		{
			// Medium horizon: Exponential decay from current to unconditional mean
			var decayRate = System.Math.Pow(persistence, horizonDays / BiWeeklyWindow);
			avgVariance = unconditionalMean + (oneDayAhead - unconditionalMean) * decayRate;
		}
		else
		{
			// Long horizon: Mostly converged to unconditional mean, but retain some short-term info
			var decayRate = System.Math.Pow(persistence, horizonDays / MonthlyWindow);
			avgVariance = unconditionalMean + (oneDayAhead - unconditionalMean) * decayRate;
		}
		
		// Ensure result is valid
		if (!double.IsFinite(avgVariance) || avgVariance <= 0)
		{
			avgVariance = oneDayAhead;
		}
		
		avgVariance = System.Math.Max(avgVariance, _minVariance);
		
		// Return annualized volatility
		return System.Math.Sqrt(avgVariance * 252.0);
	}

	/// <summary>
	/// Computes forecast for short horizons (1-10 days) using component-specific dynamics.
	/// </summary>
	private double ComputeShortHorizonForecast(
		double oneDayAhead,
		double dailyRv, 
		double shortRv, 
		double weeklyRv, 
		double biWeeklyRv,
		double monthlyRv,
		double unconditionalMean,
		double persistence,
		double horizonDays)
	{
		// For log-variance models, we need to work in log-space for proper forecasting
		// For linear models, we can work directly with variances
		
		if (_useLogVariance)
		{
			// Log-variance model: interpolate in log-space, then transform
			var logOneDayAhead = System.Math.Log(System.Math.Max(oneDayAhead, _minVariance));
			var logUnconditionalMean = System.Math.Log(System.Math.Max(unconditionalMean, _minVariance));
			
			// Decay toward unconditional mean in log-space
			var decayRate = System.Math.Pow(persistence, horizonDays / WeeklyWindow);
			var logForecast = logUnconditionalMean + (logOneDayAhead - logUnconditionalMean) * decayRate;
			
			// Transform back to variance space
			var varianceForecast = System.Math.Exp(logForecast);
			return System.Math.Max(varianceForecast, _minVariance);
		}
		
		// Linear model: use component-weighted approach
		double componentForecast = 0.0;
		
		// Start with intercept
		componentForecast = Beta0;
		
		// Daily component - most relevant for very short horizons
		if (_includeDaily && Beta1 > 0)
		{
			var weight = System.Math.Exp(-horizonDays / 2.5);
			componentForecast += Beta1 * weight * dailyRv;
		}
		
		// Short-term component (3-day)
		if (_includeShortTerm && BetaShortTerm > 0)
		{
			var weight = System.Math.Exp(-System.Math.Abs(horizonDays - ShortWindow) / 3.0);
			componentForecast += BetaShortTerm * weight * shortRv;
		}
		
		// Weekly component - increasingly relevant as horizon approaches 5 days
		if (_includeWeekly && Beta2 > 0)
		{
			var weight = horizonDays >= 3.0 ? 1.0 : horizonDays / 3.0;
			componentForecast += Beta2 * weight * weeklyRv;
		}
		
		// Bi-weekly component - relevant for horizons 5-10
		if (_includeBiWeekly && BetaBiWeekly > 0 && horizonDays >= 5.0)
		{
			var weight = System.Math.Min((horizonDays - 4.0) / 6.0, 1.0);
			componentForecast += BetaBiWeekly * weight * biWeeklyRv;
		}
		
		// Monthly component - minimal influence on short horizons
		if (_includeMonthly && Beta3 > 0 && horizonDays >= 8.0)
		{
			var weight = System.Math.Min((horizonDays - 8.0) / 14.0, 0.5);
			componentForecast += Beta3 * weight * monthlyRv;
		}
		
		// Ensure we have a valid forecast
		if (!double.IsFinite(componentForecast) || componentForecast <= 0)
		{
			return oneDayAhead;
		}
		
		// For very short horizons, stay close to 1-day ahead
		// For longer short horizons, allow more deviation but blend toward unconditional mean
		var blendToOneDayAhead = System.Math.Exp(-horizonDays / 4.0);
		var blendToUnconditional = horizonDays > BiWeeklyWindow ? 0.2 : 0.0;
		
		var result = componentForecast * (1.0 - blendToOneDayAhead - blendToUnconditional) 
		             + oneDayAhead * blendToOneDayAhead
		             + unconditionalMean * blendToUnconditional;
		
		// Final validation
		if (!double.IsFinite(result) || result <= 0)
		{
			return oneDayAhead;
		}
		
		return System.Math.Max(result, _minVariance);
	}
 
	/// <summary>
	/// Applies an intraday realized variance estimate (in daily variance units) for the current trading day.
	/// The estimate is appended to the historical series for forecasting without requiring model recalibration.
	/// </summary>
	/// <param name="dailyVariance">Daily realized variance estimate derived from intraday data.</param>
	/// <param name="currentLogReturn">Optional log return from the previous close to the current price (used when leverage effect is enabled).</param>
	public void SetIntradayVarianceEstimate(double dailyVariance, double? currentLogReturn = null)
	{
		if (!_isCalibrated)
			throw new InvalidOperationException("Model must be calibrated before applying intraday variance estimates.");

		if (!double.IsFinite(dailyVariance) || dailyVariance <= 0)
			throw new ArgumentOutOfRangeException(nameof(dailyVariance), "Daily variance must be a positive finite number.");

		if (currentLogReturn.HasValue && !double.IsFinite(currentLogReturn.Value))
			throw new ArgumentOutOfRangeException(nameof(currentLogReturn), "Log return must be finite when provided.");

		_intradayVarianceOverride = ClampVariance(dailyVariance);
	 	_intradayReturnOverride = currentLogReturn;
	}

	/// <summary>
	/// Applies an intraday realized volatility estimate for the current trading day.
	/// </summary>
	/// <param name="volatility">Realized volatility estimate (daily units by default).</param>
	/// <param name="isAnnualized">Set to true if the provided volatility is annualized.</param>
	/// <param name="currentLogReturn">Optional log return from the previous close to the current price.</param>
	public void SetIntradayVolatilityEstimate(double volatility, bool isAnnualized = false, double? currentLogReturn = null)
	{
		if (!double.IsFinite(volatility) || volatility < 0)
			throw new ArgumentOutOfRangeException(nameof(volatility), "Volatility must be a non-negative finite number.");

		var dailyVolatility = isAnnualized ? volatility / System.Math.Sqrt(252.0) : volatility;
		var dailyVariance = dailyVolatility * dailyVolatility;
	 	SetIntradayVarianceEstimate(dailyVariance, currentLogReturn);
	}

	/// <summary>
	/// Clears any active intraday overrides, reverting forecasts to use only historical end-of-day data.
	/// </summary>
	public void ClearIntradayEstimate()
	{
		_intradayVarianceOverride = null;
	 	_intradayReturnOverride = null;
	}

	private double ClampVariance(double variance) => variance < _minVariance ? _minVariance : variance;

	/// <summary>
	/// Calculates the in-sample R² statistic of the calibrated model.
	/// Note: R² is always calculated for 1-day ahead forecasts (the model's native horizon).
	/// </summary>
	public double CalculateRSquared()
	{
		if (!_isCalibrated || _coefficients == null || _designMatrix == null || _targets == null)
			throw new InvalidOperationException("Model must be calibrated before computing R².");

		double sse = 0;
		double mean = _targets.Average();
		double sst = 0;

		for (int i = 0; i < _targets.Count; i++)
		{
			var prediction = Dot(_coefficients, _designMatrix[i]);
			
			// R² is calculated in the transformed space (log-space if enabled)
			var residual = _targets[i] - prediction;
			sse += residual * residual;

			var deviation = _targets[i] - mean;
			sst += deviation * deviation;
		}

		return System.Math.Abs(sst) < 1e-12 ? 0.0 : 1.0 - (sse / sst);
	}

	private int FeatureCount =>
		1
		+ (_includeDaily ? 1 : 0)
		+ (_includeShortTerm ? 1 : 0)
		+ (_includeWeekly ? 1 : 0)
		+ (_includeBiWeekly ? 1 : 0)
		+ (_includeMonthly ? 1 : 0)
		+ (_includeLeverageEffect ? 1 : 0);

	private double[] CreateFeatureVector(double dailyRv, double shortRv, double weeklyRv, double biWeeklyRv, double monthlyRv, double currentReturn)
	{
		var vector = new double[FeatureCount];
		var index = 0;
		vector[index++] = 1.0;
		var leverageTerm = ComputeLeverageFeature(currentReturn);

		// Transform features to log-space if enabled
		if (_useLogVariance)
		{
			if (_includeDaily)
				vector[index++] = System.Math.Log(System.Math.Max(dailyRv, _minVariance));

			if (_includeShortTerm)
				vector[index++] = System.Math.Log(System.Math.Max(shortRv, _minVariance));

			if (_includeWeekly)
				vector[index++] = System.Math.Log(System.Math.Max(weeklyRv, _minVariance));

			if (_includeBiWeekly)
				vector[index++] = System.Math.Log(System.Math.Max(biWeeklyRv, _minVariance));

			if (_includeMonthly)
				vector[index++] = System.Math.Log(System.Math.Max(monthlyRv, _minVariance));

			if (_includeLeverageEffect)
				vector[index++] = leverageTerm;
		}
		else
		{
			if (_includeDaily)
				vector[index++] = dailyRv;

			if (_includeShortTerm)
				vector[index++] = shortRv;

			if (_includeWeekly)
				vector[index++] = weeklyRv;

			if (_includeBiWeekly)
				vector[index++] = biWeeklyRv;

			if (_includeMonthly)
				vector[index++] = monthlyRv;

			if (_includeLeverageEffect)
				vector[index++] = leverageTerm;
		}

		return vector;
	}

	private static double ComputeLeverageFeature(double currentReturn)
	{
		if (!double.IsFinite(currentReturn))
			return 0.0;

		return System.Math.Min(currentReturn, 0.0);
	}

	private static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
	{
		double sum = 0;
		for (int i = 0; i < a.Length; i++)
			sum += a[i] * b[i];
		return sum;
    }

    /// <summary>
    /// File format:
    /// Date,Open,High,Low,Close,Volume
    /// 1997-01-02,740.74,742.81,729.55,737.01,257350000
    /// 1997-01-03,737.01,748.24,737.01,748.03,251650000
    /// 1997-01-06,748.03,753.31,743.82,747.65,295194444
    /// 1997-01-07,747.65,753.26,742.18,753.23,299011111
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="skipLines"></param>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="FormatException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
	private void LoadReturnsFromFile(string filePath, int skipLines)
	{
		if (!File.Exists(filePath))
			throw new FileNotFoundException("Input file not found.", filePath);

		_returns.Clear();
		_realizedVariances.Clear();
		_designMatrix = null;
		_targets = null;
		_coefficients = null;
		_isCalibrated = false;
		ClearIntradayEstimate();

		double? previousPrice = null;
		int lineNumber = 0;
		int openIdx = 1, highIdx = 2, lowIdx = 3, closeIdx = 4;

        foreach (var rawLine in File.ReadLines(filePath))
		{
			lineNumber++;
			if (lineNumber <= skipLines)
				continue;

			var line = rawLine.Trim();
			if (string.IsNullOrEmpty(line))
				continue;

            var colummns = line.Split([',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (colummns.Length < 5)
                continue;

            // Trim optional double quotes from each column
            for (int i = 0; i < colummns.Length; i++) {
                colummns[i] = colummns[i].Trim('"');
            }

			// Parse header
			if (lineNumber == 1) {
                for (int i = 0; i < colummns.Length; i++) {
                    switch (colummns[i].ToLowerInvariant()) {
						case "open":
							openIdx = i;
							break;
						case "high":
							highIdx = i;
							break;
						case "low":
							lowIdx = i;
							break;
						case "price":
                        case "close":
                            closeIdx = i;
							break;
                    }
                }
				continue;
            }

            if (!double.TryParse(colummns[closeIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var close))
				throw new FormatException($"Unable to parse numeric value on line {lineNumber}: '{rawLine}'.");

			if (_treatInputAsPrices)
			{
				if (colummns.Length < 5)
					continue;

				if (!double.TryParse(colummns[openIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var open) ||
					!double.TryParse(colummns[highIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var high) ||
					!double.TryParse(colummns[lowIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var low))
				{
					throw new FormatException($"Unable to parse OHLC values on line {lineNumber}: '{rawLine}'.");
				}

				if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
					throw new InvalidOperationException($"OHLC prices must be positive on line {lineNumber}.");

				if (high < low)
					throw new InvalidOperationException($"High must be greater than or equal to low on line {lineNumber}.");

				if (previousPrice.HasValue)
				{
					var closeToCloseReturn = System.Math.Log(close / previousPrice.Value);
					var realizedVariance = ComputeYangZhangVariance(previousPrice.Value, open, high, low, close);
					AddObservation(closeToCloseReturn, realizedVariance);
				}

				previousPrice = close;
			}
			else
			{
				AddObservation(close);
			}
		}

		if (_treatInputAsPrices && _returns.Count == 0)
			throw new InvalidOperationException("Expected at least 2 prices to compute returns.");

		if (!_treatInputAsPrices && _returns.Count == 0)
			throw new InvalidOperationException("No returns were loaded from the file.");
	}

	private void EnsureRealizedVarianceConsistency()
	{
		if (_realizedVariances.Count == 0)
		{
			foreach (var r in _returns)
				_realizedVariances.Add(r * r);
		}

		if (_realizedVariances.Count != _returns.Count)
			throw new InvalidOperationException("Realized variance series is inconsistent with returns series.");
	}

	private static double ComputeYangZhangVariance(double previousClose, double open, double high, double low, double close)
	{
		var overnightReturn = System.Math.Log(open / previousClose);
		var intradayReturn = System.Math.Log(close / open);
		var logHighOpen = System.Math.Log(high / open);
		var logLowOpen = System.Math.Log(low / open);

		var rs = logHighOpen * (logHighOpen - intradayReturn) + logLowOpen * (logLowOpen - intradayReturn);
		var variance = overnightReturn * overnightReturn + DefaultYangZhangK * intradayReturn * intradayReturn + (1.0 - DefaultYangZhangK) * rs;

		return variance < 0 ? 0.0 : variance;
	}

	private double[] SolveNormalEquations(List<double[]> designMatrix, List<double> targets)
	{
		int k = FeatureCount;
		var xtx = new double[k, k];
		var xty = new double[k];

		for (int row = 0; row < designMatrix.Count; row++)
		{
			var x = designMatrix[row];
			var y = targets[row];

			for (int i = 0; i < k; i++)
			{
				xty[i] += x[i] * y;
				for (int j = 0; j < k; j++)
					xtx[i, j] += x[i] * x[j];
			}
		}

		// Add Ridge penalty (L2 regularization) to diagonal elements (except intercept)
		if (_ridgePenalty > 0)
		{
			for (int i = 1; i < k; i++) // Start from 1 to skip intercept
				xtx[i, i] += _ridgePenalty * designMatrix.Count;
		}

		return SolveLinearSystem(xtx, xty);
	}

	private void AssignCoefficients(IReadOnlyList<double> coefficients)
	{
		var index = 0;
		Beta0 = coefficients[index++];
		Beta1 = _includeDaily ? coefficients[index++] : 0.0;
		BetaShortTerm = _includeShortTerm ? coefficients[index++] : 0.0;
		Beta2 = _includeWeekly ? coefficients[index++] : 0.0;
		BetaBiWeekly = _includeBiWeekly ? coefficients[index++] : 0.0;
		Beta3 = _includeMonthly ? coefficients[index++] : 0.0;
		BetaLeverage = _includeLeverageEffect ? coefficients[index++] : 0.0;
	}

	private static double[] SolveLinearSystem(double[,] matrix, double[] vector)
	{
		var dimension = vector.Length;
		var augmented = new double[dimension, dimension + 1];

		for (int i = 0; i < dimension; i++)
		{
			for (int j = 0; j < dimension; j++)
				augmented[i, j] = matrix[i, j];

			augmented[i, dimension] = vector[i];
		}

		for (int col = 0; col < dimension; col++)
		{
			var pivotRow = col;
			var pivotAbs = System.Math.Abs(augmented[pivotRow, col]);
			for (int row = col + 1; row < dimension; row++)
			{
				var candidate = System.Math.Abs(augmented[row, col]);
				if (candidate > pivotAbs)
				{
					pivotAbs = candidate;
					pivotRow = row;
				}
			}

			if (pivotAbs < 1e-14)
				throw new InvalidOperationException("Design matrix is singular; unable to calibrate HAR-RV model.");

			if (pivotRow != col)
				SwapRows(augmented, pivotRow, col);

			var pivot = augmented[col, col];
			for (int row = col + 1; row < dimension; row++)
			{
				var factor = augmented[row, col] / pivot;
				if (System.Math.Abs(factor) < 1e-18)
					continue;

				for (int c = col; c <= dimension; c++)
					augmented[row, c] -= factor * augmented[col, c];
			}
		}

		var solution = new double[dimension];

		for (int row = dimension - 1; row >= 0; row--)
		{
			double sum = augmented[row, dimension];
			for (int col = row + 1; col < dimension; col++)
				sum -= augmented[row, col] * solution[col];

			solution[row] = sum / augmented[row, row];
		}

		return solution;
	}

	private static void SwapRows(double[,] matrix, int rowA, int rowB)
	{
		if (rowA == rowB)
			return;

		var columns = matrix.GetLength(1);
		for (int col = 0; col < columns; col++)
		{
			(matrix[rowA, col], matrix[rowB, col]) = (matrix[rowB, col], matrix[rowA, col]);
		}
	}
}
