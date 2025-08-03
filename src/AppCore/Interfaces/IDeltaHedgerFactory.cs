using AppCore.Configuration;
using AppCore.Models;

namespace AppCore.Interfaces;

public interface IDeltaHedgerFactory
{
    /// <summary>
    /// Creates a delta hedger for the specified contract.
    /// </summary>
    /// <param name="contract">The contract for which to create the delta hedger.</param>
    /// <param name="configuration">The delta hedger configuration.</param>
    /// <returns>A new instance of <see cref="IDeltaHedger"/>.</returns>
    IDeltaHedger Create(Contract contract, DeltaHedgerConfiguration configuration);
}
