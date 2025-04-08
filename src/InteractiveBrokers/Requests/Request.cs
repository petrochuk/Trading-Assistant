using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal abstract class Request
{
    public Request() {
    }

    public required string Uri { get; set; }

    public abstract void Execute(HttpClient httpClient);
}
