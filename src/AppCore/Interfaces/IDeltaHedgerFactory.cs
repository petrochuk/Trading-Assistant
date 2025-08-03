using AppCore.Configuration;
using AppCore.Models;

namespace AppCore.Interfaces;

public interface IDeltaHedgerFactory
{
    /// <summary>
    /// Creates a delta hedger for the specified contract.
    /// </summary>
    /// <param name="broker">The broker instance.</param>
    /// <param name="accountId">The account ID.</param>
    /// <param name="underlyingPosition">The underlying position for which to create the delta hedger.</param>
    /// <param name="positions">The positions collection.</param>
    /// <param name="configuration">The delta hedger configuration.</param>
    /// <returns>A new instance of <see cref="IDeltaHedger"/>.</returns>
    IDeltaHedger Create(IBroker broker, string accountId, Position underlyingPosition, PositionsCollection positions, DeltaHedgerConfiguration configuration);
}
