namespace AppCore.Statistics;

/// <summary>
/// Rolling volatility calculator using the Exponentially Weighted Moving Average (EWMA) model.
/// Variance is updated as: v_t = lambda * v_{t-1} + (1 - lambda) * r_t^2
/// where 0 &lt; lambda &lt; 1 and r_t is the (log) return at time t.
/// This model emphasizes recent observations and is often preferred when the mean is non-stationary
/// or when more weight should be given to recent volatility.
/// </summary>
public sealed class EwmaVolatility
{
    private readonly double _lambda; // decay factor in (0,1)
    private double? _variance;       // EWMA variance
    private int _count;              // number of returns processed
    private double? _lastValue;

    /// <summary>
    /// Creates a new EWMA volatility calculator.
    /// </summary>
    /// <param name="lambda">Decay factor in (0,1). Higher values (e.g., 0.94) give more weight to the past.</param>
    public EwmaVolatility(double lambda = 0.94)
    {
        if (double.IsNaN(lambda) || lambda <= 0 || lambda >= 1)
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be in the open interval (0,1).");

        _lambda = lambda;
    }

    /// <summary>
    /// Creates a new EWMA volatility calculator from a smoothing period using the EMA mapping: alpha = 2/(period+1), lambda = 1 - alpha.
    /// </summary>
    /// <param name="period">The smoothing period (&gt;= 1). Larger periods result in slower reaction.</param>
    public static EwmaVolatility FromPeriod(int period)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be &gt;= 1.");
        // EMA alpha = 2/(N+1); here lambda = 1 - alpha
        var alpha = 2.0 / (period + 1.0);
        var lambda = 1.0 - alpha;
        // Clamp to (0,1) just in case of numerical issues
        if (lambda <= 0) lambda = double.Epsilon;
        if (lambda >= 1) lambda = 1 - double.Epsilon;
        return new EwmaVolatility(lambda);
    }

    /// <summary>
    /// Adds a new return value and updates the EWMA variance.
    /// </summary>
    /// <param name="r">Return value (e.g., log return). Non-finite values are ignored.</param>
    public void AddLogReturn(double value)
    {
        if (!double.IsFinite(value))
            return;

        if (!_lastValue.HasValue) {
            _lastValue = value;
            return;
        }

        var logReturn = System.Math.Log(value / _lastValue.Value);
        var rr = logReturn * logReturn;
        if (_variance is null)
        {
            // Initialize variance with the first squared return
            _variance = rr;
        }
        else
        {
            _variance = _lambda * _variance.Value + (1 - _lambda) * rr;
        }

        _count++;
    }

    /// <summary>
    /// Gets the current volatility (standard deviation) if at least one return has been processed.
    /// </summary>
    public double Value
    {
        get
        {
            if (_variance is null || _count == 0)
                throw new InvalidOperationException("Not enough data to compute volatility.");
            return System.Math.Sqrt(_variance.Value);
        }
    }

    /// <summary>
    /// Attempts to get the current volatility (standard deviation).
    /// </summary>
    public bool TryGetValue(out double value)
    {
        if (_variance is null || _count == 0)
        {
            value = 0;
            return false;
        }

        value = System.Math.Sqrt(_variance.Value);

        return true;
    }

    /// <summary>
    /// Gets the number of returns processed.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the current EWMA variance if available; otherwise NaN.
    /// </summary>
    public double Variance => _variance ?? double.NaN;

    /// <summary>
    /// Gets the decay factor used by the calculator.
    /// </summary>
    public double Lambda => _lambda;

    public override string ToString()
    {
        if (_variance is null || _count == 0)
            return "Not enough data";
        return $"EWMA Vol: {Value}, Count: {Count}, Variance: {Variance}, Lambda: {Lambda}";
    }
}
