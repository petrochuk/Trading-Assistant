namespace InteractiveBrokers.Responses;

public class Tickle
{
    public string Session { get; set; } = string.Empty;
    public HMDS? HMDS { get; set; }
    public IServer? IServer { get; set; }
    public long lastTickle { get; set; }
    public int ssoExpires { get; set; }
    public bool Collission { get; set; }
    public int? UserId { get; set; }
    public string? Error { get; set; }
}

public class HMDS
{
    public string? Error { get; set; }
}

public class IServer
{
    public AuthStatus AuthStatus { get; set; }
}

public class AuthStatus
{
    public bool authenticated { get; set; }
    public bool competing { get; set; }
    public bool connected { get; set; }
    public string message { get; set; }
    public string MAC { get; set; }
    public ServerInfo? ServerInfo { get; set; }
}

public class ServerInfo
{
    public required string ServerName { get; set; }
    public required string ServerVersion { get; set; }
}
