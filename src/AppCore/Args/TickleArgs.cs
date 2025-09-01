namespace AppCore.Args;

public class TickleArgs : EventArgs
{
    public required string Session { get; set; }

    public bool IsConnected { get; set; }

    public bool IsAuthenticated { get; set; }
}
