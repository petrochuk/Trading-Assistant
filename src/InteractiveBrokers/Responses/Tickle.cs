namespace InteractiveBrokers.Responses;

public class Tickle
{
    public string session { get; set; }
    public Hmds hmds { get; set; }
    public Iserver iserver { get; set; }
    public long lastTickle { get; set; }
    public int ssoExpires { get; set; }
    public bool collission { get; set; }
    public int userId { get; set; }
}

public class Hmds
{
    public string error { get; set; }
}

public class Iserver
{
    public Authstatus authStatus { get; set; }
}

public class Authstatus
{
    public bool authenticated { get; set; }
    public bool competing { get; set; }
    public bool connected { get; set; }
    public string message { get; set; }
    public string MAC { get; set; }
    public Serverinfo serverInfo { get; set; }
}

public class Serverinfo
{
    public string serverName { get; set; }
    public string serverVersion { get; set; }
}
