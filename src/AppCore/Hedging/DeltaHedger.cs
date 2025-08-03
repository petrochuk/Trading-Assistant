using AppCore.Configuration;
using AppCore.Interfaces;

namespace AppCore.Hedging;

/// <summary>
/// Delta hedging service for single contract.
/// </summary>
public class DeltaHedger : IDeltaHedger
{
    private readonly DeltaHedgerConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(DeltaHedgerConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
}
