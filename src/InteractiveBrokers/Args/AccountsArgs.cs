using InteractiveBrokers.Responses;

namespace InteractiveBrokers.Args;

public class AccountsArgs : EventArgs
{
    public required List<Account> Accounts { get; init; }
}
