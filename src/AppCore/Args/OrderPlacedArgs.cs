using AppCore.Models;

namespace AppCore.Args;

public class OrderPlacedArgs : EventArgs
{
    public OrderPlacedArgs()
    {
    }

    public required string AccountId { get; init; }

    public required Guid OrderId { get; init; }

    public required Contract Contract { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
