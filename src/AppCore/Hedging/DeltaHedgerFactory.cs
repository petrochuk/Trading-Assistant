using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;

namespace AppCore.Hedging;

/// <summary>
/// Factory for creating delta hedgers.
/// </summary>
public class DeltaHedgerFactory : IDeltaHedgerFactory
{
    public IDeltaHedger Create(Contract contract, DeltaHedgerConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(contract.Symbol))
        {
            throw new ArgumentException("Underlying symbol cannot be null or empty.", nameof(contract.Symbol));
        }

        return new DeltaHedger(configuration);
    }
}
