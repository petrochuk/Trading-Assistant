using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("d:{Delta}, g:{Gamma}, t:{Theta}, v:{Vega}, vn:{Vanna}, c:{Charm}")]
public struct Greeks
{
    public float Delta;
    public float Gamma;
    public float Theta;
    public float Vega;
    public float Vanna;
    public float Charm;
}
