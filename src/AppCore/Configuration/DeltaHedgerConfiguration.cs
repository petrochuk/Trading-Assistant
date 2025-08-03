using System.Diagnostics;

namespace AppCore.Configuration;

public class DeltaHedgerConfiguration
{
    public TimeSpan HedgeInterval { get; set; } = TimeSpan.FromSeconds(15);
    public Dictionary<string, DeltaHedgerSymbolConfiguration> Configs { get; set; } = new Dictionary<string, DeltaHedgerSymbolConfiguration>();
}

[DebuggerDisplay("d:{Delta}")]
public class DeltaHedgerSymbolConfiguration
{
    public float Delta { get; set; } = 1f;
}
