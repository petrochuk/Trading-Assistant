using AppCore.Extenstions;

namespace AppCore.Statistics;

/// <summary>
/// Calculates realized volatility using subsampling method.
/// </summary>
public class RVwithSubsampling : IRealizedVolatility
{
    private readonly TimeSpan _period;
    private List<EwmaVolatility> _subsamples = new();
    private int _currentSubsampleIndex = 0;
    Lock _lock = new();

    public RVwithSubsampling(TimeSpan period, int subsamplesCount)
    {
        if (subsamplesCount < 1)
            throw new ArgumentException($"Number of subsamples must be greater than 1. Actual {subsamplesCount}", nameof(subsamplesCount));
        if (period <= TimeSpan.Zero)
            throw new ArgumentException($"Period must be greater than zero. Actual {period}", nameof(period));
        _period = period;
        for (int i = 0; i < subsamplesCount; i++)
        {
            //_subsamples.Add(new RollingStandardDeviation());
            // 24 hours = 24 * 60 / 5min
            _subsamples.Add(EwmaVolatility.FromPeriod(288));
        }
    }

    public TimeSpan Period => _period;

    public TimeSpan SubsamplePeriod => TimeSpan.FromTicks(_period.Ticks / _subsamples.Count);

    /// <summary>
    /// Resets the realized volatility calculator to the initial value.
    /// </summary>
    /// <param name="initialValue">annualized initial value</param>
    public void Reset(double? initialValue = null) {
        lock (_lock) 
        {
            var initialSubsampleVariance = initialValue.HasValue ? System.Math.Pow(initialValue.Value / System.Math.Sqrt(TimeExtensions.DaysPerYear * 24.0 * (60.0 / _period.TotalMinutes)), 2) : (double?)null;
            foreach (var subsample in _subsamples) {
                subsample.Reset(initialSubsampleVariance);
            }
            _currentSubsampleIndex = 0;
        }
    }

    public void AddValue(double value)
    {
        _subsamples[_currentSubsampleIndex].AddLogReturn(value);
        _currentSubsampleIndex = (_currentSubsampleIndex + 1) % _subsamples.Count;
    }

    public bool TryGetValue(out double value)
    {
        value = 0;
        double total = 0;
        int validSubsampleCount = 0;
        foreach (var subsample in _subsamples)
        {
            if (!subsample.TryGetValue(out double subsampleValue))
                continue;

            total += subsampleValue;
            validSubsampleCount++;
        }

        if (validSubsampleCount < _subsamples.Count)
            return false;

        value = total / validSubsampleCount * System.Math.Sqrt(TimeExtensions.DaysPerYear * 24.0 * (60.0 / _period.TotalMinutes));

        return true;
    }
}
