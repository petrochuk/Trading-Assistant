using AppCore.Collections;
using AppCore.Models;
using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("d:{DeltaHeston}, g:{Gamma}, t:{ThetaHeston}, v:{Vega}, vn:{Vanna}, c:{Charm}")]
public class Greeks
{
    public float DeltaBLS;
    public float DeltaHeston;

    public float Gamma;

    public float ThetaBLS;
    public float ThetaHeston;

    public float Vega;
    public float Vanna;
    public float Charm;

    public SortedList<float, Position> OvervaluedPositions = new(new DuplicateKeyComparer<float>());
}
