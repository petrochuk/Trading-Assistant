using AppCore.Models;

namespace AppCore.Args;

public class AccountPositionsArgs : EventArgs
{
    public required string AccountId { get; init; }

    public Dictionary<int, IPosition> Positions { get; set; } = new();
}
