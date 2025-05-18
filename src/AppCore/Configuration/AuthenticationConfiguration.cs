namespace AppCore.Configuration;

public class AuthenticationConfiguration
{
    public enum AuthenticationType
    {
        Gateway,
        OAuth2
    }

    public AuthenticationType Type { get; set; } = AuthenticationType.Gateway;
    public string TokenUrl { get; set; } = string.Empty;
    public string SessionUrl { get; set; } = string.Empty;
    public string ValidateUrl { get; set; } = string.Empty;
    public string SessionInitUrl { get; set; } = string.Empty;
    public string WebSocketUrl { get; set; } = string.Empty;

    public string PrivateKeyPath { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
