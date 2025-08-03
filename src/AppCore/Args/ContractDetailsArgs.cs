using AppCore.Models;

namespace AppCore.Args;

public class ContractDetailsArgs : EventArgs
{
    public required Contract Contract { get; init; }
}
