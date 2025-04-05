namespace InteractiveBrokers.Args;

public class AccountConnectedArgs : EventArgs
{
    public required string AccountId { get; set; }
}
