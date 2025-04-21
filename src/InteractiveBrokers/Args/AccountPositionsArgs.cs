using AppCore.Models;

namespace InteractiveBrokers.Args;

public class AccountPositionsArgs : EventArgs
{
    public Dictionary<int, IPosition> Positions { get; set; } = new();
}
