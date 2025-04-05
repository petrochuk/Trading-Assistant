using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Tickle : Request
{
    [SetsRequiredMembers]
    public Tickle() {
        Uri = "tickle";
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.PostAsync("tickle", null).ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        var tickleResponse = JsonSerializer.Deserialize<Responses.Tickle>(responseContent, JsonSerializerOptions);
        if (tickleResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid response");
        }
        if (!string.IsNullOrWhiteSpace(tickleResponse.Error)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) response: {tickleResponse.Error}");
        }
        if (tickleResponse.IServer == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not connected to local server");
        }
        if (!tickleResponse.IServer.AuthStatus.connected) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not connected");
        }
        if (!tickleResponse.IServer.AuthStatus.authenticated) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not authenticated");
        }
    }
}
