
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

    public void Clear() {
        _points.Clear();
        _maxPL = float.MinValue;
        _minPL = float.MaxValue;
    }

    public float MaxPL => _maxPL;
    public float MinPL => _minPL;

    public string Name { get; set; } = string.Empty;

    public TimeSpan TimeSpan { get; set; } = TimeSpan.Zero;
    
    public int Color { get; set; } = 0xFFFFFF;

    public SortedList<float, float> Points => _points;
}
