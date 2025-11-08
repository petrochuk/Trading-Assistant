using AppCore.Collections;
using AppCore.Models;

namespace AppCore;

public class Greeks
{
    public float DeltaITM;
    public float DeltaOTM;
    public float DeltaHedge;
    public float DeltaTotal => DeltaITM + DeltaOTM + DeltaHedge;
    public float Gamma;
    public float Theta;
    public float Vega;
    public float Vanna;
    public float Charm;

    public SortedList<float, Position> OvervaluedPositions = new(new DuplicateKeyComparer<float>());

    public bool IsDeltaValid {
        get => float.IsNaN(DeltaITM) == false && float.IsNaN(DeltaOTM) == false && float.IsNaN(DeltaHedge) == false;
    }
    override public string ToString()
    {
        return $"D: {DeltaTotal}, H:{DeltaHedge}, Gamma: {Gamma}, Theta: {Theta}, Vega: {Vega}, Vanna: {Vanna}, Charm: {Charm}";
    }
}
