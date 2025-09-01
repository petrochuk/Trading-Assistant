using AppCore.Models;

namespace AppCore.Args;

public class ContractFoundArgs : EventArgs
{
    public required string Symbol { get; init; }

    public required AssetClass AssetClass { get; init; }

    public List<Contract> Contracts { get; init; } = new List<Contract>();
}
