using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;
using System.Diagnostics;

namespace AppCore.Hedging;

/// <summary>
/// Delta hedging service for single contract.
/// </summary
[DebuggerDisplay("{Contract}")]
public class DeltaHedger : IDeltaHedger
{
    private readonly DeltaHedgerConfiguration _configuration;
    private readonly Contract _contract;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeltaHedger"/> class.
    /// </summary>
    /// <param name="configuration">The delta hedger configuration.</param>
    public DeltaHedger(Contract contract, DeltaHedgerConfiguration configuration)
    {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Contract Contract => _contract;

    public void Hedge() {
    }
}
