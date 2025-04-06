
namespace AppCore;

public class RiskCurve
{
    private SortedList<float, float> _points = new();
    private float _maxPL = float.MinValue;
    private float _minPL = float.MaxValue;

    public void Add(float price, float totalPL) {
        if (totalPL > _maxPL) {
            _maxPL = totalPL;
        }
        if (totalPL < _minPL) {
            _minPL = totalPL;
        }
        if (!_points.ContainsKey(price)) {
            _points.Add(price, totalPL);
        }
    }

    public float MaxPL => _maxPL;
    public float MinPL => _minPL;

    public SortedList<float, float> Points => _points;
}
