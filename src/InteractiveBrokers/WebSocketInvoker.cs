
namespace InteractiveBrokers;

internal class WebSocketInvoker : HttpMessageInvoker
{
    public WebSocketInvoker(HttpMessageHandler handler) : base(handler) {
    }

    public override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) {
        return base.Send(request, cancellationToken);
    }

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        return base.SendAsync(request, cancellationToken);
    }
}
