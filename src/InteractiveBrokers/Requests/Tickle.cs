using AppCore.Args;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace InteractiveBrokers.Requests;

internal class Tickle : Request
{
    private EventHandler<TickleArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Tickle(EventHandler<TickleArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        Uri = "v1/api/tickle";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.Tickle, HttpMethod.Post);
        if (!string.IsNullOrWhiteSpace(response.Error)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) response: {response.Error}");
        }
        if (response.IServer == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) not connected to local server");
        }
        if (!response.IServer.AuthStatus.connected) {
            Logger?.LogWarning($"IB Client ({httpClient.BaseAddress}) not connected to IBKR server");
        }
        if (!response.IServer.AuthStatus.authenticated) {
            Logger?.LogWarning($"IB Client ({httpClient.BaseAddress}) not authenticated to IBKR server");
        }

        _responseHandler?.Invoke(this, new TickleArgs {
            Session = response.Session,
            IsConnected = response.IServer.AuthStatus.connected,
            IsAuthenticated = response.IServer.AuthStatus.authenticated
        });
    }
}
