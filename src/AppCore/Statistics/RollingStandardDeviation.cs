﻿namespace AppCore.Statistics;

/// <summary>
/// Rolling standard deviation calculator using Welford's method.
/// Warning: For fixed periods, the algorithm can produce large errors when values trend becase the mean is changing.
/// It works well for non-trending data with stable mean.
/// </summary>
public class RollingStandardDeviation
{
    private readonly int? _period;
    private double _count;
    private double _mean;
    private double _sum;
    private double _oldestValue;
    private double? _lastValue;

    public RollingStandardDeviation(int? period = null) {
        if (period != null && period <= 1)
            throw new ArgumentException($"Period must be greater than 1. Actual {period}", nameof(period));

        _period = period;
        _count = 0;
        _mean = 0;
        _sum = 0;
        _oldestValue = 0;
    }

    public void AddLogReturn(double value) {
        if (_lastValue.HasValue) {
            var logReturn = System.Math.Log(value / _lastValue.Value);
            AddValue(logReturn);
        }

        _lastValue = value;
    }

    /// <summary>
    /// Adds a new value to the rolling standard deviation calculator.
    /// </summary>
    public void AddValue(double value) {
        if (!double.IsFinite(value))
            return;

        if (_period == null || _count < _period) {
            // Add value to the rolling window
            _count++;
            double tempMean = _mean;
            _mean += (value - tempMean) / _count;
            _sum += (value - tempMean) * (value - _mean);
        }
        else {
            // Remove the effect of the oldest value
            double tempMean = _mean;
            _mean -= (_oldestValue - tempMean) / _count;
            _sum -= (_oldestValue - tempMean) * (_oldestValue - _mean);

            // Add the new value
            tempMean = _mean;
            _mean += (value - tempMean) / _count;
            _sum += (value - tempMean) * (value - _mean);
        }

        // Update the oldest value
        _oldestValue = value;
    }

    public double Mean => _mean;

    public double Value {
        get {
            if (_count < 2)
                throw new InvalidOperationException("At least two values are required to calculate standard deviation.");

            return System.Math.Sqrt(_sum / (_count - 1));
        }
    }

    public bool TryGetValue(out double value) {
        if (_count < 2) {
            value = 0;
            return false;
        }
     
        value = System.Math.Sqrt(_sum / (_count - 1));
        
        return true;
    }

    public int Count => (int)_count;

    public override string ToString() {
        if (_count < 2)
            return "Not enough data";

        return $"StdDev: {Value}, Count: {Count}, Mean: {Mean}";
    }
}
