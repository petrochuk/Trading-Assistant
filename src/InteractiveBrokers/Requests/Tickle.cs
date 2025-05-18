using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Tickle : Request
{
    private EventHandler<Args.TickleArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Tickle(EventHandler<Args.TickleArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        Uri = "v1/api/tickle";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.Tickle);
        if (!string.IsNullOrWhiteSpace(response.Error)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) response: {response.Error}");
        }
        if (response.IServer == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not connected to local server");
        }
        if (!response.IServer.AuthStatus.connected) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not connected");
        }
        if (!response.IServer.AuthStatus.authenticated) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not authenticated");
        }

        _responseHandler?.Invoke(this, new Args.TickleArgs {
            Session = response.Session
        });
    }
}
