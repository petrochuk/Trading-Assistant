namespace AppCore.Args;

public class AccountsArgs : EventArgs
{
    public class Account
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
    }

    public List<Account> Accounts { get; } = new List<Account>();
}
