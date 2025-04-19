using InteractiveBrokers.Responses;

namespace InteractiveBrokers.Args;

public class AccountConnectedArgs : EventArgs
{
    public required Account Account { get; init; }
}
