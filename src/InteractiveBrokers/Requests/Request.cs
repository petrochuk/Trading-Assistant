using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace InteractiveBrokers.Requests;

internal abstract class Request
{
    public Request(string? bearerToken = null) {
        BearerToken = bearerToken;
    }

    public required string Uri { get; set; }

    public string? BearerToken { get; set; }

    public ILogger<Request>? Logger { get; init; }

    public abstract void Execute(HttpClient httpClient);

    protected T GetResponse<T>(HttpClient httpClient, string uri, int retryCount, JsonTypeInfo<T> jsonTypeInfo,
        HttpMethod? httpMethod = null, HttpContent? httpContent = null) {
        if (retryCount < 1) {
            throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count must be at least 1.");
        }

        for (int attempt = 1; attempt <= retryCount; attempt++) {
            try {
                return GetResponse(httpClient, uri, jsonTypeInfo, httpMethod, httpContent);
            } catch (Exception ex) {
                Logger?.LogError(ex, $"Attempt {attempt} to get response from {uri} failed.");
                if (attempt == retryCount) {
                    throw;
                }
                // Add a delay before retrying
                Thread.Sleep(2000 * attempt); // Exponential backoff
            }
        }

        throw new IBClientException($"Failed to get response from {uri} after {retryCount} attempts.");
    }

    protected T GetResponse<T>(HttpClient httpClient, string uri, JsonTypeInfo<T> jsonTypeInfo,
        HttpMethod? httpMethod = null, HttpContent? httpContent = null) {
        _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(uri)) {
            throw new ArgumentNullException(nameof(uri), "URI cannot be null or empty.");
        }

        var request = new HttpRequestMessage(httpMethod ?? HttpMethod.Get, uri);
        request.Content = httpContent;
        if (!string.IsNullOrWhiteSpace(BearerToken)) {
            request.Headers.Add("Authorization", $"Bearer {BearerToken}");
        }
        var response = httpClient.SendAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty response: {uri}");
        }

        var responseObject = JsonSerializer.Deserialize(responseContent, jsonTypeInfo);
        if (responseObject == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid response: {uri}");
        }

        return responseObject;
    }
}
