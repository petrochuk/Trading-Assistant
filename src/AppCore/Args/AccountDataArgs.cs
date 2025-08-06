namespace AppCore.Args;

public class AccountDataArgs : EventArgs
{
    public required string AccountId { get; init; }

    public required string DataKey { get; init; }

    public float? MonetaryValue { get; init; }
}
