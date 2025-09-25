using System.Diagnostics;

namespace AppCore.Configuration;

public class DeltaHedgerConfiguration
{
    public TimeSpan HedgeInterval { get; set; } = TimeSpan.FromSeconds(15);
    public List<string> SupportedAccounts { get; set; } = [];
    public Dictionary<string, DeltaHedgerSymbolConfiguration> Configs { get; set; } = [];
}

[DebuggerDisplay("d:{Delta}")]
public class DeltaHedgerSymbolConfiguration
{
    /// <summary>
    /// Delta value to hedge against (positive number). When position stays +/- Delta it is not hedged
    /// </summary>
    public float Delta { get; set; } = 0f;

    /// <summary>
    /// Minumial IV value
    /// </summary>
    public float MinIV { get; set; } = 0f;

    /// <summary>
    /// Initial IV value
    /// </summary>
    public float InitialIV { get; set; } = 0.10f;

    /// <summary>
    /// Minimum delta adjustment to trigger a hedge. It can be 1 contract for futures or 100 shares for stocks.
    /// </summary>
    public float MinDeltaAdjustment { get; set; } = 1f;

    public TimeSpan? BlackOutStart { get; set; }

    public TimeSpan? BlackOutEnd { get; set; }
}
