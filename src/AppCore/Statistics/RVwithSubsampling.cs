using AppCore.Extenstions;

namespace AppCore.Statistics;

/// <summary>
/// Calculates realized volatility using subsampling method and also tracks volatility of volatility.
/// </summary>
public class RVwithSubsampling : IRealizedVolatility
{
    private readonly TimeSpan _period;
    
    private List<EwmaVolatility> _subsamples = new();
    private int _currentSubsampleIndex = 0;

    private List<EwmaVolatility> _volOfVolSubsamples = new();
    private int _currentVolOfVolSubsampleIndex = 0;

    Lock _lock = new();
    
    public RVwithSubsampling(TimeSpan period, int subsamplesCount)
    {
        if (subsamplesCount < 1)
            throw new ArgumentException($"Number of subsamples must be greater than 1. Actual {subsamplesCount}", nameof(subsamplesCount));
        if (period <= TimeSpan.Zero)
            throw new ArgumentException($"Period must be greater than zero. Actual {period}", nameof(period));
            
        _period = period;

        // 3 hours = 3 * 60 / 5min
        int averagePeriod = (int)(3.0 * 60.0 / period.TotalMinutes);
        for (int i = 0; i < subsamplesCount; i++)
        {
            //_subsamples.Add(new RollingStandardDeviation());
            _subsamples.Add(EwmaVolatility.FromPeriod(averagePeriod));
            _volOfVolSubsamples.Add(EwmaVolatility.FromPeriod(averagePeriod));
        }
    }

    public TimeSpan Period => _period;

    public TimeSpan SubsamplePeriod => TimeSpan.FromTicks(_period.Ticks / _subsamples.Count);

    public int SubsamplesCount => _subsamples.Count;

    /// <summary>
    /// Attempts to get the current volatility of volatility value.
    /// </summary>
    /// <param name="value">The volatility of volatility value.</param>
    /// <returns>True if volatility of volatility is available, false otherwise.</returns>
    public bool TryGetVolatilityOfVolatility(out double value)
    {
        lock (_lock)
        {
            value = 0;
            double total = 0;
            int validSubsampleCount = 0;
            foreach (var subsample in _volOfVolSubsamples)
            {
                if (!subsample.TryGetValue(out double subsampleValue))
                    continue;
                total += subsampleValue;
                validSubsampleCount++;
            }

            if (validSubsampleCount < _volOfVolSubsamples.Count)
                return false;

            value = total / validSubsampleCount * System.Math.Sqrt(TimeExtensions.DaysPerYear * 24.0 * (60.0 / _period.TotalMinutes));

            return true;
        }
    }

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

            foreach (var vovSubsample in _volOfVolSubsamples) {
                vovSubsample.Reset(null);
            }
        }
    }

    public void AddValue(double value)
    {
        lock (_lock)
        {
            _subsamples[_currentSubsampleIndex].AddLogReturn(value);
            _currentSubsampleIndex = (_currentSubsampleIndex + 1) % _subsamples.Count;
            
            if (TryGetValueInternal(out double currentVolatility))
            {
                _volOfVolSubsamples[_currentVolOfVolSubsampleIndex].AddLogReturn(currentVolatility);
                _currentVolOfVolSubsampleIndex = (_currentVolOfVolSubsampleIndex + 1) % _volOfVolSubsamples.Count;
            }
        }
    }

    /// <summary>
    /// Internal method to get volatility value without locking (already locked by caller).
    /// </summary>
    private bool TryGetValueInternal(out double value)
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

    public bool TryGetValue(out double value)
    {
        lock (_lock)
        {
            return TryGetValueInternal(out value);
        }
    }
}
