using AppCore.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace InteractiveBrokers.Requests;

internal class Authenticate : Request
{
    EventHandler<Args.AuthenticatedArgs>? _responseHandler;
    private AuthenticationConfiguration _authConfiguration;
    private string _accessToken = string.Empty;
    private string _bearerToken = string.Empty;
    private string _publicIP = string.Empty;

    [SetsRequiredMembers]
    public Authenticate(EventHandler<Args.AuthenticatedArgs>? responseHandler, AuthenticationConfiguration authConfiguration) {
        _responseHandler = responseHandler;
        _authConfiguration = authConfiguration ?? throw new ArgumentNullException(nameof(authConfiguration), "Authentication configuration cannot be null.");
        Uri = string.Empty;
        if (string.IsNullOrWhiteSpace(_authConfiguration.PrivateKeyPath)) {
            throw new ArgumentNullException(nameof(_authConfiguration.PrivateKeyPath), "Private key path cannot be null or empty.");
        }
    }

    public override void Execute(HttpClient httpClient) {
        GetPublicIP(httpClient);
        RequestAccessToken(httpClient, _authConfiguration.TokenUrl);
        RequestBearerToken(httpClient, _authConfiguration.SessionUrl);
        ValidateToken(httpClient, _authConfiguration.ValidateUrl);
    }

    private void GetPublicIP(HttpClient httpClient) {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
        var response = httpClient.SendAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty public IP response");
        }
        _publicIP = responseContent;
    }

    private void RequestAccessToken(HttpClient httpClient, string tokenUrl) {
        var postRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        var formData = new List<KeyValuePair<string, string>> {
            new("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
            new("client_assertion", ComputeClientAssertion(tokenUrl)),
            new("grant_type", "client_credentials"),
            new("scope", "sso-sessions.write"),
        };
        postRequest.Content = new FormUrlEncodedContent(formData);
        var response = httpClient.SendAsync(postRequest).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty access token response");
        }
        var authenticationResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.Authenticate);
        if (authenticationResponse == null || string.IsNullOrWhiteSpace(authenticationResponse.AccessToken) ) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid access token response");
        }
        if (authenticationResponse.TokenType != "Bearer") {
            throw new IBClientException($"IB Client returned invalid token type: {authenticationResponse.TokenType}");
        }
        _accessToken = authenticationResponse.AccessToken;

        Logger?.LogInformation($"Received access token");
    }

    private void RequestBearerToken(HttpClient httpClient, string sessionUrl) {
        var postRequest = new HttpRequestMessage(HttpMethod.Post, sessionUrl);
        postRequest.Headers.Add("Authorization", $"Bearer {_accessToken}");
        postRequest.Content = new StringContent(ComputeClientAssertion(sessionUrl));
        postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/jwt");
        var response = httpClient.SendAsync(postRequest).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty bearer token response");
        }

        var authenticationResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.Authenticate);
        if (authenticationResponse == null || string.IsNullOrWhiteSpace(authenticationResponse.AccessToken)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid bearer token response");
        }
        if (authenticationResponse.TokenType != "Bearer") {
            throw new IBClientException($"IB Client returned invalid token type: {authenticationResponse.TokenType}");
        }
        _bearerToken = authenticationResponse.AccessToken;

        Logger?.LogInformation($"Received bearer token");
    }

    private void ValidateToken(HttpClient httpClient, string validateUrl) {
        var postRequest = new HttpRequestMessage(HttpMethod.Get, validateUrl);
        postRequest.Headers.Add("Authorization", $"Bearer {_bearerToken}");
        var response = httpClient.SendAsync(postRequest).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty validation response");
        }

        Logger?.LogInformation($"Validated bearer token");
    }

    private string ComputeClientAssertion(string uri) {

        // Load the private key from _privateKeyPath
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(_authConfiguration.PrivateKeyPath).AsSpan());
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256) {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        var now = DateTime.UtcNow;

        if (uri.EndsWith("/api/v1/token")) {
            var jwt = new JwtSecurityToken(
              audience: "/token",
              issuer: _authConfiguration.ClientId,
              claims: [
                new Claim("sub", _authConfiguration.ClientId),
              ],
              expires: now.AddSeconds(30),
              signingCredentials: signingCredentials
            );
            // set issued at to now - 10 seconds
            jwt.Payload["iat"] = new DateTimeOffset(now.AddSeconds(-10)).ToUnixTimeSeconds();
            jwt.Header["kid"] = "main";
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
        else if (uri.EndsWith("/api/v1/sso-sessions")) {
            var jwt = new JwtSecurityToken(
              issuer: _authConfiguration.ClientId,
              claims: [
                new Claim("ip", _publicIP),
                new Claim("credential", _authConfiguration.UserName),
              ],
              expires: now.AddDays(1),
              signingCredentials: signingCredentials
            );
            // set issued at to now - 10 seconds
            jwt.Payload["iat"] = new DateTimeOffset(now).ToUnixTimeSeconds();
            jwt.Header["kid"] = "main";
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
        else {
            throw new IBClientException($"({uri}) is invalid endpoint for client assertion");
        }
    }
}
