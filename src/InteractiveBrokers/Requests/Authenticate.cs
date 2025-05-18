using AppCore.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace InteractiveBrokers.Requests;

internal class Authenticate : Request
{
    EventHandler<Args.AuthenticatedArgs>? _responseHandler;
    private AuthenticationConfiguration _authConfiguration;

    [SetsRequiredMembers]
    public Authenticate(EventHandler<Args.AuthenticatedArgs>? responseHandler, AuthenticationConfiguration authConfiguration) {
        _responseHandler = responseHandler;
        _authConfiguration = authConfiguration ?? throw new ArgumentNullException(nameof(authConfiguration), "Authentication configuration cannot be null.");
        Uri = _authConfiguration.TokenUrl;
        if (string.IsNullOrWhiteSpace(_authConfiguration.PrivateKeyPath)) {
            throw new ArgumentNullException(nameof(_authConfiguration.PrivateKeyPath), "Private key path cannot be null or empty.");
        }
    }

    public override void Execute(HttpClient httpClient) {
        var postRequest = new HttpRequestMessage(HttpMethod.Post, Uri);
        var formData = new List<KeyValuePair<string, string>> {
            new("client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"),
            new("client_assertion", ComputeClientAssertion(Uri)),
            new("grant_type", "client_credentials"),
            new("scope", "sso-sessions.write"),
        };
        postRequest.Content = new FormUrlEncodedContent(formData);
        var response = httpClient.SendAsync(postRequest).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty authentication response");
        }
        var authenticationResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.Authenticate);
        if (authenticationResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid authentication response");
        }

        Logger?.LogInformation($"Authentication successful.");
    }

    private string ComputeClientAssertion(string uri) {

        // Load the private key from _privateKeyPath
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(_authConfiguration.PrivateKeyPath).AsSpan());
        var signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256) {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        var now = DateTime.UtcNow;
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
}
