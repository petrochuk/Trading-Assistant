namespace AppCore.Args;

public class DisconnectedArgs : EventArgs
{
    public bool IsUnexpected { get; set; } = false;
}
