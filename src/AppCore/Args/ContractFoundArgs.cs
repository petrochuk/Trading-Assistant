using AppCore.Models;

namespace AppCore.Args;

public class ContractFoundArgs : EventArgs
{
    public required Contract Contract { get; init; }
}
