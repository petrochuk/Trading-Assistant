using AppCore.Statistics;

namespace AppCore.Tests.Fakes;

public class TestRealizedVol : IRealizedVolatility
{
    public double TestValue { get; set; }

    public void AddValue(double value) {
        throw new NotImplementedException();
    }

    public void Reset(double? initialValue = 0) {
    }

    public bool TryGetValue(out double value) {
        value = TestValue;
        return true;
    }

    public bool TryGetVolatilityOfVolatility(out double value) {
        value = 0;
        return false;
    }
}
