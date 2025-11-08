using AppCore.Collections;
using AppCore.Models;

namespace AppCore;

public class Greeks
{
    public float Delta;
    public float DeltaHedge;
    public float DeltaTotal => Delta + DeltaHedge;
    public float Gamma;
    public float Theta;
    public float Vega;
    public float Vanna;
    public float Charm;

    public SortedList<float, Position> OvervaluedPositions = new(new DuplicateKeyComparer<float>());

    override public string ToString()
    {
        return $"D: {Delta}, H:{DeltaHedge}, Gamma: {Gamma}, Theta: {Theta}, Vega: {Vega}, Vanna: {Vanna}, Charm: {Charm}";
    }
}
