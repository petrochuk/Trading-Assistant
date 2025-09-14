using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("d:{DeltaHeston}, g:{Gamma}, t:{Theta}, v:{Vega}, vn:{Vanna}, c:{Charm}")]
public struct Greeks
{
    public float DeltaBLS;
    public float DeltaHeston;

    public float Gamma;
    public float Theta;
    public float Vega;
    public float Vanna;
    public float Charm;
}
