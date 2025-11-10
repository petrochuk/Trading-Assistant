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
    
    /// <summary>
    /// Variance-weighted implied volatility for VRP system tracking.
    /// Calculated as sqrt(Σ(σ² * |Vega|) / Σ|Vega|) where σ is market implied vol.
    /// This properly accounts for variance exposure (σ²) weighted by vega, 
    /// making it directly comparable to realized variance for VRP measurement.
    /// </summary>
    public float VarianceWeightedIV;

    public SortedList<float, Position> OvervaluedPositions = new(new DuplicateKeyComparer<float>());

    public bool IsDeltaValid {
        get => float.IsNaN(DeltaITM) == false && float.IsNaN(DeltaOTM) == false && float.IsNaN(DeltaHedge) == false;
    }
    override public string ToString()
    {
        return $"D: {DeltaTotal}, H:{DeltaHedge}, Gamma: {Gamma}, Theta: {Theta}, Vega: {Vega}, Vanna: {Vanna}, Charm: {Charm}, VW-IV: {VarianceWeightedIV:F3}";
    }
}
