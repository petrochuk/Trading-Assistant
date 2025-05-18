namespace InteractiveBrokers.Args;

public class AuthenticatedArgs : EventArgs
{
    public required string BearerToken { get; init; }
}
