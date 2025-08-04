using AppCore.Args;
using AppCore.Extenstions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class SuppressWarnings : Request
{
    private readonly IEnumerable<string> _suppressionIds;

    [SetsRequiredMembers]
    public SuppressWarnings(IEnumerable<string> suppressionIds, string? bearerToken) : base (bearerToken) {
        Uri = "v1/api/iserver/questions/suppress";

        _suppressionIds = suppressionIds;
    }

    public override void Execute(HttpClient httpClient) {
        var content = new StringContent(
            $"{{\"messageIds\": [{string.Join(",", _suppressionIds.Select(id => $"\"{id}\""))}]}}",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.SuppressWarnings, HttpMethod.Post, content);
        if (response == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided null response for suppress warnings request");
        }

        if (response.status != "submitted") {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided error response for suppress warnings request: {response.status}");
        }
    }
}
