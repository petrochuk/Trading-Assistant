using Microsoft.Extensions.Logging;

namespace InteractiveBrokers.Requests;

internal abstract class Request
{
    public Request() {
    }

    public required string Uri { get; set; }

    public ILogger<Request>? Logger { get; init; }

    public abstract void Execute(HttpClient httpClient);
}
