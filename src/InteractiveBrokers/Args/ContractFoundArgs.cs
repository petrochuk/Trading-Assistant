using AppCore;

namespace InteractiveBrokers.Args;

public class ContractFoundArgs : EventArgs
{
    public required Contract Contract { get; init; }
}
