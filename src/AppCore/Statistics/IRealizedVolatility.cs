namespace AppCore.Statistics;

public interface IRealizedVolatility
{
    void AddValue(double value);

    bool TryGetValue(out double value);
}
