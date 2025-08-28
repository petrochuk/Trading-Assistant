using AppCore.Extenstions;

namespace AppCore.Statistics;

/// <summary>
/// Calculates realized volatility using subsampling method.
/// </summary>
public class RVwithSubsampling
{
    private readonly TimeSpan _period;
    private List<EwmaVolatility> _subsamples = new();
    private int _currentSubsampleIndex = 0;

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
            _subsamples.Add(EwmaVolatility.FromPeriod(36));
        }
    }

    public TimeSpan Period => _period;

    public TimeSpan SubsamplePeriod => TimeSpan.FromTicks(_period.Ticks / _subsamples.Count);

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
