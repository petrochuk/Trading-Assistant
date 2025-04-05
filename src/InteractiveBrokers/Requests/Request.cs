using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal abstract class Request
{
    protected static readonly JsonSerializerOptions JsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Request() {
    }

    public required string Uri { get; set; }

    public abstract void Execute(HttpClient httpClient);
}
