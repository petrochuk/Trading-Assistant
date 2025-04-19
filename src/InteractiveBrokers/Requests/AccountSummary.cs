using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class AccountSummary : Request
{
    EventHandler<AccountSummaryArgs>? _responseHandler;

    [SetsRequiredMembers]
    public AccountSummary(string accountId, EventHandler<AccountSummaryArgs>? responseHandler) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        Uri = $"portfolio/{accountId}/summary";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.GetAsync(Uri).ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty account summary response");
        }
        var summaryResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.AccountSummaryArgs);
        if (summaryResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid account summary response");
        }

        _responseHandler?.Invoke(this, summaryResponse);
    }
}
