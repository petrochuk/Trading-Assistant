using AppCore;

namespace InteractiveBrokers.Args;

public class AccountPositionsArgs : EventArgs
{
    public required Dictionary<int, Position> Positions { get; set; }
}
